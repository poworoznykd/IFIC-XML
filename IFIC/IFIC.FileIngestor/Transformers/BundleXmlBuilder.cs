using IFIC.FileIngestor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace IFIC.FileIngestor.Transformers
{
    public class BundleXmlBuilder
    {
        private static readonly XNamespace ns = "http://hl7.org/fhir";

        public XDocument BuildFullBundle(
            ParsedFlatFile parsedFile,
            PatientXmlBuilder patientBuilder,
            EncounterXmlBuilder encounterXmlBuilder,
            QuestionnaireResponseBuilder questionnaireResponseBuilder,
            AdminMetadata adminMeta)
        {
            if (parsedFile == null)
            {
                throw new ArgumentNullException(nameof(parsedFile), "Parsed flat file cannot be null.");
            }
            
            string patientId = string.IsNullOrEmpty(adminMeta.FhirPatID) ? Guid.NewGuid().ToString() : adminMeta.FhirPatID;
            string encounterId = string.IsNullOrEmpty(adminMeta.FhirEncID) ? Guid.NewGuid().ToString() : adminMeta.FhirEncID;

            // Generate unique IDs for resources
            string bundleId = Guid.NewGuid().ToString();
            string questionnaireResponseId = Guid.NewGuid().ToString();

            // Create Bundle document
            var bundleElements = new List<object>
            {
                new XElement(ns + "id", new XAttribute("value", bundleId)),
                new XElement(ns + "type", new XAttribute("value", "transaction"))
            };
           
            if (encounterXmlBuilder != null && adminMeta.EncOper != "USE")
            {
                var encounterEntry = encounterXmlBuilder.BuildEncounterEntry(
                    parsedFile,
                    patientId,
                    encounterId
                );
                bundleElements.Add(encounterEntry);
            }
            if (questionnaireResponseBuilder != null)
            {
                var questionnaireEntry = questionnaireResponseBuilder.BuildQuestionnaireResponseEntry(
                    parsedFile,
                    patientId,
                    encounterId,
                    questionnaireResponseId
                );
                bundleElements.Add(questionnaireEntry);
            }
            if (patientBuilder != null && adminMeta.PatOper != "USE")
            {
                var patientEntry = patientBuilder.BuildPatientEntry(
                    parsedFile,
                    patientId
                );
                bundleElements.Add(patientEntry);
            }

            var bundle = new XElement(ns + "Bundle", new XAttribute("xmlns", ns), bundleElements);


            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), bundle);
        }

    }
}
