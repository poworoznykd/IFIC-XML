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

            // Generate unique IDs for resources
            string bundleId = Guid.NewGuid().ToString();
            string encounterId = Guid.NewGuid().ToString();
            string patientId = Guid.NewGuid().ToString();

            // Extract encounter values from flat file
              
            parsedFile.Encounter.TryGetValue("B5A", out var admittedFrom);
            parsedFile.Encounter.TryGetValue("B2", out var stayStartDate);
            parsedFile.Encounter.TryGetValue("R1", out var stayEndDate);
            parsedFile.Encounter.TryGetValue("B5B", out var admittedFromFacilityNumber);
            parsedFile.Encounter.TryGetValue("OrgID", out var orgId);
            parsedFile.Encounter.TryGetValue("R2", out var livingStatus);
            parsedFile.Encounter.TryGetValue("R4", out var dischargedToFacilityNumber);

            // Create Bundle document
            var bundle = new XElement(ns + "Bundle", new XAttribute("xmlns", ns),
                new XElement(ns + "id", new XAttribute("value", bundleId)),
                new XElement(ns + "type", new XAttribute("value", "transaction")),

                new XElement(ns + "entry",
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

                            // contained - admitted from
                            !string.IsNullOrWhiteSpace(admittedFrom)
                                ? new XElement(ns + "contained",
                                    new XElement(ns + "Location",
                                        new XElement(ns + "id",
                                            new XAttribute("value", admittedFrom)
                                        ),
                                        new XElement(ns + "meta",
                                            new XElement(ns + "profile",
                                                new XAttribute("value", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-location-admission")
                                            )
                                        ),
                                        new XElement(ns + "type",
                                            new XElement(ns + "coding",
                                                new XElement(ns + "code",
                                                    new XAttribute("value", "PTRES")//TODO - hardcoded? (PTRES = Program Type Residence?)
                                                )
                                            )
                                        ),
                                        new XElement(ns + "managingOrganization",
                                            new XElement(ns + "identifier",
                                                new XElement(ns + "value", new XAttribute("value", orgId))
                                            )
                                        )
                                    )
                                )
                                : null,

                            // contained - discharged to
                            !string.IsNullOrWhiteSpace(orgId)
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
                                                    new XAttribute("value", "SEMIND")//TODO - hardcoded?
                                                )
                                            )
                                        ),
                                        new XElement(ns + "managingOrganization",
                                            new XElement(ns + "identifier",
                                                new XElement(ns + "value", new XAttribute("value", orgId))
                                            )
                                        )
                                    )
                                )
                                : null,

                            // contained - program type 1
                            !string.IsNullOrWhiteSpace(patientId) &&
                            !string.IsNullOrWhiteSpace(stayStartDate) &&
                            !string.IsNullOrWhiteSpace(stayEndDate)
                                ? new XElement(ns + "contained",
                                    new XElement(ns + "Encounter",
                                        new XElement(ns + "id",
                                            new XAttribute("value", "programType1")
                                        ),
                                        new XElement(ns + "meta",
                                            new XElement(ns + "profile",
                                                new XAttribute("value", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-encounter")
                                            )
                                        ),
                                        new XElement(ns + "status",
                                            new XElement(ns + "profile",
                                                new XAttribute("value", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-encounter")
                                            )
                                        ),
                                        new XElement(ns + "type",
                                            new XElement(ns + "coding",
                                                new XElement(ns + "code",
                                                    new XAttribute("value", "PRT123") // TODO - hardcoded?
                                                )
                                            )
                                        ),
                                        new XElement(ns + "subject",
                                            new XElement(ns + "reference",
                                                new XAttribute("value", $"Patient/{patientId}")
                                            )
                                        ),
                                        new XElement(ns + "period",
                                            new XElement(ns + "start",
                                                new XAttribute("value", stayStartDate)
                                            ),
                                            new XElement(ns + "end",
                                                new XAttribute("value", stayEndDate)
                                            )
                                        ),
                                        new XElement(ns + "serviceProvider",
                                            new XElement(ns + "identifier",
                                                new XElement(ns + "system",
                                                    new XAttribute("value", "http://cihi.ca/fhir/NamingSystem/on-ministry-of-health-and-long-term-care-submission-identifier")
                                                ),
                                                new XElement(ns + "value",
                                                    new XAttribute("value", orgId)
                                                )
                                            )
                                        )
                                    )
                                )
                                : null,

                            // contained - ?
                            !string.IsNullOrWhiteSpace(admittedFrom)//TODO - hardcoded? (admittedFrom = Program Type 1?)
                                ? new XElement(ns + "contained",
                                    new XElement(ns + "Location",
                                        new XElement(ns + "id",
                                            new XAttribute("value", "misCode")
                                        ),
                                        new XElement(ns + "meta",
                                            new XElement(ns + "profile",
                                                new XAttribute("value", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-location-mis-code")
                                            )
                                        ),
                                        new XElement(ns + "type",
                                            new XElement(ns + "coding",
                                                new XElement(ns + "code",
                                                    new XAttribute("value", admittedFromFacilityNumber) 
                                                )
                                            )
                                        ),
                                            new XElement(ns + "physicalType",
                                            new XElement(ns + "coding",
                                                new XElement(ns + "code",
                                                    new XAttribute("value", "wa")//TODO - hard coded?
                                                )
                                            )
                                        )
                                    )
                                )
                                : null,

                            // contained - Bed Type
                            !string.IsNullOrWhiteSpace(admittedFrom)//TODO - hardcoded? (admittedFrom = Program Type 1?)
                                ? new XElement(ns + "contained",
                                    new XElement(ns + "Location",
                                        new XElement(ns + "id",
                                            new XAttribute("value", "bedType")
                                        ),
                                        new XElement(ns + "meta",
                                            new XElement(ns + "profile",
                                                new XAttribute("value", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-location-bed-type")
                                            )
                                        ),
                                        new XElement(ns + "type",
                                            new XElement(ns + "coding",
                                                new XElement(ns + "code",
                                                    new XAttribute("value", "CCCF")//TODO- hardcoded? (CCCF = Complex Continuing Care Facility
                                                )
                                            )
                                        ),
                                            new XElement(ns + "physicalType",
                                            new XElement(ns + "coding",
                                                new XElement(ns + "code",
                                                    new XAttribute("value", "bd")//TODO - hard coded? (bd = Bed Type)
                                                )
                                            )
                                        )
                                    )
                                )
                                : null,

                            // contained - Payment Source
                            !string.IsNullOrWhiteSpace(admittedFrom)//TODO - hardcoded? (admittedFrom = Program Type 1?)
                                ? new XElement(ns + "contained",
                                    new XElement(ns + "Account",
                                        new XElement(ns + "id",
                                            new XAttribute("value", "paymentSource")
                                        ),
                                        new XElement(ns + "meta",
                                            new XElement(ns + "profile",
                                                new XAttribute("value", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-account")
                                            )
                                        ),
                                        new XElement(ns + "type",
                                            new XElement(ns + "coding",
                                                new XElement(ns + "code",
                                                    new XAttribute("value", "PBILLACCT")// TODO - hard coded? (PBILLACCT = Provincial Billing Account)
                                                )
                                            )
                                        ),
                                        new XElement(ns + "coverage",
                                            new XElement(ns + "coverage",
                                                new XElement(ns + "reference",
                                                    new XAttribute("value", "#coverage-iA7a")// TODO - hard coded? (coverage-iA7a = Insurance Account 7a?)
                                                )
                                            )
                                        ),
                                            new XElement(ns + "coverage",
                                            new XElement(ns + "coverage",
                                                new XElement(ns + "reference",
                                                    new XAttribute("value", "#coverage-iA7f")// TODO - hard coded? (coverage-iA7f = Insurance Account 7f?)
                                                )
                                            )
                                        )
                                    )
                                )
                                : null,

                            // contained - coverage-iA7a
                            !string.IsNullOrWhiteSpace(admittedFrom)//TODO - hardcoded? (admittedFrom = Program Type 1?)
                                ? new XElement(ns + "contained",
                                    new XElement(ns + "Coverage",
                                        new XElement(ns + "id",
                                            new XAttribute("value", "coverage-iA7a")
                                        ),
                                        new XElement(ns + "meta",
                                            new XElement(ns + "profile",
                                                new XAttribute("value", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-coverage")
                                            )
                                        ),
                                        new XElement(ns + "type",
                                            new XElement(ns + "coding",
                                                new XElement(ns + "code",
                                                    new XAttribute("value", "INPUBLICPOL")// TODO - hard coded? (INPUBLICPOL = Insurance Public Policy)
                                                )
                                            )
                                        ),
                                        new XElement(ns + "period",
                                            new XElement(ns + "start",
                                                new XAttribute("value", "2017-01-01")// TODO - hard coded? (start date for coverage?)
                                            )
                                        )
                                    )
                                )
                                : null,

                            // contained - coverage-iA7f
                            !string.IsNullOrWhiteSpace(admittedFrom)//TODO - hardcoded? (admittedFrom = Program Type 1?)
                                ? new XElement(ns + "contained",
                                    new XElement(ns + "Coverage",
                                        new XElement(ns + "id",
                                            new XAttribute("value", "coverage-iA7f")
                                        ),
                                        new XElement(ns + "meta",
                                            new XElement(ns + "profile",
                                                new XAttribute("value", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-coverage")
                                            )
                                        ),
                                        new XElement(ns + "type",
                                            new XElement(ns + "coding",
                                                new XElement(ns + "code",
                                                    new XAttribute("value", "pay")// TODO - hard coded? (pay = Private Insurance?)
                                                )
                                            )
                                        ),
                                        new XElement(ns + "period",
                                            new XElement(ns + "start",
                                                new XAttribute("value", "2017-01-01")// TODO - hard coded? (start date for coverage?)
                                            )
                                        )
                                    )
                                )
                                : null,

                            // contained - Reference to the contained program type
                            !string.IsNullOrWhiteSpace(admittedFrom)//TODO - hardcoded? (admittedFrom = Program Type 1?)
                                ? new XElement(ns + "extension",
                                    new XAttribute("url", "http://cihi.ca/fhir/irrs/StructureDefinition/irrs-ext-includes"),
                                    new XElement(ns + "valueReference",
                                        new XElement(ns + "reference",
                                            new XAttribute("value", "#programType1")//TODO - hard coded? (programType1 = Program Type 1?
                                        )
                                    )
                                )
                                : null,

                            //planned
                            new XElement(ns + "status",
                                new XAttribute("value", "planned")
                            ),
                            // contained - Patient ID
                            !string.IsNullOrWhiteSpace(patientId)
                                ? new XElement(ns + "subject",
                                    new XElement(ns + "reference",
                                        new XAttribute("value", $"Patient/{patientId}")
                                    )
                                )
                                : null,

                            // period
                            !string.IsNullOrWhiteSpace(stayStartDate) || !string.IsNullOrWhiteSpace(stayEndDate)
                                ? new XElement(ns + "period",
                                    new XElement(ns + "start",
                                        new XAttribute("value", stayStartDate)
                                    ),
                                    new XElement(ns + "end",
                                        new XAttribute("value", stayEndDate)
                                    )
                                )
                                : null,

                            !string.IsNullOrWhiteSpace(stayStartDate)//TODO - hardcoded? (stayStartDate = Start Date of Stay?)
                                ? new XElement(ns + "Account",
                                    new XElement(ns + "id",
                                        new XAttribute("value", "#paymentSource")
                                    )
                                )
                                : null,

                                //Hospitalization
                                !string.IsNullOrWhiteSpace(admittedFrom)
                                ? new XElement(ns + "hospitalization",
                                    new XElement(ns + "origin",
                                        new XElement(ns + "reference",
                                            new XAttribute("value", "#admittedFrom")
                                        )
                                    ),
                                    new XElement(ns + "reAdmission",
                                        new XElement(ns + "coding",
                                            new XAttribute("code", 1)//TODO - hard coded? (1 = Yes, 2 = No, 3 = Unknown?)
                                        )
                                    ),
                                    new XElement(ns + "destination",
                                            new XAttribute("value", "#dischargedTo")
                                    )
                                )
                                : null,

                                //MIS Function Centre
                                !string.IsNullOrWhiteSpace(stayStartDate) && !string.IsNullOrWhiteSpace(stayEndDate)
                                ? new XElement(ns + "location",
                                    new XElement(ns + "location",
                                        new XElement(ns + "reference",
                                            new XAttribute("value", "#misCode")
                                        )
                                    ),
                                    new XElement(ns + "period",
                                        new XElement(ns + "start",
                                            new XAttribute("value", stayStartDate)
                                        ),
                                        new XElement(ns + "end",
                                            new XAttribute("value", stayEndDate)
                                        )
                                    )
                                )
                                : null,

                                //Bed Type
                                !string.IsNullOrWhiteSpace(stayStartDate) && !string.IsNullOrWhiteSpace(stayEndDate)
                                ? new XElement(ns + "location",
                                    new XElement(ns + "location",
                                        new XElement(ns + "reference",
                                            new XAttribute("value", "#bedType")
                                        )
                                    ),
                                    new XElement(ns + "period",
                                        new XElement(ns + "start",
                                            new XAttribute("value", stayStartDate)
                                        ),
                                        new XElement(ns + "end",
                                            new XAttribute("value", stayEndDate)
                                        )
                                    )
                                )
                                : null,

                            // Faciltiy/agnecy identifier this encounter is related to
                            new XElement(ns + "serviceProvider",
                                new XElement(ns + "identifier",
                                    new XElement(ns + "system", new XAttribute("value", "http://cihi.ca/fhir/NamingSystem/on-ministry-of-health-and-long-term-care-submission-identifier")),
                                    new XElement(ns + "value", new XAttribute("value", orgId))
                                )
                            )
                        )
                    ),
                    new XElement(ns + "request",
                        new XElement(ns + "method", new XAttribute("value", "POST")),
                        new XElement(ns + "url", new XAttribute("value", "Encounter"))
                    )
                )
            );

            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), bundle);
        }
    }
}
