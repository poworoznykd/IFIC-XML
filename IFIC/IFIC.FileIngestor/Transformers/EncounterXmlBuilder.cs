using IFIC.FileIngestor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace IFIC.FileIngestor.Transformers
{
    public class EncounterXmlBuilder
    {
        private static readonly XNamespace ns = "http://hl7.org/fhir";
        public string? StartDate { get; set; }
        public AdminMetadata AdminData { get; set; }

        public EncounterXmlBuilder(AdminMetadata data)
        {
            AdminData = data;
        }

        /// <summary>
        /// Creates the I code elements.
        /// </summary>
        /// <param name="parsedFile">The parsed flat file</param>
        /// <returns>returns a list of elements</returns>
        private List<XElement> CreateICodeA7Elements(ParsedFlatFile parsedFile)
        {
            var coverageCodes = new[] { "iA7a", "iA7b", "iA7j", "iA7d", "iA7i", "iA7k", "iA7e", "iA7l", "iA7f", "iA7n", "iA7m" };
            var coverageContainedElements = new List<XElement>();

            // Default StartDate to null
            StartDate = null;

            // Safely pick the field key
            string? asmType = AdminData?.AsmType;
            string dateFieldKey = !string.IsNullOrEmpty(asmType) &&
                                  asmType.Contains("return", StringComparison.OrdinalIgnoreCase)
                ? "A12"
                : "B2";

            // Ensure parsedFile and Encounter dictionary exist before lookup
            if (parsedFile != null && parsedFile.Encounter != null)
            {
                if (parsedFile.Encounter.TryGetValue(dateFieldKey, out var rawDate) &&
                    !string.IsNullOrWhiteSpace(rawDate))
                {
                    StartDate = rawDate;
                }
            }

            foreach (var code in coverageCodes)
            {
                if (parsedFile.Encounter.TryGetValue(code, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    // Assign type code - customize as needed per code
                    string typeCode = code == "iA7a" ? "INPUBLICPOL" : "pay";

                    coverageContainedElements.Add(
                        new XElement(ns + "contained",
                            new XElement(ns + "Coverage",
                                new XElement(ns + "id", new XAttribute("value", $"{code}")),
                                new XElement(ns + "meta",
                                    new XElement(ns + "profile", new XAttribute("value", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-coverage"))
                                ),
                                new XElement(ns + "type",
                                    new XElement(ns + "coding",
                                        new XElement(ns + "code", new XAttribute("value", typeCode))
                                    )
                                ),
                                !string.IsNullOrWhiteSpace(StartDate)
                                ? new XElement(ns + "period",
                                    new XElement(ns + "start", new XAttribute("value", StartDate))
                                  )
                                : null
                            )
                        )
                    );
                }
            }

            return coverageContainedElements;
        }

        /// <summary>
        /// Builds an Encounter entry element for the FHIR Bundle using parsed flat file data.
        /// </summary>
        public XElement BuildEncounterEntry(
            ParsedFlatFile parsedFile,
            string patientId,
            string encounterId, 
            string encOper, 
            bool isReturnAssess,
            string questionnaireId) 
        {
            var coverageCodes = new[] { "iA7a", "iA7b", "iA7j", "iA7d", "iA7i", "iA7k", "iA7e", "iA7l", "iA7f", "iA7n", "iA7m" };

            string fullUrlEntry = "Encounter/";  
            if (encOper.CompareTo("CREATE") == 0) 
                    fullUrlEntry = "urn:uuid:";

            parsedFile.Encounter.TryGetValue("B5A", out var admittedFrom);
            parsedFile.Encounter.TryGetValue("B2", out var stayStartDate);
            parsedFile.Encounter.TryGetValue("R1", out var stayEndDate);
            parsedFile.Encounter.TryGetValue("B5B", out var admittedFromFacilityNumber);
            parsedFile.Encounter.TryGetValue("OrgID", out var orgId);
            parsedFile.Encounter.TryGetValue("R2", out var dischargedTo);
            parsedFile.Encounter.TryGetValue("R4", out var dischargedToFacilityNumber);



            // Create Bundle document
            var result = new XElement(ns + "entry",
                new XElement(ns + "fullUrl", new XAttribute("value", $"{fullUrlEntry}{encounterId}")),
                new XElement(ns + "resource",
                    new XElement(ns + "Encounter",
                    new XAttribute("xmlns", ns),
                        new XElement(ns + "id", new XAttribute("value", encounterId)),
                        new XElement(ns + "meta",
                            new XElement(ns + "profile",
                                new XAttribute("value", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-encounter")
                            )
                        ),
                        // contained - Payment Source
                        // Include Account only if at least one coverage code has a value
                        (coverageCodes.Any(code => parsedFile.Encounter.ContainsKey(code) && !string.IsNullOrWhiteSpace(parsedFile.Encounter[code]))
                            ? new XElement(ns + "contained",
                                new XElement(ns + "Account",
                                    new XElement(ns + "id", new XAttribute("value", "paymentSource")),
                                    new XElement(ns + "meta",
                                        new XElement(ns + "profile", new XAttribute("value", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-account"))
                                    ),
                                    new XElement(ns + "type",
                                        new XElement(ns + "coding",
                                            new XElement(ns + "code", new XAttribute("value", "PBILLACCT"))
                                        )
                                    ),
                                    coverageCodes
                                        .Where(code => parsedFile.Encounter.ContainsKey(code) && !string.IsNullOrWhiteSpace(parsedFile.Encounter[code]))
                                        .Select(code => new XElement(ns + "coverage",
                                            new XElement(ns + "coverage",
                                                new XElement(ns + "reference", new XAttribute("value", $"#{code}"))
                                            )
                                        ))
                                )
                            )
                            : null),
                          CreateICodeA7Elements(parsedFile),

                        // contained - admitted from
                        !string.IsNullOrWhiteSpace(admittedFrom)
                        ? new XElement(ns + "contained",
                            new XElement(ns + "Location",
                                new XElement(ns + "id",
                                    new XAttribute("value", "admittedFrom")
                                ),
                                new XElement(ns + "meta",
                                    new XElement(ns + "profile",
                                        new XAttribute("value", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-location-admission")
                                    )
                                ),
                                new XElement(ns + "type",
                                    new XElement(ns + "coding",
                                        new XElement(ns + "code",
                                            new XAttribute("value", admittedFrom)
                                        )
                                    )
                                ),
                                !string.IsNullOrWhiteSpace(admittedFromFacilityNumber)
                                ? new XElement(ns + "managingOrganization",
                                    new XElement(ns + "identifier",
                                        new XElement(ns + "value", new XAttribute("value", admittedFromFacilityNumber))
                                    )
                                ) : null
                            )
                        )
                        : null,
                        !string.IsNullOrWhiteSpace(orgId) &&
                        !string.IsNullOrWhiteSpace(dischargedTo) &&
                        (dischargedTo != "exp")
                            ? new XElement(ns + "contained",
                                new XElement(ns + "Location",
                                    new XElement(ns + "id",
                                        new XAttribute("value", "dischargedTo")
                                    ),

                                    new XElement(ns + "meta",
                                        new XElement(ns + "profile",
                                            new XAttribute("value", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-location-discharge")
                                        )
                                    ),
                                    new XElement(ns + "type",
                                        new XElement(ns + "coding",
                                            new XElement(ns + "code",
                                                new XAttribute("value", dischargedTo)
                                            )
                                        )
                                    ),
                                    !string.IsNullOrWhiteSpace(dischargedToFacilityNumber)
                                    ? new XElement(ns + "managingOrganization",
                                        new XElement(ns + "identifier",
                                            new XElement(ns + "value", new XAttribute("value", dischargedToFacilityNumber))
                                        )
                                    ) : null
                                )
                            )
                            : null,
            #region Commented Code
// SEANNIE - Test Case 1 - uncomment only 2 "//" for program type
// contained - program type 1
//!string.IsNullOrWhiteSpace(patientId) //&&
////!string.IsNullOrWhiteSpace(stayStartDate) 
////!string.IsNullOrWhiteSpace(stayEndDate)   // SEANNIE don't need the stayEndDate to have a program
//    ? new XElement(ns + "contained",
//        new XElement(ns + "Encounter",
//            new XElement(ns + "id",
//                new XAttribute("value", "programType1")
//            ),
//            new XElement(ns + "meta",
//                new XElement(ns + "profile",
//                    new XAttribute("value", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-encounter")
//                )
//            ),
//            new XElement(ns + "status",
//                new XElement(ns + "profile",
//                    new XAttribute("value", "planned")     //SEANNIE - I believe this can be hardcoded as "planned"
//                )
//            ),
//            new XElement(ns + "type",
//                new XElement(ns + "coding",
//                    new XElement(ns + "code",
//                        new XAttribute("value", "PRG02")  // SEANNIE - should be read from parameters really
//                    )
//                )
//            ),
//            new XElement(ns + "subject",
//                new XElement(ns + "reference",
//                    new XAttribute("value", $"Patient/{patientId}")
//                )
//            ),
//            new XElement(ns + "period",
//                new XElement(ns + "start",
//                    new XAttribute("value", "2019-08-01")      // could be the variable stayStartDate - but the test has it set to 2019-08-01
//                ),
////                new XElement(ns + "end",
////                    new XAttribute("value", stayEndDate)     // SEANNIE make this population conditional on stayEndDate not being null/blank
////                )
////            ),
//            new XElement(ns + "serviceProvider",
//                new XElement(ns + "identifier",
//                    new XElement(ns + "system",
//                        new XAttribute("value", "http://cihi.ca/fhir/NamingSystem/on-ministry-of-health-and-long-term-care-submission-identifier")
//                    ),
//                    new XElement(ns + "value",
//                        new XAttribute("value", orgId)
//                    )
//                )
//            )
//        )
//    ) )  // SEANNIE - had to add another ")"
//    : null,

// Leave this MIS Functional Center stuff commented out for now 
//!string.IsNullOrWhiteSpace(admittedFrom)
//    ? new XElement(ns + "contained",
//        new XElement(ns + "Location",
//            new XElement(ns + "id",
//                new XAttribute("value", "misCode")
//            ),
//            new XElement(ns + "meta",
//                new XElement(ns + "profile",
//                    new XAttribute("value", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-location-mis-code")
//                )
//            ),
//            new XElement(ns + "type",
//                new XElement(ns + "coding",
//                    new XElement(ns + "code",
//                        new XAttribute("value", admittedFromFacilityNumber) 
//                    )
//                )
//            ),
//                new XElement(ns + "physicalType",
//                new XElement(ns + "coding",
//                    new XElement(ns + "code",
//                        new XAttribute("value", "wa")
//                    )
//                )
//            )
//        )
//    )
//    : null,

//// leave this Bed Type stuff commented out for now
//!string.IsNullOrWhiteSpace(admittedFrom)//TODO - hardcoded? (admittedFrom = Program Type 1?)
//    ? new XElement(ns + "contained",
//        new XElement(ns + "Location",
//            new XElement(ns + "id",
//                new XAttribute("value", "bedType")
//            ),
//            new XElement(ns + "meta",
//                new XElement(ns + "profile",
//                    new XAttribute("value", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-location-bed-type")
//                )
//            ),
//            new XElement(ns + "type",
//                new XElement(ns + "coding",
//                    new XElement(ns + "code",
//                        new XAttribute("value", "CCCF")//TODO- hardcoded? (CCCF = Complex Continuing Care Facility
//                    )
//                )
//            ),
//                new XElement(ns + "physicalType",
//                new XElement(ns + "coding",
//                    new XElement(ns + "code",
//                        new XAttribute("value", "bd")//TODO - hard coded? (bd = Bed Type)
//                    )
//                )
//            )
//        )
//    )
//    : null,
            #endregion
                        
            #region Commented Code 2
                            //// contained - Reference to the contained program type

                            //new XElement(ns + "extension",
                            //    new XAttribute("url", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-ext-includes"),
                            //    new XElement(ns + "valueReference",
                            //        new XElement(ns + "reference",
                            //            new XAttribute("value", "#programType1")
                            //        )
                            //    )
                            //),

            #endregion
           
                        new XElement(ns + "status", new XAttribute("value", "planned")),
                        // contained - Patient ID
                        !string.IsNullOrWhiteSpace(patientId)
                            ? BuildSubject(parsedFile,patientId)
                            : null,

                        (coverageCodes.Any(code => parsedFile.Encounter.ContainsKey(code) && !string.IsNullOrWhiteSpace(parsedFile.Encounter[code]))
                            ?new XElement(ns + "account",
                                new XElement(ns + "reference",
                                    new XAttribute("value", "#paymentSource")
                                )
                            ) : null),
                           //Hospitalization
                           !string.IsNullOrWhiteSpace(admittedFrom) ||
                           !string.IsNullOrWhiteSpace(dischargedTo) ||
                           (isReturnAssess)
                            ?new XElement(ns + "hospitalization",
                                !string.IsNullOrWhiteSpace(admittedFrom)
                                ?new XElement(ns + "origin",
                                    new XElement(ns + "reference",
                                        new XAttribute("value", "#admittedFrom")
                                    )
                                ) : null,
                                (isReturnAssess)
                                ?new XElement(ns + "reAdmission",
                                    new XElement(ns + "coding",
                                        new XElement(ns + "code",
                                           new XAttribute("value", "1")
                                        )
                                    )
                                ) : null,
                                !string.IsNullOrWhiteSpace(dischargedTo) &&
                                (dischargedTo != "exp")
                                ?new XElement(ns + "destination",
                                    new XElement(ns + "reference",
                                        new XAttribute("value", "#dischargedTo")
                                    )
                                ) : null,
                                !string.IsNullOrWhiteSpace(dischargedTo) &&
                                (dischargedTo == "exp")
                                ? new XElement(ns + "dischargeDisposition",
                                    new XElement(ns + "coding",
                                        new XElement(ns + "code",
                                           new XAttribute("value", dischargedTo)
                                        )
                                    )
                                ) : null
                            )
                            : null,
                        // period
                        new XElement(ns + "period",
                            !string.IsNullOrWhiteSpace(StartDate)
                            ? new XElement(ns + "start",
                                new XAttribute("value", StartDate)
                            ) : null,
                            !string.IsNullOrWhiteSpace(stayEndDate)
                            ? new XElement(ns + "end",
                                new XAttribute("value", stayEndDate)
                            ) : null
                        ),

                        // SEANNIE - TRANSFER
                        // -- this is the code that "links" the encounter created at Facility A (IRRS30)
                        //    to the encounter just created at Facility B (IRRS29)
                        // 
//                        new XElement(ns + "incomingReferral",
//                            new XElement(ns + "reference",
//                                new XAttribute("value", $"{fullUrlEntry}{questionnaireId}"))
//                            ),
            #region Commented Code 3
                        ////MIS Function Centre
                        //!string.IsNullOrWhiteSpace(stayStartDate) && !string.IsNullOrWhiteSpace(stayEndDate)
                        //? new XElement(ns + "location",
                        //    new XElement(ns + "location",
                        //        new XElement(ns + "reference",
                        //            new XAttribute("value", "#misCode")
                        //        )
                        //    ),
                        //    new XElement(ns + "period",
                        //        new XElement(ns + "start",
                        //            new XAttribute("value", stayStartDate)
                        //        ),
                        //        new XElement(ns + "end",
                        //            new XAttribute("value", stayEndDate)
                        //        )
                        //    )
                        //)
                        //: null,

                        ////Bed Type
                        //!string.IsNullOrWhiteSpace(stayStartDate) && !string.IsNullOrWhiteSpace(stayEndDate)
                        //? new XElement(ns + "location",
                        //    new XElement(ns + "location",
                        //        new XElement(ns + "reference",
                        //            new XAttribute("value", "#bedType")
                        //        )
                        //    ),
                        //    new XElement(ns + "period",
                        //        new XElement(ns + "start",
                        //            new XAttribute("value", stayStartDate)
                        //        ),
                        //        new XElement(ns + "end",
                        //            new XAttribute("value", stayEndDate)
                        //        )
                        //    )
                        //)
                        //: null,
            #endregion
                        // Faciltiy/agency identifier this encounter is related to
                        !string.IsNullOrWhiteSpace(orgId)
                        ? new XElement(ns + "serviceProvider",
                            new XElement(ns + "identifier",
                                new XElement(ns + "system", new XAttribute("value", "http://cihi.ca/fhir/NamingSystem/cihi-submission-identifier")),
                                new XElement(ns + "value", new XAttribute("value", orgId))
                            )
                            ) : null
                    )
                ),
                BuildEntryPoint(parsedFile, encounterId)
            );
            return result;
        }

        /// <summary>
        /// Builds the subject element
        /// </summary>
        /// <param name="parsedFile">The parsed file</param>
        /// <param name="patientId">The id of the patient to be used.</param>
        /// <returns></returns>
        private XElement BuildSubject(
            ParsedFlatFile parsedFile,
            string patientId)
        {
            if (AdminData.PatOper?.Equals("CREATE", StringComparison.OrdinalIgnoreCase) == true)
            {
                return new XElement(ns + "subject",
                    new XElement(ns + "reference",
                        new XAttribute("value", $"urn:uuid:{patientId}")
                    )
                );
            }
            else
            {
                return new XElement(ns + "subject",
                    new XElement(ns + "reference",
                        new XAttribute("value", $"Patient/{patientId}")
                    )
                );
            }
        }

        /// <summary>
        /// Builds the Encounter request entry point for the FHIR bundle.
        /// Uses AdminData.EncOper to decide which API action to generate.
        /// </summary>
        /// <param name="parsedFile">Parsed flat file (not directly used here).</param>
        /// <param name="encounterId">Encounter UUID or placeholder.</param>
        /// <returns>XElement representing the request element, or null for USE.</returns>
        private XElement? BuildEntryPoint(
            ParsedFlatFile parsedFile,
            string encounterId)
        {
            if (string.IsNullOrWhiteSpace(AdminData.EncOper))
            {
                throw new InvalidOperationException("Encounter operation (encOper) not specified in ADMIN section.");
            }

            switch (AdminData.EncOper.Trim().ToUpperInvariant())
            {
                case "CREATE":
                    return new XElement(ns + "request",
                        new XElement(ns + "method", new XAttribute("value", "POST")),
                        new XElement(ns + "url", new XAttribute("value", $"/Encounter/"))
                    );

                case "CORRECTION":
                    return new XElement(ns + "request",
                        new XElement(ns + "method", new XAttribute("value", "PUT")),
                        //new XElement(ns + "url", new XAttribute("value", $"/Encounter/{encounterId}"))
                        new XElement(ns + "url", new XAttribute("value", $"/Encounter"))
                    );

                case "UPDATE":
                    return new XElement(ns + "request",
                        new XElement(ns + "method", new XAttribute("value", "POST")),
                        //new XElement(ns + "url", new XAttribute("value", $"/Encounter/{encounterId}/$update"))
                        new XElement(ns + "url", new XAttribute("value", $"/Encounter/$update"))
                    );
                case "DELETE":
                    return new XElement(ns + "request",
                        new XElement(ns + "method", new XAttribute("value", "DELETE")),
                        new XElement(ns + "url", new XAttribute("value", $"/Encounter/{encounterId}"))
                        //new XElement(ns + "url", new XAttribute("value", $"/Encounter"))
                    );

                case "USE":
                    // No REST call; just reference the Encounter UUID in the bundle
                    return null;

                default:
                    throw new InvalidOperationException($"Unknown encounter operation: {AdminData.EncOper}");
            }
        }

        /// <summary>
        /// Builds the header for an Encounter Bundle.
        /// </summary>
        /// <param name="parsedFile"></param>
        /// <returns></returns>
        public XElement BuildEncounterBundleHeader(
            ParsedFlatFile parsedFile,
            string? bundleId,
            string? patientId,
            string? encounterId, 
            string encOper)
        {
            // Generate unique IDs for resources
            bundleId = string.IsNullOrEmpty(bundleId) ? Guid.NewGuid().ToString() : bundleId;
            patientId = string.IsNullOrEmpty(patientId) ? Guid.NewGuid().ToString() : patientId;
            encounterId = string.IsNullOrEmpty(encounterId) ? Guid.NewGuid().ToString() : encounterId;

            bool isReturnAssessment = false;
            if(AdminData != null &&
                AdminData.IsReturn != null &&
                AdminData.IsReturn.CompareTo("YES") == 0)
            {
                isReturnAssessment = true;
            }

            // Create Bundle document
            var bundle = new XElement(ns + "Bundle", new XAttribute("xmlns", ns),
                new XElement(ns + "id", new XAttribute("value", bundleId)),
                new XElement(ns + "type", new XAttribute("value", "transaction")),
                BuildEncounterEntry(parsedFile, patientId, encounterId, encOper, isReturnAssessment, "")
            );

            return bundle;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parsedFile"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public XDocument BuildEncounterBundle(ParsedFlatFile parsedFile)
        {
            if (parsedFile == null)
            {
                throw new ArgumentNullException(nameof(parsedFile), "Parsed flat file cannot be null.");
            }

            string? patientId = AdminData.FhirPatID;
            string? encounterId = AdminData.FhirEncID;
            string? encOper = AdminData.EncOper;

            XElement bundle = BuildEncounterBundleHeader(
                parsedFile,
                null,
                patientId,
                encounterId,
                encOper);

            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), bundle);
        }
    }
}
