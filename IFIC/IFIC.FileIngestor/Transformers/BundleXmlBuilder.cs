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
            QuestionnaireResponseBuilder questionnaireResponseBuilder)
        {
            if (parsedFile == null)
            {
                throw new ArgumentNullException(nameof(parsedFile), "Parsed flat file cannot be null.");
            }

            // Generate unique IDs for resources
            string bundleId = Guid.NewGuid().ToString();
            string patientId = Guid.NewGuid().ToString();
            string encounterId = Guid.NewGuid().ToString();
            string questionnaireResponseId = Guid.NewGuid().ToString();


            // Create Bundle document
            var bundle = new XElement(ns + "Bundle", new XAttribute("xmlns", ns),
                new XElement(ns + "id", new XAttribute("value", bundleId)),
                new XElement(ns + "type", new XAttribute("value", "transaction")),
                encounterXmlBuilder.BuildEncounterEntry(
                    parsedFile, 
                    patientId,
                    encounterId),
                questionnaireResponseBuilder.BuildQuestionnaireResponseEntry(
                    parsedFile, 
                    patientId,
                    encounterId,
                    questionnaireResponseId),
                patientBuilder.BuildPatientEntry(
                    parsedFile, 
                    patientId)
            );

            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), bundle);
        }

    }
}
