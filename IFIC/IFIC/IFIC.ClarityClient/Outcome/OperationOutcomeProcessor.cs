/*
 *  FILE          : OperationOutcomeProcessor.cs
 *  PROJECT       : IFIC.Outcome (or IFIC.Runner)
 *  DESCRIPTION   :
 *    CIHI OperationOutcome processor that:
 *      - Handles OperationOutcome as root or inside Bundle entries.
 *      - For each issue, chooses ONE primary iCode (from interRAI-iCode system),
 *        rewrites the display/diagnostics message by replacing any iCode tokens
 *        with their Element Names, and writes a single note to the mapped Section.
 *      - Sets ccrsSectionState.Section_<X> = '2'.
 *      - For asmOper in {CREATE, CORRECTION, DELETE}, marks Assessments as
 *        Incomplete and transmit=NO.
 *
 *  PASTE LOCATION : IFIC.Outcome/OperationOutcomeProcessor.cs (replace file)
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

        /// <summary>
        /// Applies CIHI errors to the database by translating iCodes to Element Names,
        /// rewriting messages, appending ONE section note per issue, updating section
        /// state, and marking assessment status when required.
        /// </summary>
        /// <param name="db">Database client for LTCF updates.</param>
        /// <param name="map">Element mapping used to resolve iCodes to (Section, Element Name).</param>
        /// <param name="recIdString">Assessment record id as string (parsed to INT).</param>
        /// <param name="asmOper">Assessment operation label (CREATE | CORRECTION | DELETE | ...).</param>
        /// <param name="operationOutcomeXml">Raw OperationOutcome (or Bundle) XML received from CIHI.</param>
        /// <param name="ct">Cancellation token.</param>
        public static async Task ApplyErrorsAsync(
            IClarityClient db,
            IElementMapping map,
            string recIdString,
            string asmOper,
            string operationOutcomeXml,
            CancellationToken ct)
        {
            if (!int.TryParse(recIdString?.Trim(), out var recId)) return;
            if (string.IsNullOrWhiteSpace(operationOutcomeXml)) return;

            var xdoc = XDocument.Parse(operationOutcomeXml);

            // Harvest OperationOutcome nodes (root or inside Bundle/entry/resource)
            var outcomes = xdoc.Descendants().Where(e => e.Name.LocalName == "OperationOutcome");

            // De-dup final (Section, Message) so repeated issues don't spam notes
            var updates = new HashSet<(string Section, string Message)>(StringTupleComparer.OrdinalIgnoreCase);

            foreach (var oo in outcomes)
            {
                var issues = oo.Descendants().Where(e => e.Name.LocalName == "issue");
                foreach (var issue in issues)
                {
                    // 1) Build a base message from display/diagnostics
                    var displays = issue.Descendants().Where(e => e.Name.LocalName == "coding")
                        .Select(c => c.Elements().FirstOrDefault(n => n.Name.LocalName == "display")?.Attribute("value")?.Value)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();

                    var diag = issue.Elements().FirstOrDefault(e => e.Name.LocalName == "diagnostics")?.Attribute("value")?.Value;
                    if (!string.IsNullOrWhiteSpace(diag)) displays.Add(diag);

                    string baseMessage = displays.FirstOrDefault() ?? "Validation error returned by CIHI.";

                    // 2) Choose ONE primary iCode: prefer coding with system = *interRAI-iCode*
                    string? primaryICode = issue.Descendants().Where(e => e.Name.LocalName == "coding")
                        .Where(c =>
                        {
                            var sys = c.Elements().FirstOrDefault(n => n.Name.LocalName == "system")?.Attribute("value")?.Value;
                            return !string.IsNullOrWhiteSpace(sys)
                                   && sys!.IndexOf("interRAI-iCode", StringComparison.OrdinalIgnoreCase) >= 0;
                        })
                        .Select(c => c.Elements().FirstOrDefault(n => n.Name.LocalName == "code")?.Attribute("value")?.Value)
                        .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

                    // Fallback to first iCode token found in text
                    if (string.IsNullOrWhiteSpace(primaryICode))
                    {
                        var m = ICodeToken.Match(baseMessage);
                        if (m.Success) primaryICode = m.Value;
                    }

                    // If still no iCode, optionally allow mapping a system/business code (rare)
                    if (string.IsNullOrWhiteSpace(primaryICode))
                    {
                        var sysCode = issue.Descendants().Where(e => e.Name.LocalName == "coding")
                            .Select(c => c.Elements().FirstOrDefault(n => n.Name.LocalName == "code")?.Attribute("value")?.Value)
                            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

                        if (!string.IsNullOrWhiteSpace(sysCode) && map.TryResolveICode(sysCode!, out var sec0, out _))
                        {
                            updates.Add((sec0, baseMessage));
                        }
                        continue; // move to next issue
                    }

                    // 3) For readability, replace ALL iCode tokens appearing in the message
                    //    with their Element Names (even if primary is just one).
                    var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (Match m in ICodeToken.Matches(baseMessage))
                    {
                        var token = m.Value;
                        if (!replacements.ContainsKey(token) && map.TryResolveICode(token, out _, out var name))
                        {
                            replacements[token] = name;
                        }
                    }
                    string normalizedMessage = ReplaceTokensWithNames(baseMessage, replacements);

                    // 4) Resolve the single target section from the PRIMARY iCode and queue ONE update
                    if (map.TryResolveICode(primaryICode!, out var section, out _))
                    {
                        updates.Add((section, normalizedMessage));
                    }
                }
            }

            // Apply DB updates
            foreach (var (section, message) in updates)
            {
                await db.AppendSectionNoteAsync(section, recId, message, ct);
                await db.SetSectionStateAsync(section, recId, state: "2", ct);
            }

            // Mark assessment as not transmitted for certain operations
            if (asmOper.Equals("CREATE", StringComparison.OrdinalIgnoreCase)
                || asmOper.Equals("CORRECTION", StringComparison.OrdinalIgnoreCase)
                || asmOper.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
            {
                await db.MarkAssessmentIncompleteNotTransmittedAsync(recId, ct);
            }
        }

        /// <summary>
        /// Replaces any iCode tokens in <paramref name="text"/> with their element names
        /// using the provided <paramref name="replacements"/> map. Longest tokens are
        /// replaced first to avoid partial overlaps (e.g., iA9 before iA9a).
        /// </summary>
        private static string ReplaceTokensWithNames(string text, IDictionary<string, string> replacements)
        {
            if (replacements.Count == 0 || string.IsNullOrEmpty(text)) return text;

            var ordered = replacements.Keys.OrderByDescending(k => k.Length).ToList();
            string result = text;
            foreach (var token in ordered)
            {
                var name = replacements[token];
                result = Regex.Replace(result, $@"\(\s*{Regex.Escape(token)}\s*\)", $"({name})", RegexOptions.IgnoreCase);
                result = Regex.Replace(result, $@"\b{Regex.Escape(token)}\b", name, RegexOptions.IgnoreCase);
            }
            return result;
        }

        /// <summary>
        /// Case-insensitive tuple comparer used by the HashSet of (Section, Message).
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
