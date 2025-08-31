using System;
using System.Xml.Linq;
using IFIC.FileIngestor.Models;

namespace IFIC.FileIngestor.Transformers
{
    /// <summary>
    /// Generates FHIR XML for Patient inside a Bundle.
    /// Uses strongly typed <see cref="AdminMetadata"/> for routing and context.
    /// </summary>
    public class PatientXmlBuilder
    {
        private static readonly XNamespace ns = "http://hl7.org/fhir";

        /// <summary>
        /// Strongly typed admin data for routing and IDs.
        /// </summary>
        public AdminMetadata AdminData { get; set; }

        /// <summary>
        /// Constructor using the <see cref="AdminMetadata"/> instance.
        /// </summary>
        /// <param name="data">Admin metadata containing IDs and routing info.</param>
        public PatientXmlBuilder(AdminMetadata data)
        {
            AdminData = data;
        }

        /// <summary>
        /// Builds the header for a Patient Bundle.
        /// Always uses <see cref="AdminData.FhirPatID"/> if provided, otherwise falls back to a GUID.
        /// </summary>
        public XElement BuildPatientBundleHeader(
            ParsedFlatFile parsedFile,
            string bundleId,
            string patientId,
            string patOper)
        {
            // Generate unique IDs for resources
            bundleId = string.IsNullOrEmpty(bundleId) ? Guid.NewGuid().ToString() : bundleId;
            patientId = !string.IsNullOrWhiteSpace(AdminData.FhirPatID)
                ? AdminData.FhirPatID
                : Guid.NewGuid().ToString();

            // Build bundle with patient entry
            var bundle = new XElement(ns + "Bundle", new XAttribute("xmlns", ns),
                new XElement(ns + "id", new XAttribute("value", bundleId)),
                new XElement(ns + "type", new XAttribute("value", "transaction")),
                BuildPatientEntry(parsedFile, patientId, patOper)
            );

            return bundle;
        }

        /// <summary>
        /// Builds a Patient entry element for the FHIR Bundle using parsed flat file data.
        /// Uses AdminData for identifiers and operations; demographic details come from <see cref="ParsedFlatFile.Patient"/>.
        /// </summary>
        public XElement BuildPatientEntry(
            ParsedFlatFile parsedFile,
            string patientId,
            string patOper)
        {

            string fullUrlEntry = "Patient/";   //SEANNIE
            if (patOper.CompareTo("CREATE") == 0) fullUrlEntry = "urn:uuid:";


            // Extract patient values from [PATIENT] section
            parsedFile.Patient.TryGetValue("A5A", out var healthCardNumber);
            parsedFile.Patient.TryGetValue("A5B", out var province);
            parsedFile.Patient.TryGetValue("A5C", out var caseId);
            parsedFile.Patient.TryGetValue("A2A", out var sexAtBirth);
            parsedFile.Patient.TryGetValue("A3", out var birthDate);
            parsedFile.Patient.TryGetValue("A4", out var maritalStatus);
            parsedFile.Patient.TryGetValue("B4", out var language);
            parsedFile.Patient.TryGetValue("B6", out var postalCode);
            parsedFile.Patient.TryGetValue("OrgID", out var orgId);

            // SEANNIE
            // extra logic to handle the case of unknown/not-applicable HCN
            string hcnExtension = "unknown";
            if (healthCardNumber == "2") hcnExtension = "not-applicable";

            // SEANNIE
            // generate the Prov/Territory Issuing source URL 
            //   - default to ON
            string provIssuing = "https://fhir.infoway-inforoute.ca/NamingSystem/ca-on-patient-hcn";
            if((province!="0") && (province!="1") && (province!="ON"))
            {
                // 0=unknown, 1=not-applicable and we defaulted to ON already
                provIssuing = "https://fhir.infoway-inforoute.ca/NamingSystem/ca-"+province.ToLower()+ "-patient-healthcare-id";
            }
            // Build Patient resource
            var patientResource = new XElement(ns + "Patient",
                new XAttribute("xmlns", ns),
                new XElement(ns + "id", new XAttribute("value", patientId)),
                new XElement(ns + "meta",
                    new XElement(ns + "profile",
                        new XAttribute("value", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-patient")
                    )
                ),

                // Extension - Sex at birth
                !string.IsNullOrWhiteSpace(sexAtBirth)
                    ? new XElement(ns + "extension", new XAttribute("url", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-ext-birth-sex"),
                        new XElement(ns + "valueCode", new XAttribute("value", sexAtBirth)))
                    : null,

                // Identifier - Health Card Number
                //  SEANNIE - needed to add logic to catch the override logic for
                //            HCN=1 (unknown) and 2 (not-applicable)
                !string.IsNullOrWhiteSpace(healthCardNumber) && ((healthCardNumber != "1") && (healthCardNumber != "2"))
                    ? new XElement(ns + "identifier",
                        new XElement(ns + "type",
                            new XElement(ns + "coding",
                                new XElement(ns + "system", new XAttribute("value", "http://hl7.org/fhir/v2/0203")),
                                new XElement(ns + "code", new XAttribute("value", "JHN"))
                            )
                        ),
                        new XElement(ns + "system", new XAttribute("value", provIssuing)),
                        new XElement(ns + "value", new XAttribute("value", healthCardNumber))
                    )
                    // assume that if we get here that HCN = 1 or 2 and it is not NULL :)
                    : 
                    
                    new XElement(ns + "identifier",
                        new XElement(ns + "extension", new XAttribute("url", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-ext-data-absent-reason"),
                        new XElement(ns + "valueCode", new XAttribute("value", hcnExtension))),

                        new XElement(ns + "type",
                            new XElement(ns + "coding",
                                new XElement(ns + "system", new XAttribute("value", "http://hl7.org/fhir/v2/0203")),
                                new XElement(ns + "code", new XAttribute("value", "JHN"))
                            )
                        )
                    ),

                // Identifier - Case record number
                !string.IsNullOrWhiteSpace(caseId)
                    ? new XElement(ns + "identifier",
                        new XElement(ns + "type",
                            new XElement(ns + "coding",
                                new XElement(ns + "system", new XAttribute("value", "http://hl7.org/fhir/v2/0203")),
                                new XElement(ns + "code", new XAttribute("value", "MR"))
                            )
                        ),
                        new XElement(ns + "system", new XAttribute("value", "http://acme.vendor.com/facility-cm")),
                        new XElement(ns + "value", new XAttribute("value", caseId))
                    )
                    : null,

                // BirthDate
                !string.IsNullOrWhiteSpace(birthDate)
                    ? new XElement(ns + "birthDate", new XAttribute("value", birthDate))
                    : null,

                // Address (Postal Code only)
                !string.IsNullOrWhiteSpace(postalCode)
                    ? new XElement(ns + "address",
                        new XElement(ns + "use", new XAttribute("value", "home")),
                        new XElement(ns + "postalCode", new XAttribute("value", postalCode))
                    )
                    : null,

                // Marital Status
                !string.IsNullOrWhiteSpace(maritalStatus)
                    ? new XElement(ns + "maritalStatus",
                        new XElement(ns + "coding",
                            new XElement(ns + "code", new XAttribute("value", maritalStatus))
                        )
                    )
                    : null,

                // Primary Language
                !string.IsNullOrWhiteSpace(language)
                    ? new XElement(ns + "communication",
                        new XElement(ns + "language",
                            new XElement(ns + "coding",
                                new XElement(ns + "code", new XAttribute("value", language))
                            )
                        )
                    )
                    : null,

                // Managing Organization (OrgID)
                !string.IsNullOrWhiteSpace(orgId)
                    ? new XElement(ns + "managingOrganization",
                        new XElement(ns + "identifier",
                            new XElement(ns + "system", new XAttribute("value", "http://cihi.ca/fhir/NamingSystem/cihi-submission-identifier")),
                            new XElement(ns + "value", new XAttribute("value", orgId))
                        )
                    )
                    : null
            );

            // Wrap full entry (resource + request)
            var result = new XElement(ns + "entry",
                new XElement(ns + "fullUrl", new XAttribute("value", $"{fullUrlEntry}{patientId}")),
                new XElement(ns + "resource", patientResource),
                BuildEntryPoint(patientId)   // NEW: delegate to BuildEntryPoint
            );

            return result;
        }

        /// <summary>
        /// Builds the Patient request entry point for the FHIR bundle.
        /// Uses AdminData.PatOper to decide which API action to generate.
        /// </summary>
        private XElement BuildEntryPoint(string patientId)
        {
            if (string.IsNullOrWhiteSpace(AdminData.PatOper))
            {
                throw new InvalidOperationException("Patient operation (patOper) not specified in ADMIN section.");
            }

            switch (AdminData.PatOper.Trim().ToUpperInvariant())
            {
                case "CREATE":
                    return new XElement(ns + "request",
                        new XElement(ns + "method", new XAttribute("value", "POST")),
                        new XElement(ns + "url", new XAttribute("value", "/Patient"))
                    );

                case "CORRECTION":
                    return new XElement(ns + "request",
                        new XElement(ns + "method", new XAttribute("value", "PUT")),
//                        new XElement(ns + "url", new XAttribute("value", $"/Patient/{patientId}"))
                        new XElement(ns + "url", new XAttribute("value", $"/Patient"))
                    );

                case "UPDATE":
                    return new XElement(ns + "request",
                        new XElement(ns + "method", new XAttribute("value", "POST")),
//                        new XElement(ns + "url", new XAttribute("value", $"/Patient/{patientId}/$update"))
                        new XElement(ns + "url", new XAttribute("value", $"/Patient/$update"))
                    );

                    // SEANNIE
                    // the DELETE method @CIHI requires the {patientID} as part of the entrypoint
                    // unlike the UPDATE/CORRECTION method above -- I wish CIHI learned how to consistently
                    // and correctly document their API
                case "DELETE":
                    return new XElement(ns + "request",
                        new XElement(ns + "method", new XAttribute("value", "DELETE")),
                        new XElement(ns + "url", new XAttribute("value", $"/Patient/{patientId}"))
                        //new XElement(ns + "url", new XAttribute("value", $"/Patient"))
                    );

                case "USE":
                    // No REST call; just reference the Patient UUID in the bundle
                    return null;

                default:
                    throw new InvalidOperationException($"Unknown patient operation: {AdminData.PatOper}");
            }
        }

        /// <summary>
        /// Builds a complete Patient Bundle.
        /// Uses AdminData for identifiers and ensures IDs are never null.
        /// </summary>
        public XDocument BuildPatientBundle(ParsedFlatFile parsedFile)
        {
            if (parsedFile == null)
            {
                throw new ArgumentNullException(nameof(parsedFile), "Parsed flat file cannot be null.");
            }

            // SEANNIE
            // - add the passing of the patOper into the subsequent methods in order to properly
            //   create the correct syntax in the "fullurl" for the entry
            string patID = AdminData.FhirPatID;
            string patOper = AdminData.PatOper;

            // Build header (patientId always comes from AdminData or fallback GUID)
            XElement bundle = BuildPatientBundleHeader(
                parsedFile,
                null,
                patID,
                patOper);

            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), bundle);
        }
    }
}
