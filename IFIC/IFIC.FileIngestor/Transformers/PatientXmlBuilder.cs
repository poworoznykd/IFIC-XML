/************************************************************************************
* FILE          : PatientXmlBuilder.cs
* PROJECT       : IFIC-XML
* PROGRAMMER    : Darryl Poworoznyk
* FIRST VERSION : 2025-08-02
* DESCRIPTION   :
*   Builds FHIR-compliant XML for Patient resources wrapped in a Bundle.
************************************************************************************/

using System;
using System.Reflection;
using System.Xml.Linq;
using IFIC.FileIngestor.Models;

namespace IFIC.FileIngestor.Transformers
{
    /// <summary>
    /// Generates FHIR XML for Patient inside a Bundle.
    /// </summary>
    public class PatientXmlBuilder
    {
        private static readonly XNamespace ns = "http://hl7.org/fhir";

        public XElement BuildPatientBundleHeader(
            ParsedFlatFile parsedFile,
            string bundleId,
            string patientId)
        {
            // Generate unique IDs for resources
            bundleId = string.IsNullOrEmpty(bundleId) ? Guid.NewGuid().ToString() : bundleId;
            patientId = string.IsNullOrEmpty(patientId) ? Guid.NewGuid().ToString() : patientId;

            // Create Bundle document
            var bundle = new XElement(ns + "Bundle", new XAttribute("xmlns", ns),
                new XElement(ns + "id", new XAttribute("value", bundleId)),
                new XElement(ns + "type", new XAttribute("value", "transaction")),
                BuildPatientEntry(parsedFile, patientId)
            );

            return bundle;
        }

        /// <summary>
        /// Builds a Patient entry element for the FHIR Bundle using parsed flat file data.
        /// </summary>
        /// <param name="parsedFile"></param>
        /// <returns></returns>
        public XElement BuildPatientEntry(
            ParsedFlatFile parsedFile,
            string patientId)
        {
            // Extract patient values from flat file
            parsedFile.Patient.TryGetValue("A5A", out var healthCardNumber);
            parsedFile.Patient.TryGetValue("A5B", out var province);
            parsedFile.Patient.TryGetValue("A5C", out var caseId);
            parsedFile.Patient.TryGetValue("A2A", out var gender);
            parsedFile.Patient.TryGetValue("A3", out var birthDate);
            parsedFile.Patient.TryGetValue("A4", out var maritalStatus);
            parsedFile.Patient.TryGetValue("B4", out var language);
            parsedFile.Patient.TryGetValue("B6", out var postalCode);
            parsedFile.Patient.TryGetValue("OrgID", out var orgId);


            XElement result = new XElement(ns + "entry",
                    new XElement(ns + "fullUrl", new XAttribute("value", $"urn:uuid:{patientId}")),
                    new XElement(ns + "resource",
                        new XElement(ns + "Patient",
                        new XAttribute("xmlns", ns),
                            new XElement(ns + "id", new XAttribute("value", patientId)),
                            new XElement(ns + "meta",
                                new XElement(ns + "profile",
                                    new XAttribute("value", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-patient")
                                )
                            ),

            // Identifier - Sex at birth
                            !string.IsNullOrWhiteSpace(gender)
                                ? new XElement(ns + "extension", new XAttribute("url", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-ext-birth-sex"),
                                    new XElement(ns + "valueCode", new XAttribute("value", gender))
                                  )
                                : null,

                            // Identifier - Health Card Number (if exists)
                            !string.IsNullOrWhiteSpace(healthCardNumber) && healthCardNumber != "unknown"
                                ? new XElement(ns + "identifier",
                                    new XElement(ns + "type",
                                        new XElement(ns + "coding",
                                            new XElement(ns + "system", new XAttribute("value", "http://hl7.org/fhir/v2/0203")),
                                            new XElement(ns + "code", new XAttribute("value", "JHN"))
                                        )
                                    ),
                                    new XElement(ns + "system", new XAttribute("value", "https://fhir.infoway-inforoute.ca/NamingSystem/ca-on-patient-hcn")),
                                    new XElement(ns + "value", new XAttribute("value", healthCardNumber))
                                )
                                : null,

                            // Identifier - case record number
                            !string.IsNullOrWhiteSpace(caseId)
                                ? new XElement(ns + "identifier",
                                    new XElement(ns + "type",
                                        new XElement(ns + "coding",
                                            new XElement(ns + "system", new XAttribute("value", "http://hl7.org/fhir/v2/0203")),
                                            new XElement(ns + "code", new XAttribute("value", "MR"))
                                        )
                                    ),
                                    new XElement(ns + "system", new XAttribute("value", "http://acme.vendor.com/facility-cm")),/*could be http://acme.vendor.com/facility-x */
                                    new XElement(ns + "value", new XAttribute("value", caseId))
                                )
                                : null,

                            // BirthDate
                            !string.IsNullOrWhiteSpace(birthDate)
                                ? new XElement(ns + "birthDate", new XAttribute("value", birthDate))
                                : null,

                            // Address (PostalCode)
                            !string.IsNullOrWhiteSpace(postalCode)
                                ? new XElement(ns + "address",
                                    new XElement(ns + "use", new XAttribute("value", "home")),
                                    new XElement(ns + "postalCode", new XAttribute("value", postalCode))
                                  )
                                : null,
                            //Marital status
                            !string.IsNullOrWhiteSpace(maritalStatus)
                                ? new XElement(ns + "maritalStatus",
                                    new XElement(ns + "coding",
                                    new XElement(ns + "code", new XAttribute("value", maritalStatus))
                                  ))
                                : null,

                            // Communication - Primary Language
                            !string.IsNullOrWhiteSpace(language)
                                ? new XElement(ns + "communication",
                                    new XElement(ns + "language",
                                        new XElement(ns + "coding",
                                            new XElement(ns + "code", new XAttribute("value", language))
                                        )
                                    )
                                )
                                : null,

                            // Facility/Agency identifier
                            !string.IsNullOrWhiteSpace(orgId)
                            ? new XElement(ns + "managingOrganization",
                                new XElement(ns + "identifier",
                                    new XElement(ns + "system", new XAttribute("value", "http://cihi.ca/fhir/NamingSystem/cihi-submission-identifier")),
                                    new XElement(ns + "value", new XAttribute("value", orgId))
                                )
                              ) : null
                        )
                    ),
                    new XElement(ns + "request",
                        new XElement(ns + "method", new XAttribute("value", "POST")),
                        new XElement(ns + "url", new XAttribute("value", $"urn:uuid:{patientId}"))
                    )
                );

            return result;
        }

        /// <summary>
        /// Builds a FHIR Bundle containing a Patient resource using parsed flat file data.
        /// </summary>
        /// <param name="parsedFile">The parsed flat file containing patient data.</param>
        /// <returns>XDocument representing the FHIR Bundle XML.</returns>
        public XDocument BuildPatientBundle(ParsedFlatFile parsedFile)
        {
            if (parsedFile == null)
            {
                throw new ArgumentNullException(nameof(parsedFile), "Parsed flat file cannot be null.");
            }

            XElement bundle = BuildPatientBundleHeader(
                parsedFile,
                null,
                null);
            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), bundle);
        }
    }
}
