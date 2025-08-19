using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace IFIC.FileIngestor.Parsers
{
    using System;
    using System.Xml.Linq;

    public static class CihiResponseParser
    {
        /// <summary>
        /// Parses a CIHI transaction-response Bundle and extracts IDs
        /// for Patient, Encounter, and QuestionnaireResponse resources.
        /// </summary>
        /// <param name="apiResponse">The raw XML response string from CIHI.</param>
        /// <param name="patientId">Out: CIHI Patient resource ID (or null if missing).</param>
        /// <param name="encounterId">Out: CIHI Encounter resource ID (or null if missing).</param>
        /// <param name="questionnaireId">Out: CIHI QuestionnaireResponse ID (or null if missing).</param>
        public static void ExtractResourceIds(
            string apiResponse,
            out string? patientId,
            out string? encounterId,
            out string? questionnaireId)
        {
            patientId = null;
            encounterId = null;
            questionnaireId = null;

            if (string.IsNullOrWhiteSpace(apiResponse))
                return;

            var ns = (XNamespace)"http://hl7.org/fhir";

            // Find first '<' in case response contains logs or prefixes
            int firstLt = apiResponse.IndexOf('<');
            if (firstLt < 0) return;

            string xml = apiResponse.Substring(firstLt).Trim();
            var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);

            foreach (var entry in doc.Descendants(ns + "entry"))
            {
                var location = entry.Element(ns + "response")?
                                   .Element(ns + "location")?
                                   .Attribute("value")?.Value;

                if (string.IsNullOrWhiteSpace(location))
                    continue;

                if (location.StartsWith("Patient/", StringComparison.OrdinalIgnoreCase))
                {
                    patientId = location.Replace("Patient/", "");
                }
                else if (location.StartsWith("Encounter/", StringComparison.OrdinalIgnoreCase))
                {
                    encounterId = location.Replace("Encounter/", "");
                }
                else if (location.StartsWith("QuestionnaireResponse/", StringComparison.OrdinalIgnoreCase))
                {
                    questionnaireId = location.Replace("QuestionnaireResponse/", "");
                }
            }
        }
    }

}
