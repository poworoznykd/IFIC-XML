/*
 * FILE          : FlatFileParser.cs
 * PROJECT       : IFIC-XML
 * PROGRAMMER    : Darryl Poworoznyk
 * FIRST VERSION : 2025-08-02
 * DESCRIPTION   :
 *   Reads Clarity LTCF flat files and converts them into ParsedFlatFile objects
 *   for downstream transformation into FHIR-compliant resources.
 */

using System;
using System.Collections.Generic;
using System.IO;
using IFIC.FileIngestor.Models;

namespace IFIC.FileIngestor.Parsers
{
    /// <summary>
    /// Provides functionality to read a flat file and parse it into structured sections.
    /// </summary>
    public class FlatFileParser
    {
        /// <summary>
        /// Parses a Clarity LTCF flat file into a ParsedFlatFile object.
        /// </summary>
        /// <param name="filePath">Path to the flat file.</param>
        /// <returns>ParsedFlatFile object containing Admin, Patient, Encounter, and Assessment sections.</returns>
        public ParsedFlatFile Parse(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Flat file not found: {filePath}");
            }

            var parsedFile = new ParsedFlatFile();
            string currentSection = null;

            foreach (var rawLine in File.ReadLines(filePath))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Trim('[', ']');
                    continue;
                }

                if (currentSection == null) continue;

                var parts = line.Split('=', 2);
                if (parts.Length != 2) continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                switch (currentSection.ToUpperInvariant())
                {
                    case "ADMIN":
                        parsedFile.Admin[key] = value;
                        break;
                    case "PATIENT":
                        parsedFile.Patient[key] = value;
                        break;
                    case "ENCOUNTER":
                        parsedFile.Encounter[key] = value;
                        break;
                    default:
                        if (!parsedFile.AssessmentSections.ContainsKey(currentSection))
                        {
                            parsedFile.AssessmentSections[currentSection] =
                                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        }
                        parsedFile.AssessmentSections[currentSection][key] = value;
                        break;
                }
            }

            return parsedFile;
        }
    }
}
