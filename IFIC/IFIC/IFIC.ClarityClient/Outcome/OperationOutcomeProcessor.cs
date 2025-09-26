/*
 *  FILE          : OperationOutcomeProcessor.cs
 *  PROJECT       : IFIC.Outcome (or IFIC.Runner)
 *  DESCRIPTION   :
 *    CIHI OperationOutcome processor that:
 *      - Handles OperationOutcome as root or inside Bundle entries.
 *      - For each issue, chooses ONE primary iCode (from interRAI-iCode system),
 *        rewrites the message by replacing any iCode tokens with their mapped
 *        ElementCode (e.g., R7, A9), and writes a single note to the mapped Section.
 *      - NEW: If no iCode is present but the human-readable message contains a plain
 *        ElementCode token (e.g., "A8"), route to that Section letter (A) and surface
 *        the message as-is. This fixes cases like: "No response found for required item A8".
 *      - Sets ccrsSectionState.Section_<X> = '2'.
 *      - For asmOper in {CREATE, CORRECTION, DELETE}, marks Assessments as
 *        Incomplete and transmit=NO.
 *      - Unknown/no-issue fallback: marks assessment Incomplete/NO and sets ALL
 *        sections A..Z to '2' with a standard note.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using IFIC.ClarityClient;

namespace IFIC.Outcome
{
    /// <summary>
    /// Parses OperationOutcome XML and writes normalized error notes back into LTCF.
    /// </summary>
    public static class OperationOutcomeProcessor
    {
        // Matches iCode tokens in free text: iA9, iU2, iA5a, iB3b, case-insensitive
        private static readonly Regex ICodeToken = new Regex(@"\b[iI][A-Z][0-9]+[a-z]?\b", RegexOptions.Compiled);

        // Matches plain element codes in free text: A8, A12, B3b, R7 (no leading 'i')
        private static readonly Regex ElementCodeToken = new Regex(@"\b[A-Z][0-9]{1,3}[a-z]?\b", RegexOptions.Compiled);

        private const string FallbackUnknownExceptionNote =
            "Unknown exception occurred when submitting to CIHI. Please contact administrator and see runlog for more details.";

        /// <summary>
        /// Applies CIHI errors to the database by translating iCodes to ElementCodes for display,
        /// appending ONE section note per issue, updating section state, and marking assessment
        /// status when required. When the response has no usable issues (or XML is unparseable),
        /// marks the assessment Incomplete/NO and sets ALL section states to '2' with a standard note.
        /// </summary>
        public static async Task ApplyErrorsAsync(
            IClarityClient db,
            IElementMapping map,
            string recIdString,
            string asmOper,
            string operationOutcomeXml,
            CancellationToken ct)
        {
            if (!int.TryParse(recIdString?.Trim(), out var recId)) return;

            bool fallbackAllSections = string.IsNullOrWhiteSpace(operationOutcomeXml);
            var updates = new HashSet<(string Section, string Message)>(StringTupleComparer.OrdinalIgnoreCase);

            if (!fallbackAllSections)
            {
                try
                {
                    var xdoc = XDocument.Parse(operationOutcomeXml);
                    var outcomes = xdoc.Descendants().Where(e => e.Name.LocalName == "OperationOutcome").ToList();
                    if (outcomes.Count == 0) fallbackAllSections = true;

                    bool anyIssueProcessed = false;

                    foreach (var oo in outcomes)
                    {
                        var issues = oo.Descendants().Where(e => e.Name.LocalName == "issue").ToList();
                        if (issues.Count == 0) continue;

                        foreach (var issue in issues)
                        {
                            // 1) Build base message from display/diagnostics
                            var displays = issue.Descendants().Where(e => e.Name.LocalName == "coding")
                                .Select(c => c.Elements().FirstOrDefault(n => n.Name.LocalName == "display")?.Attribute("value")?.Value)
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .ToList();
                            var diag = issue.Elements().FirstOrDefault(e => e.Name.LocalName == "diagnostics")?.Attribute("value")?.Value;
                            if (!string.IsNullOrWhiteSpace(diag)) displays.Add(diag);
                            string baseMessage = displays.FirstOrDefault() ?? "Validation error returned by CIHI.";

                            // 2) Choose ONE primary iCode (prefer details/coding with system = interRAI-iCode; handle 2nd/3rd coding, etc.)
                            string? primaryICode = ExtractICodeFromIssue(issue, baseMessage);

                            // 3) If we still have no primary iCode, try to recover from a plain ElementCode in the text
                            //    Example: "No response found for required item A8" -> section "A"
                            string? derivedSectionFromPlainElement = null;
                            if (string.IsNullOrWhiteSpace(primaryICode))
                            {
                                var mE = ElementCodeToken.Match(baseMessage);
                                if (mE.Success)
                                {
                                    var plainElement = mE.Value; // e.g., "A8"
                                    derivedSectionFromPlainElement = plainElement.Substring(0, 1).ToUpperInvariant();
                                }
                            }

                            // 4) Build token → ElementCode replacements for readability (iU2 → R7, iA9 → A9)
                            var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            foreach (Match m in ICodeToken.Matches(baseMessage))
                            {
                                var token = m.Value;
                                if (!replacements.ContainsKey(token) && map.TryResolveICode(token, out _, out var elementCodeR, out var elementNameR))
                                {
                                    var repl = !string.IsNullOrEmpty(elementCodeR) ? elementCodeR : elementNameR; // prefer R7/A9; else name
                                    if (!string.IsNullOrEmpty(repl)) replacements[token] = repl;
                                }
                            }
                            string normalizedMessage = ReplaceTokens(baseMessage, replacements);

                            // 5) Resolve the single target section and build enriched message
                            string? section = null;
                            string messageToWrite;

                            if (!string.IsNullOrWhiteSpace(primaryICode) && map.TryResolveICode(primaryICode!, out var mappedSection, out var elementCode, out var elementName))
                            {
                                section = mappedSection; // mapped from iCode

                                // Enrich the message with a human label once per issue.
                                messageToWrite = BuildEnrichedMessage(normalizedMessage, elementCode, elementName);
                            }
                            else if (!string.IsNullOrWhiteSpace(derivedSectionFromPlainElement))
                            {
                                section = derivedSectionFromPlainElement; // derived from "A8" -> "A"
                                messageToWrite = baseMessage; // keep as-is when we didn't resolve an iCode
                            }
                            else
                            {
                                messageToWrite = baseMessage;
                            }

                            // Normalize for DB rules (e.g., S -> R)
                            var dbSection = NormalizeDbSection(section);

                            // 6) Queue the update if a section was resolved
                            if (!string.IsNullOrWhiteSpace(dbSection))
                            {
                                updates.Add((dbSection!, messageToWrite));
                                anyIssueProcessed = true;
                            }
                        }
                    }

                    if (!anyIssueProcessed && updates.Count == 0)
                    {
                        fallbackAllSections = true;
                    }
                }
                catch
                {
                    fallbackAllSections = true;
                }
            }

            // --- Plan B: If parsing captured nothing but XML contains display/diagnostics, salvage a Section note ---
            if (!fallbackAllSections && updates.Count == 0)
            {
                try
                {
                    var mDisplay = Regex.Match(operationOutcomeXml ?? string.Empty, @"<display\s+value=""([^""]+)""", RegexOptions.IgnoreCase);
                    var mDiag = Regex.Match(operationOutcomeXml ?? string.Empty, @"<diagnostics\s+value=""([^""]+)""", RegexOptions.IgnoreCase);
                    var msg = mDisplay.Success ? mDisplay.Groups[1].Value : (mDiag.Success ? mDiag.Groups[1].Value : null);

                    if (!string.IsNullOrWhiteSpace(msg))
                    {
                        var mElem = ElementCodeToken.Match(msg);
                        if (mElem.Success)
                        {
                            var sec = NormalizeDbSection(mElem.Value.Substring(0, 1));
                            updates.Add((sec, msg));
                        }
                    }
                }
                catch
                {
                    // ignore and let the fallback below fire
                }
            }

            // Apply per-issue DB updates collected above
            if (!fallbackAllSections && updates.Count > 0)
            {
                foreach (var (section, message) in updates)
                {
                    await db.AppendSectionNoteAsync(section, recId, message, ct);
                    await db.SetSectionStateAsync(section, recId, state: "2", ct);
                }

                if (asmOper.Equals("CREATE", StringComparison.OrdinalIgnoreCase)
                    || asmOper.Equals("CORRECTION", StringComparison.OrdinalIgnoreCase)
                    || asmOper.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
                {
                    await db.MarkAssessmentIncompleteNotTransmittedAsync(recId, ct);
                }

                return;
            }

            // Unknown/exception with no issues → mark incomplete and touch all sections
            await db.MarkAssessmentIncompleteNotTransmittedAsync(recId, ct);
            for (char ch = 'A'; ch <= 'Z'; ch++)
            {
                string section = NormalizeDbSection(ch.ToString());
                await db.SetSectionStateAsync(section, recId, state: "2", ct);
                await db.AppendSectionNoteAsync(section, recId, FallbackUnknownExceptionNote, ct);
            }
        }

        /// <summary>
        /// Finds the best iCode for an issue by scanning details/coding (in order) for interRAI-iCode,
        /// then any coding under the issue, then falling back to the first iCode token in text.
        /// </summary>
        private static string? ExtractICodeFromIssue(XElement issue, string baseMessage)
        {
            static string? CodeFromCoding(XElement coding)
                => coding.Elements().FirstOrDefault(n => n.Name.LocalName == "code")?.Attribute("value")?.Value;

            // (1) Prefer details/coding[*] entries (where multiple codings commonly appear)
            var detailsCodings = issue
                .Elements().Where(e => e.Name.LocalName == "details")
                .Descendants().Where(e => e.Name.LocalName == "coding");

            foreach (var c in detailsCodings)
            {
                var sys = c.Elements().FirstOrDefault(n => n.Name.LocalName == "system")?.Attribute("value")?.Value;
                if (!string.IsNullOrWhiteSpace(sys) &&
                    sys.IndexOf("interRAI-iCode", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var code = CodeFromCoding(c);
                    if (!string.IsNullOrWhiteSpace(code)) return code;
                }
            }

            // (2) Fallback: any coding in the issue with interRAI-iCode (covers odd placements)
            var allCodings = issue.Descendants().Where(e => e.Name.LocalName == "coding");
            foreach (var c in allCodings)
            {
                var sys = c.Elements().FirstOrDefault(n => n.Name.LocalName == "system")?.Attribute("value")?.Value;
                if (!string.IsNullOrWhiteSpace(sys) &&
                    sys.IndexOf("interRAI-iCode", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var code = CodeFromCoding(c);
                    if (!string.IsNullOrWhiteSpace(code)) return code;
                }
            }

            // (3) Last resort: first iCode token from the human-readable message
            var m = ICodeToken.Match(baseMessage ?? string.Empty);
            return m.Success ? m.Value : null;
        }

        /// <summary>
        /// Builds a final message by appending a single standardized element label when available.
        /// Example suffix: " [Element: A7a – <Element Name>]".
        /// </summary>
        private static string BuildEnrichedMessage(string normalizedMessage, string? elementCode, string? elementName)
        {
            var code = (elementCode ?? string.Empty).Trim();
            var name = (elementName ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(name))
                return normalizedMessage;

            // Avoid duplicating if message already contains both code and name textually
            var alreadyContains = (!string.IsNullOrEmpty(code) && normalizedMessage.IndexOf(code, StringComparison.OrdinalIgnoreCase) >= 0)
                                  && (!string.IsNullOrEmpty(name) && normalizedMessage.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
            if (alreadyContains) return normalizedMessage;

            var label = !string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(name)
                        ? $" [Element: {code} – {name}]"
                        : !string.IsNullOrEmpty(code)
                          ? $" [Element: {code}]"
                          : $" [Element: {name}]";

            return normalizedMessage + label;
        }


        /// <summary>
        /// Replaces tokens found in <paramref name=\"text\"/> using <paramref name=\"replacements\"/>,
        /// applying longest-match-first to avoid partial overlaps (e.g., iA9 before iA9a).
        /// </summary>
        private static string ReplaceTokens(string text, IDictionary<string, string> replacements)
        {
            if (replacements.Count == 0 || string.IsNullOrEmpty(text)) return text;
            var ordered = replacements.Keys.OrderByDescending(k => k.Length).ToList();
            string result = text;
            foreach (var token in ordered)
            {
                var repl = replacements[token];
                result = Regex.Replace(result, @"\(\s*" + Regex.Escape(token) + @"\s*\)", "(" + repl + ")", RegexOptions.IgnoreCase);
                result = Regex.Replace(result, @"\b" + Regex.Escape(token) + @"\b", repl, RegexOptions.IgnoreCase);
            }
            return result;
        }

        /// <summary>
        /// Normalizes the db section - uses toupper
        /// </summary>
        /// <param name="section">The section to normalize</param>
        /// <returns>An uppercase section</returns>
        private static string NormalizeDbSection(string? section)
        {
            if (string.IsNullOrWhiteSpace(section)) return section ?? string.Empty;
            char ch = char.ToUpperInvariant(section[0]);
            // Business rule: S* (e.g., S1, S2) write to the R section in the database (Section_R exists; Section_S does not).\n            if (ch == 'S') ch = 'R';
            return ch.ToString();
        }

        /// <summary>
        /// Case-insensitive comparer for (string A, string B) tuples,
        /// used to deduplicate or key lookups by treating both items with OrdinalIgnoreCase.
        /// </summary>
        private sealed class StringTupleComparer : IEqualityComparer<(string A, string B)>
        {
            public static readonly StringTupleComparer OrdinalIgnoreCase = new();
            public bool Equals((string A, string B) x, (string A, string B) y)
                => string.Equals(x.A, y.A, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.B, y.B, StringComparison.OrdinalIgnoreCase);
            public int GetHashCode((string A, string B) obj)
                => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.A) * 397
                 ^ StringComparer.OrdinalIgnoreCase.GetHashCode(obj.B);
        }
    }
}
