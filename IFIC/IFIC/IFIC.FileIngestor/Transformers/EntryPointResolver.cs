using System.Xml.Linq;
using IFIC.FileIngestor.Models;

namespace IFIC.FileIngestor.Transformers
{
    public static class EntryPointResolver
    {
        private static readonly XNamespace ns = "http://hl7.org/fhir";

        /// <summary>
        /// Builds the correct <request> entry point for Encounter, Patient, or QuestionnaireResponse.
        /// Uses AdminMetadata values to determine whether to POST to uuid (new) or $update (existing).
        /// If ID is missing, a new GUID is generated.
        /// </summary>
        public static XElement BuildEntryPoint(AdminMetadata admin, string resourceType)
        {
            string? oper = null;
            string? id = null;

            switch (resourceType)
            {
                case "Encounter":
                    oper = admin.EncOper;
                    id = string.IsNullOrWhiteSpace(admin.FhirEncID) ? Guid.NewGuid().ToString() : admin.FhirEncID;
                    break;

                case "Patient":
                    oper = admin.PatOper;
                    id = string.IsNullOrWhiteSpace(admin.FhirPatID) ? Guid.NewGuid().ToString() : admin.FhirPatID;
                    break;

                case "QuestionnaireResponse":
                    oper = admin.AsmOper;
                    id = string.IsNullOrWhiteSpace(admin.FhirAsmID) ? Guid.NewGuid().ToString() : admin.FhirAsmID;
                    break;

                default:
                    throw new ArgumentException($"Unsupported resourceType: {resourceType}");
            }

            if (oper == "UPDATE")
            {
                return new XElement(ns + "request",
                    new XElement(ns + "method", new XAttribute("value", "POST")),
                    new XElement(ns + "url", new XAttribute("value", $"/{resourceType}/{id}/$update"))
                );
            }
            else
            {
                return new XElement(ns + "request",
                    new XElement(ns + "method", new XAttribute("value", "POST")),
                    new XElement(ns + "url", new XAttribute("value", $"urn:uuid:{id}"))
                );
            }
        }
    }
}
