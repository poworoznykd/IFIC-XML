/*
 * FILE          : AdminMetadata.cs
 * PROJECT       : IFIC.FileIngestor
 * PROGRAMMER    : Darryl Poworoznyk
 * FIRST VERSION : 2025-08-17
 * DESCRIPTION   :
 *   Strongly typed representation of [ADMIN] metadata from Clarity LTCF 
 *   flat files, providing routing and contextual information.
 */

using System;
using System.Collections.Generic;
using IFIC.FileIngestor.Parsers;

namespace IFIC.FileIngestor.Models
{
    /// <summary>
    /// Parsed values from the [ADMIN] section of the queued data file.
    /// Only Fiscal and Quarter are required for routing; others are optional.
    /// </summary>
    public sealed class AdminMetadata
    {
        // Patient
        public string? FhirPatID { get; set; }
        public string? FhirPatKey { get; set; }
        public string? PatOper { get; set; }

        // Encounter
        public string? FhirEncID { get; set; }
        public string? FhirEncKey { get; set; }
        public string? EncOper { get; set; }

        // Assessment
        public string? FhirAsmID { get; set; }
        public string? RecId { get; set; }
        public string? AsmOper { get; set; }

        // Assessment type and routing
        public string? AsmType { get; set; }

        /// <summary>Required for routing. Example: "2025".</summary>
        public string? Fiscal { get; set; }

        /// <summary>Required for routing. Example: "Q3-2025".</summary>
        public string? Quarter { get; set; }

        /// <summary>
        /// Creates an <see cref="AdminMetadata"/> from a parsed flat file by reading the [ADMIN] dictionary.
        /// Keys are matched case-insensitively. Missing keys result in null properties.
        /// </summary>
        /// <param name="parsedFile">The parsed flat file that contains the [ADMIN] key/value pairs.</param>
        /// <returns>A new <see cref="AdminMetadata"/> instance populated from the [ADMIN] section.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="parsedFile"/> is null.</exception>
        public static AdminMetadata FromParsedFlatFile(ParsedFlatFile parsedFile)
        {
            if (parsedFile == null) throw new ArgumentNullException(nameof(parsedFile));

            // Use an OrdinalIgnoreCase view for safe key lookup regardless of how the parser created the dictionary.
            var admin = parsedFile.Admin ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Local helper for case-insensitive gets without throwing.
            static string? Get(Dictionary<string, string> dict, string key)
            {
                return dict.TryGetValue(key, out var value) ? value : null;
            }

            return new AdminMetadata
            {
                // Patient
                FhirPatID = Get(admin, "fhirPatID") ?? Get(admin, "FhirPatID"),
                FhirPatKey = Get(admin, "fhirPatKey") ?? Get(admin, "FhirPatKey"),
                PatOper = Get(admin, "patOper") ?? Get(admin, "PatOper"),

                // Encounter
                FhirEncID = Get(admin, "fhirEncID") ?? Get(admin, "FhirEncID"),
                FhirEncKey = Get(admin, "fhirEncKey") ?? Get(admin, "FhirEncKey"),
                EncOper = Get(admin, "encOper") ?? Get(admin, "EncOper"),

                // Assessment (QuestionnaireResponse)
                FhirAsmID = Get(admin, "fhirAsmID") ?? Get(admin, "FhirAsmID"),
                RecId = Get(admin, "rec_id") ?? Get(admin, "Rec_Id"),
                AsmOper = Get(admin, "asmOper") ?? Get(admin, "AsmOper"),
                AsmType = Get(admin, "asmType") ?? Get(admin, "AsmType"),

                // Routing
                Fiscal = Get(admin, "fiscal") ?? Get(admin, "Fiscal"),
                Quarter = Get(admin, "quarter") ?? Get(admin, "Quarter"),
            };
        }
    }
}
