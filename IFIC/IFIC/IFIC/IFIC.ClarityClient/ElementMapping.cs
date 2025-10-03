/*
 *  FILE          : ElementMapping.cs
 *  PROJECT       : IFIC.ClarityClient
 *  DESCRIPTION   :
 *    Loads the canonical iCode → (DB Section letter, ElementCode, ElementName) map from the spreadsheet
 *    and serves fast in-memory lookups thereafter.
 *
 *  SHEET FORMAT  : Worksheet "elementMap" (header row) with columns:
 *                  [1] iCodeName       (e.g., iU2, iA9, or element code like B1)
 *                  [2] elementName     (friendly display text)
 *                  [3] Section         (CIHI label, e.g., S2, R7, A9)
 *                  [4] DbSection       (OPTIONAL DB override; accepts R or R7; R7→R for DB letter)
 *
 *  RULES         :
 *    - DB section letter used for table names is derived from DbSection (if present) else Section → first A..Z.
 *    - ElementCode shown in rewritten messages prefers DbSection if it contains letter+digits (e.g., R7),
 *      else uses Section if it contains letter+digits (e.g., A9), else empty.
 *    - Accepts both "iA5a" and "cihiA5a" alternate keys.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace IFIC.ClarityClient
{
    /// <summary>
    /// Spreadsheet-backed implementation of <see cref="IElementMapping"/>.
    /// </summary>
    public sealed class ElementMapping : IElementMapping
    {
        private readonly Dictionary<string, (string SectionLetter, string ElementCode, string ElementName)> map;
        private static readonly Regex ElementCodeToken = new Regex(@"^[A-Z][0-9]+[a-z]?$", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of <see cref="ElementMapping"/> and loads the mapping into memory.
        /// </summary>
        /// <param name="excelPath">Absolute path to <c>Error-Element-Mapping.xlsx</c> (sheet: <c>elementMap</c>).</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="excelPath"/> is null or whitespace.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the file cannot be found.</exception>
        public ElementMapping(string excelPath)
        {
            if (string.IsNullOrWhiteSpace(excelPath)) throw new ArgumentNullException(nameof(excelPath));
            if (!File.Exists(excelPath)) throw new FileNotFoundException("Element mapping .xlsx not found", excelPath);

            map = new(StringComparer.OrdinalIgnoreCase);

            using var wb = new XLWorkbook(excelPath);
            var ws = wb.Worksheet("elementMap");

            bool firstRow = true;
            foreach (var row in ws.RowsUsed())
            {
                if (firstRow) { firstRow = false; continue; }

                string key = Safe(row, 1); // iCodeName (iU2 / iA9 / B1)
                string name = Safe(row, 2); // Element Name
                string section = Safe(row, 3); // CIHI label (S2/R7/A9)
                string dbSection = Safe(row, 4); // OPTIONAL DB override (R or R7)

                if (string.IsNullOrEmpty(key)) continue;

                string dbLetter = FirstLetterAZ(!string.IsNullOrEmpty(dbSection) ? dbSection : section);
                if (string.IsNullOrEmpty(dbLetter)) continue; // cannot address Section_<X>

                string displayCode = PreferElementCode(dbSection, section);

                map[key] = (dbLetter, displayCode, name);

                // Also accept the alternate "cihiA5a"/"iA5a" forms
                if (key.StartsWith("cihi", StringComparison.OrdinalIgnoreCase))
                {
                    var alt = "i" + key.Substring(4);
                    TryAddAlt(alt, dbLetter, displayCode, name);
                }
                else if (key.StartsWith("i", StringComparison.OrdinalIgnoreCase))
                {
                    var alt = "cihi" + key.Substring(1);
                    TryAddAlt(alt, dbLetter, displayCode, name);
                }
            }

            /// <summary>
            /// Reads a cell as a trimmed string or an empty string when missing.
            /// </summary>
            static string Safe(IXLRow row, int col)
                => (row.Cell(col).GetString() ?? string.Empty).Trim();

            /// <summary>
            /// Extracts the first A..Z letter from a section-like value (e.g., "R7" → "R").
            /// </summary>
            static string FirstLetterAZ(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return string.Empty;
                foreach (char ch in s.Trim().ToUpperInvariant())
                    if (ch >= 'A' && ch <= 'Z') return ch.ToString();
                return string.Empty;
            }

            /// <summary>
            /// Picks a human-facing ElementCode for display (e.g., "R7", "A9").
            /// Prefers <paramref name="primary"/>; falls back to <paramref name="fallback"/>.
            /// </summary>
            static string PreferElementCode(string primary, string fallback)
            {
                string pick = !string.IsNullOrEmpty(primary) ? primary : fallback;
                pick = pick?.Trim().ToUpperInvariant() ?? string.Empty;
                if (ElementCodeToken.IsMatch(pick)) return pick; // e.g., R7, A9, B1, A9a
                // Try to extract something like Letter+Digits from within the string
                var m = Regex.Match(pick, @"([A-Z][0-9]+[a-z]?)");
                return m.Success ? m.Groups[1].Value : string.Empty;
            }

            /// <summary>
            /// Adds an alternate key (e.g., "cihiA5a" for "iA5a") if it is not already present.
            /// </summary>
            void TryAddAlt(string altKey, string sectionLetter, string elementCode, string elementName)
            {
                if (!map.ContainsKey(altKey))
                    map.Add(altKey, (sectionLetter, elementCode, elementName));
            }
        }

        /// <summary>
        /// Attempts to resolve an iCode (or plain element code) to mapping details.
        /// </summary>
        /// <param name="iCode">interRAI token such as <c>iU2</c> / <c>iA9</c>, or an element code like <c>B1</c>.</param>
        /// <param name="sectionLetter">Out: single letter <c>A..Z</c> designating <c>Section_&lt;X&gt;</c> in the DB.</param>
        /// <param name="elementCode">Out: display code such as <c>R7</c>/<c>A9</c> (empty if unavailable).</param>
        /// <param name="elementName">Out: human-friendly Element Name (empty if unavailable).</param>
        /// <returns><c>true</c> if a mapping exists; otherwise <c>false</c>.</returns>
        public bool TryResolveICode(string iCode, out string sectionLetter, out string elementCode, out string elementName)
        {
            sectionLetter = string.Empty;
            elementCode = string.Empty;
            elementName = string.Empty;
            if (string.IsNullOrWhiteSpace(iCode)) return false;

            if (map.TryGetValue(iCode.Trim(), out var v))
            {
                sectionLetter = v.SectionLetter;
                elementCode = v.ElementCode;
                elementName = v.ElementName;
                return true;
            }
            return false;
        }
    }
}
