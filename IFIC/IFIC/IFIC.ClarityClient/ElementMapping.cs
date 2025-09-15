/*
 *  FILE          : ElementMapping.cs
 *  PROJECT       : IFIC.ClarityClient
 *  DESCRIPTION   :
 *    Loads the canonical iCode → (Section, Element Name) map from the spreadsheet
 *    at construction time and serves fast in-memory lookups thereafter.
 *
 *  SHEET FORMAT  : Worksheet "elementMap" with headers. Supported headers (case-insensitive):
 *                  - iCodeName | iCode | code
 *                  - elementName | element | display
 *                  - Section | cihiSection   (CIHI's section notation, e.g., S2, R7)
 *                  - DbSection | databaseSection | db_section (OPTIONAL override for DB)
 *
 *  RULES         :
 *    - If DbSection is present on a row, it wins (e.g., "R" or "R7" → we use "R").
 *    - Else we parse the Section cell and take the first letter A..Z (e.g., "R7" → "R", "S2" → "S").
 *    - If element name is missing, mapping still resolves the section; replacement in messages
 *      will be a no-op (token remains the same).
 *
 *  DEPENDENCY    : ClosedXML (NuGet)
 *
 *  PASTE LOCATION : IFIC.ClarityClient/ElementMapping.cs (replace file)
 */

using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;

namespace IFIC.ClarityClient
{
    /// <summary>
    /// Spreadsheet-backed implementation of <see cref="IElementMapping"/>.
    /// Accepts both "iA5a" and "cihiA5a" keys by installing alternate forms.
    /// </summary>
    public sealed class ElementMapping : IElementMapping
    {
        private readonly Dictionary<string, (string Section, string Element)> map; // case-insensitive

        /// <summary>
        /// Loads the mapping from the given Excel file into memory.
        /// </summary>
        /// <param name="excelPath">Absolute path to Error-Element-Mapping.xlsx (sheet: elementMap).</param>
        /// <exception cref="ArgumentNullException">When <paramref name="excelPath"/> is null/empty.</exception>
        /// <exception cref="FileNotFoundException">When the file cannot be found.</exception>
        public ElementMapping(string excelPath)
        {
            if (string.IsNullOrWhiteSpace(excelPath)) throw new ArgumentNullException(nameof(excelPath));
            if (!File.Exists(excelPath)) throw new FileNotFoundException("Element mapping .xlsx not found", excelPath);

            map = new(StringComparer.OrdinalIgnoreCase);

            using var wb = new XLWorkbook(excelPath);
            var ws = wb.Worksheet("elementMap");

            // Detect headers from the first used row. If unavailable, fall back to fixed columns (1..3).
            var headerRow = ws.FirstRowUsed();
            int iCodeCol = 1, elementCol = 2, sectionCol = 3, dbSectionCol = 0;

            if (headerRow != null)
            {
                // Map header names to columns (1-based)
                var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var cell in headerRow.CellsUsed())
                {
                    var name = (cell.GetString() ?? string.Empty).Trim();
                    if (!string.IsNullOrEmpty(name))
                        headers[name] = cell.Address.ColumnNumber;
                }

                iCodeCol = FindColumn(headers, "icode", "icodeName", "code");
                elementCol = FindColumn(headers, "elementname", "element", "display");
                sectionCol = FindColumn(headers, "section", "cihisection");
                dbSectionCol = FindColumn(headers, "dbsection", "databasesection", "db_section", "sectiondb");

                // Fallbacks to preserve your original 3-column format
                if (iCodeCol == 0) iCodeCol = 1;
                if (elementCol == 0) elementCol = 2;
                if (sectionCol == 0) sectionCol = 3;
                // dbSectionCol is optional by design
            }

            bool firstRow = true;
            foreach (var row in ws.RowsUsed())
            {
                if (firstRow) { firstRow = false; continue; } // skip header

                string iCode = SafeGet(row, iCodeCol);
                string element = SafeGet(row, elementCol);
                string secCell = dbSectionCol > 0 ? SafeGet(row, dbSectionCol) : SafeGet(row, sectionCol);

                if (string.IsNullOrEmpty(iCode)) continue;

                // Resolve DB section letter:
                //   - Prefer explicit DbSection cell (if present)
                //   - Else parse the first A..Z letter from secCell (handles "R7" → "R", "S2" → "S")
                string dbSectionLetter = ExtractDbSectionLetter(secCell);
                if (string.IsNullOrEmpty(dbSectionLetter))
                {
                    // If still empty, skip this row (cannot form Section_<X>)
                    continue;
                }

                // Install mapping
                map[iCode] = (dbSectionLetter, element);

                // Also accept the alternate "cihiA5a"/"iA5a" forms
                if (iCode.StartsWith("cihi", StringComparison.OrdinalIgnoreCase))
                {
                    var alt = "i" + iCode.Substring(4);
                    TryAddAlt(alt, dbSectionLetter, element);
                }
                else if (iCode.StartsWith("i", StringComparison.OrdinalIgnoreCase))
                {
                    var alt = "cihi" + iCode.Substring(1);
                    TryAddAlt(alt, dbSectionLetter, element);
                }
            }

            // Local helpers
            static int FindColumn(Dictionary<string, int> headers, params string[] names)
            {
                foreach (var n in names)
                {
                    foreach (var kv in headers)
                    {
                        if (HeaderMatches(kv.Key, n)) return kv.Value;
                    }
                }
                return 0;
            }

            static bool HeaderMatches(string header, string token)
            {
                // Compare by normalized alphanumerics, case-insensitive
                static string N(string s)
                {
                    var span = s.AsSpan().Trim();
                    var arr = new char[span.Length];
                    int j = 0;
                    for (int i = 0; i < span.Length; i++)
                    {
                        char ch = span[i];
                        if (char.IsLetterOrDigit(ch)) arr[j++] = char.ToLowerInvariant(ch);
                    }
                    return new string(arr, 0, j);
                }
                return N(header).Equals(N(token), StringComparison.OrdinalIgnoreCase);
            }

            static string SafeGet(IXLRow row, int col)
            {
                if (col <= 0) return string.Empty;
                return (row.Cell(col).GetString() ?? string.Empty).Trim();
            }

            static string ExtractDbSectionLetter(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return string.Empty;
                var span = s.Trim().ToUpperInvariant().AsSpan();
                for (int i = 0; i < span.Length; i++)
                {
                    char ch = span[i];
                    if (ch >= 'A' && ch <= 'Z') return ch.ToString();
                }
                return string.Empty;
            }

            void TryAddAlt(string altKey, string section, string elementName)
            {
                if (!map.ContainsKey(altKey))
                    map.Add(altKey, (section, elementName));
            }
        }

        /// <inheritdoc />
        public bool TryResolveICode(string iCode, out string sectionLetter, out string elementName)
        {
            sectionLetter = string.Empty;
            elementName = string.Empty;
            if (string.IsNullOrWhiteSpace(iCode)) return false;

            if (map.TryGetValue(iCode.Trim(), out var v))
            {
                sectionLetter = v.Section;
                elementName = v.Element; // may be empty if sheet didn't provide a display name
                return true;
            }
            return false;
        }
    }
}
