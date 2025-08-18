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
        public string StartDate { get; set; }
        private List<XElement> CreateICodeA7Elements(ParsedFlatFile parsedFile)
        {
            var coverageCodes = new[] { "iA7a", "iA7b", "iA7c", "iA7d", "iA7e", "iA7f", "iA7g", "iA7h", "iA7i", "iA7j", "iA7k" };
            var coverageContainedElements = new List<XElement>();

            // Determine if assessment type contains "return"
            string axType = parsedFile.Admin.TryGetValue("axType", out var axTypeValue) ? axTypeValue : string.Empty;
            bool isReturnAssessment = axType.IndexOf("return", StringComparison.OrdinalIgnoreCase) >= 0;

            // Choose appropriate field for start date
            string dateFieldKey = isReturnAssessment ? "A12" : "B2";
            StartDate = parsedFile.Encounter.TryGetValue(dateFieldKey, out var rawDate) && !string.IsNullOrWhiteSpace(rawDate)
                ? rawDate
                : null; // no fallback; omit if missing

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
        /// <param name="parsedFile"></param>
        /// <param name="patientId"></param>
        /// <param name="encounterId"></param>
        /// <returns></returns>
        public XElement BuildEncounterEntry(
            ParsedFlatFile parsedFile,
            string patientId,
            string encounterId)
        {
            var coverageCodes = new[] { "iA7a", "iA7b", "iA7c", "iA7d", "iA7e", "iA7f", "iA7g", "iA7h", "iA7i", "iA7j", "iA7k" };

            parsedFile.Encounter.TryGetValue("B5A", out var admittedFrom);
            parsedFile.Encounter.TryGetValue("B2", out var stayStartDate);
            parsedFile.Encounter.TryGetValue("R1", out var stayEndDate);
            parsedFile.Encounter.TryGetValue("B5B", out var admittedFromFacilityNumber);
            parsedFile.Encounter.TryGetValue("OrgID", out var orgId);
            parsedFile.Encounter.TryGetValue("R2", out var livingStatus);
            parsedFile.Encounter.TryGetValue("R4", out var dischargedToFacilityNumber);

            // Create Bundle document
            var result = new XElement(ns + "entry",
                new XElement(ns + "fullUrl", new XAttribute("value", $"urn:uuid:{encounterId}")),
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

                        // contained - discharged to
                        !string.IsNullOrWhiteSpace(orgId) &&
                        !string.IsNullOrWhiteSpace(livingStatus) &&
                        !string.IsNullOrWhiteSpace(dischargedToFacilityNumber)
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
                                                new XAttribute("value", livingStatus)
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
//// contained - program type 1
//!string.IsNullOrWhiteSpace(patientId) &&
//!string.IsNullOrWhiteSpace(stayStartDate) &&
//!string.IsNullOrWhiteSpace(stayEndDate)
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
//                    new XAttribute("value", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-encounter")
//                )
//            ),
//            new XElement(ns + "type",
//                new XElement(ns + "coding",
//                    new XElement(ns + "code",
//                        new XAttribute("value", "PRT123") 
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
//                    new XAttribute("value", stayStartDate)
//                ),
//                new XElement(ns + "end",
//                    new XAttribute("value", stayEndDate)
//                )
//            ),
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
//    )
//    : null,

// contained - ?
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

//// contained - Bed Type
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

                            ////planned
                            //new XElement(ns + "status",
                            //    new XAttribute("value", "planned")
                            //),


            #endregion
           
                        new XElement(ns + "status", new XAttribute("value", "planned")),
                        // contained - Patient ID
                        !string.IsNullOrWhiteSpace(patientId)
                            ? BuildSubject(parsedFile,patientId)
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
            #region Commented Code 3
                        (coverageCodes.Any(code => parsedFile.Encounter.ContainsKey(code) && !string.IsNullOrWhiteSpace(parsedFile.Encounter[code]))
                            ?new XElement(ns + "account",
                                new XElement(ns + "reference",
                                    new XAttribute("value", "#paymentSource")
                                )
                            ) : null),

                           //Hospitalization
                           !string.IsNullOrWhiteSpace(admittedFrom) ||
                           !string.IsNullOrWhiteSpace(dischargedToFacilityNumber)
                            ?new XElement(ns + "hospitalization",
                                !string.IsNullOrWhiteSpace(admittedFrom)
                                ?new XElement(ns + "origin",
                                    new XElement(ns + "reference",
                                        new XAttribute("value", "#admittedFrom")
                                    )
                                ) : null,
                                //new XElement(ns + "reAdmission",
                                //    new XElement(ns + "coding",
                                //        new XAttribute("code", 1)//TODO - hard coded? (1 = Yes, 2 = No, 3 = Unknown?)
                                //    )
                                //),
                                !string.IsNullOrWhiteSpace(dischargedToFacilityNumber)
                                ?new XElement(ns + "destination",
                                        new XAttribute("value", "#dischargedTo")
                                ) : null
                            )
                            : null,

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
                        // Faciltiy/agnecy identifier this encounter is related to
                        !string.IsNullOrWhiteSpace(orgId)
                        ? new XElement(ns + "serviceProvider",
                            new XElement(ns + "identifier",
                                new XElement(ns + "system", new XAttribute("value", "http://cihi.ca/fhir/NamingSystem/cihi-submission-identifier")),
                                new XElement(ns + "value", new XAttribute("value", orgId))
                            )
                            ) : null
                    )
                ),
                BuildSubEntryPoint(parsedFile, encounterId)
            );

            return result;
        }

        private XElement BuildSubject(
            ParsedFlatFile parsedFile,
            string patientId)
        {
            parsedFile.Admin.TryGetValue("patOper", out var patOper);
            if (patOper == "USE")
            {
                return new XElement(ns + "subject",
                   new XElement(ns + "reference",
                       new XAttribute("value", $"Patient/{patientId}")
                   )
                );
            }
            else
            {
                return new XElement(ns + "subject",
                    new XElement(ns + "reference",
                        new XAttribute("value", $"urn:uuid:{patientId}")
                    )
                );
            }

        }

        private XElement BuildSubEntryPoint(
            ParsedFlatFile parsedFile,
            string encounterId)
        {
            parsedFile.Admin.TryGetValue("encOper", out var encOper);
            if(encOper == "UPDATE")
            {
                return new XElement(ns + "request",
                    new XElement(ns + "method", new XAttribute("value", "POST")),
                    new XElement(ns + "url", new XAttribute("value", $"/encounter/{encounterId}/$update"))
                );
            }
            else
            {
                return new XElement(ns + "request",
                    new XElement(ns + "method", new XAttribute("value", "POST")),
                    new XElement(ns + "url", new XAttribute("value", $"urn:uuid:{encounterId}"))
                );
            }
        }

        /// <summary>
        /// Builds the header for an Encounter Bundle.
        /// </summary>
        /// <param name="parsedFile"></param>
        /// <returns></returns>
        public XElement BuildEncounterBundleHeader(
            ParsedFlatFile parsedFile,
            string bundleId,
            string patientId,
            string encounterId)
        {
            // Generate unique IDs for resources
            bundleId = string.IsNullOrEmpty(bundleId) ? Guid.NewGuid().ToString() : bundleId;
            patientId = string.IsNullOrEmpty(patientId) ? Guid.NewGuid().ToString() : patientId;
            encounterId = string.IsNullOrEmpty(encounterId) ? Guid.NewGuid().ToString() : encounterId;

            // Create Bundle document
            var bundle = new XElement(ns + "Bundle", new XAttribute("xmlns", ns),
                new XElement(ns + "id", new XAttribute("value", bundleId)),
                new XElement(ns + "type", new XAttribute("value", "transaction")),
                BuildEncounterEntry(parsedFile, patientId, encounterId)
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
            parsedFile.Admin.TryGetValue("fhirPatID", out var fhirPatID);
            parsedFile.Admin.TryGetValue("fhirEncID", out var fhirEncID);
            XElement bundle = BuildEncounterBundleHeader(
                parsedFile,
                null,
                fhirPatID,
                fhirEncID);

            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), bundle);
        }
    }
}
