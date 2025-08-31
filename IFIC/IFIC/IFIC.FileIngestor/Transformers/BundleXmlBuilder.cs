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

            // SEANNIE
            // need to use the ASMId if it is present instead of always generating one
            string questionnaireResponseId = string.IsNullOrEmpty(adminMeta.FhirAsmID) ? Guid.NewGuid().ToString() : adminMeta.FhirAsmID;
            //string questionnaireResponseId = Guid.NewGuid().ToString();

            // Create Bundle document
            var bundleElements = new List<object>
            {
                new XElement(ns + "id", new XAttribute("value", bundleId)),
                new XElement(ns + "type", new XAttribute("value", "transaction"))
            };

            // SEANNIE
            // 
            // Question to Darryl 
            //   - why does your code build the patient bundle, the encounter bundle and the questionnaire
            //     bundle all separately *before* it calls "BuildFullBundle()" here where it
            //     *once again* builds them all from scratch???
            //



            // SEANNIE 
            // need special bundle building logic here
            // 1. in the case of deleting assessments - need to delete all assessments *before* the
            //    corresponding encounter (if the encounter is also being deleted)
            //    NOTE: the if statement below *does NOT* output the patient bundle
            //
//            if ((encounterXmlBuilder != null && adminMeta.EncOper == "DELETE") && (questionnaireResponseBuilder != null && adminMeta.AsmOper == "DELETE"))
//            {
//                if (questionnaireResponseBuilder != null)
//                {
//                    var questionnaireEntry = questionnaireResponseBuilder.BuildQuestionnaireResponseEntry(
//                        parsedFile,
//                        patientId,
//                        encounterId,
//                        questionnaireResponseId, adminMeta.AsmOper
//                    );
//                    bundleElements.Add(questionnaireEntry);
//                }
//                var encounterEntry = encounterXmlBuilder.BuildEncounterEntry(
//                    parsedFile,
//                    patientId,
//                    encounterId, adminMeta.EncOper
//                );
//                bundleElements.Add(encounterEntry);
//            }
//            else
//            {
                if (encounterXmlBuilder != null && adminMeta.EncOper != "USE")
                {
                    // SEANNIE
                    // - need to know if this is a return assessment or not so we can add the 
                    //   "reAdmission" tag to the <hospitalization> element in the encounter entry
                    //   if need be ... fuck me!  Why did CIHI make this soooo complicated?!?!?
                    //
                    bool isReturnAssessment = false;
                    if (adminMeta.AsmType.Contains("return", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        isReturnAssessment = true;
                    }

                var encounterEntry = encounterXmlBuilder.BuildEncounterEntry(
                        parsedFile,
                        patientId,
                        encounterId, adminMeta.EncOper, isReturnAssessment
                    );
                    bundleElements.Add(encounterEntry);
                }
                if (questionnaireResponseBuilder != null)
                {
                    var questionnaireEntry = questionnaireResponseBuilder.BuildQuestionnaireResponseEntry(
                        parsedFile,
                        patientId,
                        encounterId,
                        questionnaireResponseId, adminMeta.AsmOper
                    );
                    bundleElements.Add(questionnaireEntry);
                }
                if (patientBuilder != null && adminMeta.PatOper != "USE")
                {
                    var patientEntry = patientBuilder.BuildPatientEntry(
                        parsedFile,
                        patientId,
                        adminMeta.PatOper
                    );
                    bundleElements.Add(patientEntry);
                }
 //           }
            var bundle = new XElement(ns + "Bundle", new XAttribute("xmlns", ns), bundleElements);


            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), bundle);
        }

    }
}
