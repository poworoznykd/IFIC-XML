using IFIC.FileIngestor.Models;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Crypto;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace IFIC.FileIngestor.Transformers
{
    public class QuestionnaireResponseBuilder
    {
        private static readonly XNamespace ns = "http://hl7.org/fhir";

        /// <summary>
        /// Builds a FHIR Bundle containing a Patient resource using parsed flat file data.
        /// </summary>
        /// <param name="parsedFile">The parsed flat file containing patient data.</param>
        /// <returns>XDocument representing the FHIR Bundle XML.</returns>
        public XDocument BuildQuestionnaireResponsBundle(ParsedFlatFile parsedFile)
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

            // Extract patient values from flat file
            #region Section A
            parsedFile.Patient.TryGetValue("A2b", out var genderIdentity);
            parsedFile.Patient.TryGetValue("A8", out var assessmentReason);
            parsedFile.Patient.TryGetValue("A9", out var assessmentRefDate);
            parsedFile.Patient.TryGetValue("A10", out var primaryCareGoal);
            parsedFile.Patient.TryGetValue("A10a", out var expressedCareGoal);
            parsedFile.Patient.TryGetValue("A11", out var timeSinceLastVisit);
            #endregion
            #region Section B
            parsedFile.Patient.TryGetValue("B1", out var levelOfControl);
            parsedFile.Patient.TryGetValue("B3a", out var indigenousIdentityFirstNations);
            parsedFile.Patient.TryGetValue("B3b", out var indigenousIdentityMetis);
            parsedFile.Patient.TryGetValue("B3c", out var indigenousIdentityInuit);
            parsedFile.Patient.TryGetValue("B5c", out var residentialStatus);
            parsedFile.Patient.TryGetValue("B7", out var priorLivingArrangement);
            parsedFile.Patient.TryGetValue("B8a", out var residentialHistoryA);
            parsedFile.Patient.TryGetValue("B8b", out var residentialHistoryB);
            parsedFile.Patient.TryGetValue("B8c", out var residentialHistoryC);
            parsedFile.Patient.TryGetValue("B8d", out var residentialHistoryD);
            parsedFile.Patient.TryGetValue("B8e", out var residentialHistoryE);
            parsedFile.Patient.TryGetValue("B8f", out var residentialHistoryF);
            parsedFile.Patient.TryGetValue("B9", out var historyMentalIllness);
            parsedFile.Patient.TryGetValue("B10", out var interpreterNeeded);
            #endregion
            #region Section C
            parsedFile.Patient.TryGetValue("C1", out var dailyDecisionMaking);
            parsedFile.Patient.TryGetValue("C2a", out var shortTermMemory);
            parsedFile.Patient.TryGetValue("C2b", out var longTermMemory);
            parsedFile.Patient.TryGetValue("C2c", out var proceduralMemory);
            parsedFile.Patient.TryGetValue("C2d", out var SituationalMemory);
            parsedFile.Patient.TryGetValue("C3a", out var easilyDistracted);
            parsedFile.Patient.TryGetValue("C3b", out var disorganizedSpeech);
            parsedFile.Patient.TryGetValue("C3c", out var varyingMentalFunction);
            parsedFile.Patient.TryGetValue("C4", out var changeInMentalStatus);
            parsedFile.Patient.TryGetValue("C5", out var changeInDecisionMaking);
            #endregion
            #region Section D
            parsedFile.Patient.TryGetValue("D1", out var makingSelfUnderstood);
            parsedFile.Patient.TryGetValue("D2", out var abilityToUnderstandOthers);
            parsedFile.Patient.TryGetValue("D3a", out var hearing);
            parsedFile.Patient.TryGetValue("D3b", out var hearingAidUsed);
            parsedFile.Patient.TryGetValue("D4a", out var visionAdequateLight);
            parsedFile.Patient.TryGetValue("D4b", out var visionApplianceUsed);

            #endregion
            #region Section E
            parsedFile.Patient.TryGetValue("E1a", out var negativeStatements);
            parsedFile.Patient.TryGetValue("E1b", out var persistentAngerWithSelfOrOthers);
            parsedFile.Patient.TryGetValue("E1c", out var unrealisticFears);
            parsedFile.Patient.TryGetValue("E1d", out var repetitiveHealthComplaints);
            parsedFile.Patient.TryGetValue("E1e", out var repetitiveAnxiousComplaints);
            parsedFile.Patient.TryGetValue("E1f", out var sadPainedOrWorriedFacialExpressions);
            parsedFile.Patient.TryGetValue("E1g", out var cryingTearfulness);
            parsedFile.Patient.TryGetValue("E1h", out var recurrentStatementsTerribleAboutToHappen);
            parsedFile.Patient.TryGetValue("E1i", out var withdrawalFromActivitiesOfInterest);
            parsedFile.Patient.TryGetValue("E1j", out var reducedSocialInteractions);
            parsedFile.Patient.TryGetValue("E1k", out var lackOfPleasureExpressions);
            parsedFile.Patient.TryGetValue("E2a", out var selfReportLittleInterest);
            parsedFile.Patient.TryGetValue("E2b", out var selfReportAnxiousRestlessUneasy);
            parsedFile.Patient.TryGetValue("E2c", out var selfReportSadDepressedHopeless);
            parsedFile.Patient.TryGetValue("E3a", out var wandering);
            parsedFile.Patient.TryGetValue("E3b", out var verbalAbuse);
            parsedFile.Patient.TryGetValue("E3c", out var physicalAbuse);
            parsedFile.Patient.TryGetValue("E3d", out var sociallyInappropriate);
            parsedFile.Patient.TryGetValue("E3e", out var inappropriateSexualBehaviour);
            parsedFile.Patient.TryGetValue("E3f", out var resistsCare);


            #endregion
            #region Section F
            parsedFile.Patient.TryGetValue("F1a", out var socialParticipationLongStandingActivities);
            parsedFile.Patient.TryGetValue("F1b", out var socialVisitLongStandingRelation);
            parsedFile.Patient.TryGetValue("F1c", out var socialOtherInteractionLongStandingRelation);

            parsedFile.Patient.TryGetValue("F2a", out var atEaseInteractingWithOthers);
            parsedFile.Patient.TryGetValue("F2b", out var atEaseDoingPlannedActivities);
            parsedFile.Patient.TryGetValue("F2c", out var acceptsInvitations);
            parsedFile.Patient.TryGetValue("F2d", out var pursuesInvolvementActivities);
            parsedFile.Patient.TryGetValue("F2e", out var initiatesInteractionsWithOthers);
            parsedFile.Patient.TryGetValue("F2f", out var reactsPositivelyToInteractions);
            parsedFile.Patient.TryGetValue("F2g", out var adjustsEasilyToChangeInRoutine);

            parsedFile.Patient.TryGetValue("F3a", out var conflictWithOtherCareRecipients);
            parsedFile.Patient.TryGetValue("F3b", out var conflictWithStaff);
            parsedFile.Patient.TryGetValue("F3c", out var staffFrustration);
            parsedFile.Patient.TryGetValue("F3d", out var familyOrFriendsOverwhelmed);
            parsedFile.Patient.TryGetValue("F3e", out var lonely);

            parsedFile.Patient.TryGetValue("F4", out var majorLifeStressors);

            parsedFile.Patient.TryGetValue("F5a", out var consistentPositiveOutlook);
            parsedFile.Patient.TryGetValue("F5b", out var findsMeaningInDayToDayLife);
            parsedFile.Patient.TryGetValue("F5c", out var strongAndSupportiveRelationship);

            #endregion
            #region Section G
            parsedFile.Patient.TryGetValue("G1a", out var adlBathingPerformance);
            parsedFile.Patient.TryGetValue("G1b", out var adlPersonalHygienePerformance);
            parsedFile.Patient.TryGetValue("G1c", out var adlDressingUpperBodyPerformance);
            parsedFile.Patient.TryGetValue("G1d", out var adlDressingLowerBodyPerformance);
            parsedFile.Patient.TryGetValue("G1e", out var adlWalkingPerformance);
            parsedFile.Patient.TryGetValue("G1f", out var adlLocomotionPerformance);
            parsedFile.Patient.TryGetValue("G1g", out var adlTransferToiletPerformance);
            parsedFile.Patient.TryGetValue("G1h", out var adlToiletUsePerformance);
            parsedFile.Patient.TryGetValue("G1i", out var adlBedMobilityPerformance);
            parsedFile.Patient.TryGetValue("G1j", out var adlEatingPerformance);

            parsedFile.Patient.TryGetValue("G2a", out var primaryModeOfLocomotion);
            parsedFile.Patient.TryGetValue("G2b", out var timedWalk);
            parsedFile.Patient.TryGetValue("G2c", out var distanceWalked);
            parsedFile.Patient.TryGetValue("G2d", out var distanceWheeledSelf);

            parsedFile.Patient.TryGetValue("G3a", out var totalHoursExerciseOrPhysicalActivity);
            parsedFile.Patient.TryGetValue("G3b", out var activityLevelDaysOutOfHouse);

            parsedFile.Patient.TryGetValue("G4a", out var personBelievesCanImprove);
            parsedFile.Patient.TryGetValue("G4b", out var careProfessionalBelievesCanImprove);

            parsedFile.Patient.TryGetValue("G5", out var changeInAdlStatus);

            #endregion
            #region Section H
            parsedFile.Patient.TryGetValue("H1", out var bladderContinence);
            parsedFile.Patient.TryGetValue("H2", out var urinaryCollectionDevice);
            parsedFile.Patient.TryGetValue("H3", out var bowelContinence);
            parsedFile.Patient.TryGetValue("H4", out var ostomy);

            #endregion
            #region Section I
            // Section I variables
            parsedFile.Patient.TryGetValue("I1a", out var hipFracture);
            parsedFile.Patient.TryGetValue("I1b", out var otherFracture);
            parsedFile.Patient.TryGetValue("I1c", out var alzheimers);
            parsedFile.Patient.TryGetValue("I1d", out var otherDementia);
            parsedFile.Patient.TryGetValue("I1e", out var hemiplegia);
            parsedFile.Patient.TryGetValue("I1f", out var multipleSclerosis);
            parsedFile.Patient.TryGetValue("I1g", out var paraplegia);
            parsedFile.Patient.TryGetValue("I1h", out var parkinsons);
            parsedFile.Patient.TryGetValue("I1i", out var quadriplegia);
            parsedFile.Patient.TryGetValue("I1j", out var strokeCva);
            parsedFile.Patient.TryGetValue("I1k", out var coronaryHeartDisease);
            parsedFile.Patient.TryGetValue("I1m", out var congestiveHeartFailure);
            parsedFile.Patient.TryGetValue("I1l", out var copd);
            parsedFile.Patient.TryGetValue("I1n", out var anxiety);
            parsedFile.Patient.TryGetValue("I1o", out var bipolar);
            parsedFile.Patient.TryGetValue("I1p", out var depression);
            parsedFile.Patient.TryGetValue("I1q", out var schizophrenia);
            parsedFile.Patient.TryGetValue("I1r", out var pneumonia);
            parsedFile.Patient.TryGetValue("I1s", out var urinaryTractInfection);
            parsedFile.Patient.TryGetValue("I1t", out var cancer);
            parsedFile.Patient.TryGetValue("I1u", out var diabetesMellitus);

            parsedFile.Patient.TryGetValue("I2aa", out var diseaseCode);
            parsedFile.Patient.TryGetValue("I2ab", out var diseaseDiagnosisICD10);

            #endregion
            #region Section J
            parsedFile.Patient.TryGetValue("J1a", out var fallsLast30Days);
            parsedFile.Patient.TryGetValue("J1b", out var falls31To90DaysAgo);
            parsedFile.Patient.TryGetValue("J1c", out var falls91To180DaysAgo);

            parsedFile.Patient.TryGetValue("J2a", out var difficultyStanding);
            parsedFile.Patient.TryGetValue("J2b", out var difficultyTurningAround);
            parsedFile.Patient.TryGetValue("J2c", out var dizziness);
            parsedFile.Patient.TryGetValue("J2d", out var unsteadyGait);
            parsedFile.Patient.TryGetValue("J2e", out var chestPain);
            parsedFile.Patient.TryGetValue("J2f", out var difficultyClearingAirway);
            parsedFile.Patient.TryGetValue("J2g", out var abnormalThoughtProcess);
            parsedFile.Patient.TryGetValue("J2h", out var delusions);
            parsedFile.Patient.TryGetValue("J2i", out var hallucinations);
            parsedFile.Patient.TryGetValue("J2j", out var aphasia);
            parsedFile.Patient.TryGetValue("J2k", out var acidReflux);
            parsedFile.Patient.TryGetValue("J2l", out var constipation);
            parsedFile.Patient.TryGetValue("J2m", out var diarrhea);
            parsedFile.Patient.TryGetValue("J2n", out var vomiting);
            parsedFile.Patient.TryGetValue("J2o", out var difficultyFallingAsleepOrStayingAsleep);
            parsedFile.Patient.TryGetValue("J2p", out var tooMuchSleep);
            parsedFile.Patient.TryGetValue("J2q", out var aspiration);
            parsedFile.Patient.TryGetValue("J2r", out var fever);
            parsedFile.Patient.TryGetValue("J2s", out var giOrGuBleeding);
            parsedFile.Patient.TryGetValue("J2t", out var poorHygiene);
            parsedFile.Patient.TryGetValue("J2u", out var peripheralEdema);

            parsedFile.Patient.TryGetValue("J3", out var dyspnea);
            parsedFile.Patient.TryGetValue("J4", out var fatigue);

            parsedFile.Patient.TryGetValue("J5a", out var painFrequency);
            parsedFile.Patient.TryGetValue("J5b", out var painIntensity);
            parsedFile.Patient.TryGetValue("J5c", out var painConsistency);
            parsedFile.Patient.TryGetValue("J5d", out var breakthroughPain);
            parsedFile.Patient.TryGetValue("J5e", out var painControl);

            parsedFile.Patient.TryGetValue("J6a", out var conditionsUnstable);
            parsedFile.Patient.TryGetValue("J6b", out var acuteEpisodeOrFlareUp);
            parsedFile.Patient.TryGetValue("J6c", out var endStageDisease);

            parsedFile.Patient.TryGetValue("J7", out var selfReportedHealth);

            parsedFile.Patient.TryGetValue("J8a", out var smokesTobaccoDaily);
            parsedFile.Patient.TryGetValue("J8b", out var alcohol);

            #endregion
            #region Section K
            parsedFile.Patient.TryGetValue("K1a", out var heightCentimetres);
            parsedFile.Patient.TryGetValue("K1b", out var weightKilograms);

            parsedFile.Patient.TryGetValue("K2a", out var weightLoss);
            parsedFile.Patient.TryGetValue("K2b", out var dehydrated);
            parsedFile.Patient.TryGetValue("K2c", out var fluidIntake);
            parsedFile.Patient.TryGetValue("K2d", out var fluidOutputExceedsInput);
            parsedFile.Patient.TryGetValue("K2e", out var decreaseInFoodOrFluid);
            parsedFile.Patient.TryGetValue("K2f", out var ateOneOrFewerMeals);

            parsedFile.Patient.TryGetValue("K3", out var modeOfNutritionalIntake);
            parsedFile.Patient.TryGetValue("K4", out var parenteralIntake);

            parsedFile.Patient.TryGetValue("K5a", out var wearsDenture);
            parsedFile.Patient.TryGetValue("K5b", out var brokenTeeth);
            parsedFile.Patient.TryGetValue("K5c", out var reportsMouthFacialPain);
            parsedFile.Patient.TryGetValue("K5d", out var reportsHavingDryMouth);
            parsedFile.Patient.TryGetValue("K5e", out var reportsDifficultyChewing);
            parsedFile.Patient.TryGetValue("K5f", out var gumInflammation);

            #endregion
            #region Section L
            parsedFile.Patient.TryGetValue("L1", out var mostSeverePressureUlcer);
            parsedFile.Patient.TryGetValue("L2", out var priorPressureUlcer);
            parsedFile.Patient.TryGetValue("L3", out var otherSkinUlcer);
            parsedFile.Patient.TryGetValue("L4", out var majorSkinProblems);
            parsedFile.Patient.TryGetValue("L5", out var skinTearsOrCuts);
            parsedFile.Patient.TryGetValue("L6", out var otherSkinCondition);
            parsedFile.Patient.TryGetValue("L7", out var footProblems);

            #endregion
            #region Section M
            parsedFile.Patient.TryGetValue("M1", out var timeInvolvedInActivities);

            parsedFile.Patient.TryGetValue("M2a", out var cardsGamesOrPuzzles);
            parsedFile.Patient.TryGetValue("M2b", out var computerActivity);
            parsedFile.Patient.TryGetValue("M2c", out var conversingTalkingOnPhone);
            parsedFile.Patient.TryGetValue("M2d", out var craftsOrArts);
            parsedFile.Patient.TryGetValue("M2e", out var dancing);
            parsedFile.Patient.TryGetValue("M2f", out var reminiscingAboutLife);
            parsedFile.Patient.TryGetValue("M2g", out var exerciseOrSports);
            parsedFile.Patient.TryGetValue("M2h", out var gardeningOrPlants);
            parsedFile.Patient.TryGetValue("M2i", out var helpingOthers);
            parsedFile.Patient.TryGetValue("M2j", out var musicOrSinging);
            parsedFile.Patient.TryGetValue("M2k", out var pets);
            parsedFile.Patient.TryGetValue("M2l", out var reading);
            parsedFile.Patient.TryGetValue("M2m", out var spiritualActivities);
            parsedFile.Patient.TryGetValue("M2n", out var tripsOrShopping);
            parsedFile.Patient.TryGetValue("M2o", out var walkingOrWheelingOutdoors);
            parsedFile.Patient.TryGetValue("M2p", out var watchingTvOrListeningToRadio);

            parsedFile.Patient.TryGetValue("M3", out var timeAsleepDuringDay);

            #endregion
            #region Section N
            parsedFile.Patient.TryGetValue("N2", out var drugAllergy);
            parsedFile.Patient.TryGetValue("N3", out var numberOfMedications);
            parsedFile.Patient.TryGetValue("N4", out var numberOfHerbalNutritionalSupplements);
            parsedFile.Patient.TryGetValue("N5", out var recentlyChangedMedications);
            parsedFile.Patient.TryGetValue("N6", out var selfReportNeedForMedicationReview);

            parsedFile.Patient.TryGetValue("N7a", out var antipsychoticLast7Days);
            parsedFile.Patient.TryGetValue("N7b", out var anxiolyticLast7Days);
            parsedFile.Patient.TryGetValue("N7c", out var antidepressantLast7Days);
            parsedFile.Patient.TryGetValue("N7d", out var hypnoticLast7Days);

            parsedFile.Patient.TryGetValue("N8", out var medicationByDailyInjection);
            parsedFile.Patient.TryGetValue("N9", out var cannabisUseTimeSinceUse);
            parsedFile.Patient.TryGetValue("N10", out var medicinalUseOfCannabis);

            #endregion
            #region Section O
            parsedFile.Patient.TryGetValue("O1a", out var bloodPressure);
            parsedFile.Patient.TryGetValue("O1b", out var colonoscopy);
            parsedFile.Patient.TryGetValue("O1c", out var dentalExam);
            parsedFile.Patient.TryGetValue("O1d", out var eyeExam);
            parsedFile.Patient.TryGetValue("O1e", out var hearingExam);
            parsedFile.Patient.TryGetValue("O1f", out var influenzaVaccine);
            parsedFile.Patient.TryGetValue("O1g", out var mammogramOrBreastExam);
            parsedFile.Patient.TryGetValue("O1h", out var pneumovaxVaccine);

            parsedFile.Patient.TryGetValue("O2a", out var chemotherapy);
            parsedFile.Patient.TryGetValue("O2b", out var dialysis);
            parsedFile.Patient.TryGetValue("O2c", out var infectionControlSegregation);
            parsedFile.Patient.TryGetValue("O2d", out var ivMedication);
            parsedFile.Patient.TryGetValue("O2e", out var oxygenTherapy);
            parsedFile.Patient.TryGetValue("O2f", out var radiation);
            parsedFile.Patient.TryGetValue("O2g", out var suctioning);
            parsedFile.Patient.TryGetValue("O2h", out var tracheostomyCare);
            parsedFile.Patient.TryGetValue("O2i", out var transfusion);
            parsedFile.Patient.TryGetValue("O2j", out var ventilator);
            parsedFile.Patient.TryGetValue("O2k", out var woundCare);
            parsedFile.Patient.TryGetValue("O2l", out var scheduledToiletingProgram);
            parsedFile.Patient.TryGetValue("O2m", out var palliativeCare);
            parsedFile.Patient.TryGetValue("O2n", out var turningProgram);

            parsedFile.Patient.TryGetValue("O3ab", out var physicalTherapyDays);
            parsedFile.Patient.TryGetValue("O3ac", out var physicalTherapyMinutes);
            parsedFile.Patient.TryGetValue("O3aa", out var physicalTherapyScheduled);

            parsedFile.Patient.TryGetValue("O3bb", out var occupationalTherapyDays);
            parsedFile.Patient.TryGetValue("O3bc", out var occupationalTherapyMinutes);
            parsedFile.Patient.TryGetValue("O3ba", out var occupationalTherapyScheduled);

            parsedFile.Patient.TryGetValue("O3cb", out var speechLanguageTherapyDays);
            parsedFile.Patient.TryGetValue("O3cc", out var speechLanguageTherapyMinutes);
            parsedFile.Patient.TryGetValue("O3ca", out var speechLanguageTherapyScheduled);

            parsedFile.Patient.TryGetValue("O3db", out var respiratoryTherapyDays);
            parsedFile.Patient.TryGetValue("O3dc", out var respiratoryTherapyMinutes);
            parsedFile.Patient.TryGetValue("O3da", out var respiratoryTherapyScheduled);

            parsedFile.Patient.TryGetValue("O3eb", out var functionalRehabilitationDays);
            parsedFile.Patient.TryGetValue("O3ec", out var functionalRehabilitationMinutes);
            parsedFile.Patient.TryGetValue("O3ea", out var functionalRehabilitationScheduled);

            parsedFile.Patient.TryGetValue("O3fb", out var psychologicalTherapiesDays);
            parsedFile.Patient.TryGetValue("O3fc", out var psychologicalTherapiesMinutes);
            parsedFile.Patient.TryGetValue("O3fa", out var psychologicalTherapiesScheduled);

            parsedFile.Patient.TryGetValue("O3gb", out var recreationTherapyDays);
            parsedFile.Patient.TryGetValue("O3gc", out var recreationTherapyMinutes);
            parsedFile.Patient.TryGetValue("O3ga", out var recreationTherapyScheduled);

            parsedFile.Patient.TryGetValue("O4a", out var inpatientAcuteCareHospitalWithOvernightStay);
            parsedFile.Patient.TryGetValue("O4b", out var emergencyRoomVisit);

            parsedFile.Patient.TryGetValue("O7a", out var fullBedRails);
            parsedFile.Patient.TryGetValue("O7b", out var trunkRestraint);
            parsedFile.Patient.TryGetValue("O7c", out var chairPreventsRising);

            parsedFile.Patient.TryGetValue("O5", out var numberOfDaysPhysicianVisits);
            parsedFile.Patient.TryGetValue("O6", out var numberOfDaysPhysicianOrders);


            #endregion
            #region Section P
            parsedFile.Patient.TryGetValue("P1a", out var decisionMakerPersonalCare);
            parsedFile.Patient.TryGetValue("P1b", out var decisionMakerProperty);

            parsedFile.Patient.TryGetValue("P2a", out var advanceDirectiveDoNotResuscitate);
            parsedFile.Patient.TryGetValue("P2b", out var advanceDirectiveDoNotHospitalize);



            #endregion
            #region Section Q
            parsedFile.Patient.TryGetValue("Q1a", out var preferenceToReturnOrRemainInCommunity);
            parsedFile.Patient.TryGetValue("Q1b", out var supportPersonPositiveAboutDischarge);
            parsedFile.Patient.TryGetValue("Q1c", out var hasHousingAvailableInCommunity);
            parsedFile.Patient.TryGetValue("Q2", out var expectedLengthOfStay);


            #endregion
            #region Section R
            parsedFile.Patient.TryGetValue("R1", out var lastDayOfStay);
            parsedFile.Patient.TryGetValue("R2", out var residentialLivingStatusAfterDischarge);
            parsedFile.Patient.TryGetValue("R4", out var dischargeToFacilityNumber);
            parsedFile.Patient.TryGetValue("R3", out var homeCareServicesScheduledAtDischarge);
            parsedFile.Patient.TryGetValue("R5", out var covid19Status);


            #endregion
            #region Section S
            parsedFile.Patient.TryGetValue("S2", out var assessmentSignedAsComplete);

            #endregion



            parsedFile.Patient.TryGetValue("OrgID", out var orgId);

            // Create Bundle document
            var bundle = new XElement(ns + "Bundle", new XAttribute("xmlns", ns),
                new XElement(ns + "id", new XAttribute("value", bundleId)),
                new XElement(ns + "type", new XAttribute("value", "transaction")),

                new XElement(ns + "entry",
                    new XElement(ns + "fullUrl", new XAttribute("value", $"urn:uuid:{questionnaireResponseId}")),
                    new XElement(ns + "resource",
                        new XElement(ns + "QuestionnaireResponse",
                            new XAttribute("xmlns", ns),
                                new XElement(ns + "id",
                                    new XAttribute("value", questionnaireResponseId)
                                ),

                            new XElement(ns + "questionnaire",
                                new XElement(ns + "reference",
                                    new XAttribute("value", "Questionnaire/irrs-ltcf")
                                )
                            ),
                            //Status
                            //Questionnaire status
                            new XElement(ns + "status",
                                new XAttribute("value", "completed")
                            ),
                            // < !--Patient ID-- >
                            new XElement(ns + "subject",
                                new XElement(ns + "reference",
                                    new XAttribute("value", $"Patient/{patientId}")
                                )
                            ),
                             //< !--Encounter ID-- >
                             new XElement(ns + "context",
                                new XElement(ns + "reference",
                                    new XAttribute("value", $"Encounter/{encounterId}")
                                )
                            ),
                            // Section A
                            new XElement(ns + "item",
                                new XElement(ns + "linkId",
                                    new XAttribute("value", $"A")
                                ),
                                !string.IsNullOrWhiteSpace(genderIdentity)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", $"A2")),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", $"A2b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", genderIdentity))
                                            )
                                        )
                                    )
                                ) : null,
                                !string.IsNullOrWhiteSpace(genderIdentity)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", $"A8")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", assessmentReason))
                                        )
                                    )
                                ) : null,
                                !string.IsNullOrWhiteSpace(assessmentRefDate)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", $"A9")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueDate", new XAttribute("value", assessmentRefDate))
                                    )
                                ) : null,
                                !string.IsNullOrWhiteSpace(primaryCareGoal)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "A10g")),

                                    // Primary Care Goal (always added if primaryCareGoal is not empty)
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "A10")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueString", new XAttribute("value", primaryCareGoal))
                                        )
                                    ),

                                    // Expressed Goal (only added if expressedCareGoal is not empty)
                                    !string.IsNullOrWhiteSpace(expressedCareGoal)
                                        ? new XElement(ns + "item",
                                            new XElement(ns + "linkId", new XAttribute("value", "A10a")),
                                            new XElement(ns + "answer",
                                                new XElement(ns + "valueString", new XAttribute("value", expressedCareGoal))
                                            )
                                        )
                                        : null
                                )
                                : null,
                                !string.IsNullOrWhiteSpace(timeSinceLastVisit)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", $"A11")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", timeSinceLastVisit))
                                        )
                                    )
                                ) : null
                            ),
                            // Section B
                            new XElement(ns + "item",
                                new XElement(ns + "linkId",
                                    new XAttribute("value", $"B")
                                ),
                                !string.IsNullOrWhiteSpace(levelOfControl)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "B1")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", levelOfControl))
                                        )
                                    )
                                )
                                : null,
                                !string.IsNullOrWhiteSpace(indigenousIdentityFirstNations) ||
                                !string.IsNullOrWhiteSpace(indigenousIdentityMetis) ||
                                !string.IsNullOrWhiteSpace(indigenousIdentityInuit)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "B3")),
                                    // First Nations Identity
                                    !string.IsNullOrWhiteSpace(indigenousIdentityFirstNations)
                                        ? new XElement(ns + "item",
                                            new XElement(ns + "linkId", new XAttribute("value", "B3a")),
                                            new XElement(ns + "answer",
                                                new XElement(ns + "valueCoding",
                                                    new XElement(ns + "code", new XAttribute("value", indigenousIdentityFirstNations))
                                                )
                                            )
                                        )
                                        : null,
                                    // Metis Identity
                                    !string.IsNullOrWhiteSpace(indigenousIdentityMetis)
                                        ? new XElement(ns + "item",
                                            new XElement(ns + "linkId", new XAttribute("value", "B3b")),
                                            new XElement(ns + "answer",
                                                new XElement(ns + "valueCoding",
                                                    new XElement(ns + "code", new XAttribute("value", indigenousIdentityMetis))
                                                )
                                            )
                                        )
                                        : null,
                                    // Inuit Identity
                                    !string.IsNullOrWhiteSpace(indigenousIdentityInuit)
                                        ? new XElement(ns + "item",
                                            new XElement(ns + "linkId", new XAttribute("value", "B3c")),
                                            new XElement(ns + "answer",
                                                new XElement(ns + "valueCoding",
                                                    new XElement(ns + "code", new XAttribute("value", indigenousIdentityInuit))
                                                )
                                            )
                                        )
                                        : null
                                    )
                                    : null,
                                !string.IsNullOrWhiteSpace(residentialStatus)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "B5")),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "B5c")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", residentialStatus))
                                            )
                                        )
                                    )
                                )
                                : null,
                                !string.IsNullOrWhiteSpace(priorLivingArrangement)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "B7")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", priorLivingArrangement))
                                        )
                                    )
                                )
                                : null,
                                !string.IsNullOrWhiteSpace(residentialHistoryA) ||
                                !string.IsNullOrWhiteSpace(residentialHistoryB) ||
                                !string.IsNullOrWhiteSpace(residentialHistoryC) ||
                                !string.IsNullOrWhiteSpace(residentialHistoryD) ||
                                !string.IsNullOrWhiteSpace(residentialHistoryE) ||
                                !string.IsNullOrWhiteSpace(residentialHistoryF)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "B8")),
                                    !string.IsNullOrWhiteSpace(residentialHistoryA)
                                    ? new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "B8a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", residentialHistoryA))
                                            )
                                        )
                                    )
                                    : null,
                                    !string.IsNullOrWhiteSpace(residentialHistoryB)
                                    ? new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "B8b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", residentialHistoryB))
                                            )
                                        )
                                    )
                                    : null,
                                    !string.IsNullOrWhiteSpace(residentialHistoryC)
                                    ? new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "B8c")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", residentialHistoryC))
                                            )
                                        )
                                    )
                                    : null,
                                    !string.IsNullOrWhiteSpace(residentialHistoryD)
                                    ? new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "B8d")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", residentialHistoryD))
                                            )
                                        )
                                    )
                                    : null,
                                    !string.IsNullOrWhiteSpace(residentialHistoryE)
                                    ? new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "B8e")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", residentialHistoryE))
                                            )
                                        )
                                    )
                                    : null,
                                    !string.IsNullOrWhiteSpace(residentialHistoryF)
                                    ? new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "B8f")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", residentialHistoryF))
                                            )
                                        )
                                    )
                                    : null
                                )
                                : null,
                                !string.IsNullOrWhiteSpace(historyMentalIllness)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "B9")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", historyMentalIllness))
                                        )
                                    )
                                )
                                : null,
                                !string.IsNullOrWhiteSpace(interpreterNeeded)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "B10")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", interpreterNeeded))
                                        )
                                    )
                                )
                                : null
                            ),
                            // Section C
                            new XElement(ns + "item",
                                new XElement(ns + "linkId",
                                    new XAttribute("value", $"C")
                                ),
                                !string.IsNullOrWhiteSpace(timeSinceLastVisit)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", $"C1")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", timeSinceLastVisit))
                                        )
                                    )
                                ) : null,
                                !string.IsNullOrWhiteSpace(shortTermMemory) ||
                                !string.IsNullOrWhiteSpace(longTermMemory) ||
                                !string.IsNullOrWhiteSpace(proceduralMemory) ||
                                !string.IsNullOrWhiteSpace(SituationalMemory)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "C2")),
                                    !string.IsNullOrWhiteSpace(shortTermMemory)
                                        ? new XElement(ns + "item",
                                            new XElement(ns + "linkId", new XAttribute("value", "C2a")),
                                            new XElement(ns + "answer",
                                                new XElement(ns + "valueCoding",
                                                    new XElement(ns + "code", new XAttribute("value", shortTermMemory))
                                                )
                                            )
                                        )
                                        : null,
                                    !string.IsNullOrWhiteSpace(longTermMemory)
                                        ? new XElement(ns + "item",
                                            new XElement(ns + "linkId", new XAttribute("value", "C2b")),
                                            new XElement(ns + "answer",
                                                new XElement(ns + "valueCoding",
                                                    new XElement(ns + "code", new XAttribute("value", longTermMemory))
                                                )
                                            )
                                        )
                                        : null,
                                    !string.IsNullOrWhiteSpace(proceduralMemory)
                                        ? new XElement(ns + "item",
                                            new XElement(ns + "linkId", new XAttribute("value", "C2c")),
                                            new XElement(ns + "answer",
                                                new XElement(ns + "valueCoding",
                                                    new XElement(ns + "code", new XAttribute("value", proceduralMemory))
                                                )
                                            )
                                        )
                                        : null,
                                    !string.IsNullOrWhiteSpace(SituationalMemory)
                                    ? new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "C2d")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", SituationalMemory))
                                            )
                                        )
                                    )
                                    : null
                                )
                                : null,
                                !string.IsNullOrWhiteSpace(easilyDistracted) ||
                                !string.IsNullOrWhiteSpace(disorganizedSpeech) ||
                                !string.IsNullOrWhiteSpace(varyingMentalFunction)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "C3")),
                                    !string.IsNullOrWhiteSpace(easilyDistracted)
                                        ? new XElement(ns + "item",
                                            new XElement(ns + "linkId", new XAttribute("value", "C3a")),
                                            new XElement(ns + "answer",
                                                new XElement(ns + "valueCoding",
                                                    new XElement(ns + "code", new XAttribute("value", easilyDistracted))
                                                )
                                            )
                                        )
                                        : null,
                                    !string.IsNullOrWhiteSpace(disorganizedSpeech)
                                        ? new XElement(ns + "item",
                                            new XElement(ns + "linkId", new XAttribute("value", "C3b")),
                                            new XElement(ns + "answer",
                                                new XElement(ns + "valueCoding",
                                                    new XElement(ns + "code", new XAttribute("value", disorganizedSpeech))
                                                )
                                            )
                                        )
                                        : null,
                                    !string.IsNullOrWhiteSpace(varyingMentalFunction)
                                        ? new XElement(ns + "item",
                                            new XElement(ns + "linkId", new XAttribute("value", "C3c")),
                                            new XElement(ns + "answer",
                                                new XElement(ns + "valueCoding",
                                                    new XElement(ns + "code", new XAttribute("value", varyingMentalFunction))
                                                )
                                            )
                                        )
                                        : null
                                ) : null,
                                !string.IsNullOrWhiteSpace(changeInMentalStatus)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", $"C4")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", changeInMentalStatus))
                                        )
                                    )
                                ) : null,
                                !string.IsNullOrWhiteSpace(changeInDecisionMaking)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", $"C5")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", changeInDecisionMaking))
                                        )
                                    )
                                ) : null
                            //Section C END
                            ),
                            // Section D
                            new XElement(ns + "item",
                                new XElement(ns + "linkId",
                                    new XAttribute("value", $"D")
                                ),
                                !string.IsNullOrWhiteSpace(makingSelfUnderstood)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", $"D1")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", makingSelfUnderstood))
                                        )
                                    )
                                ) : null,
                                !string.IsNullOrWhiteSpace(abilityToUnderstandOthers)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", $"D2")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", abilityToUnderstandOthers))
                                        )
                                    )
                                ) : null,
                                !string.IsNullOrWhiteSpace(hearing) ||
                                !string.IsNullOrWhiteSpace(hearingAidUsed)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "D3")),
                                    !string.IsNullOrWhiteSpace(hearing)
                                    ? new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "D3a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", hearing))
                                            )
                                        )
                                    )
                                    : null,
                                    !string.IsNullOrWhiteSpace(hearingAidUsed)
                                    ? new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "D3b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", hearingAidUsed))
                                            )
                                        )
                                    ) : null
                                ) : null,
                                !string.IsNullOrWhiteSpace(visionAdequateLight) ||
                                !string.IsNullOrWhiteSpace(visionApplianceUsed)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "D4")),
                                    !string.IsNullOrWhiteSpace(visionAdequateLight)
                                    ? new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "D4a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", visionAdequateLight))
                                            )
                                        )
                                    )
                                    : null,
                                    !string.IsNullOrWhiteSpace(visionApplianceUsed)
                                    ? new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "D4b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", visionApplianceUsed))
                                            )
                                        )
                                    ) : null
                                ) : null
                            //Section D END
                            ),
                            // Section E
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", new XAttribute("value", "E")),
                                !string.IsNullOrWhiteSpace(negativeStatements)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "E1a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", negativeStatements))
                                        )
                                    )
                                ) : null,
                                // E1b - Persistent Anger With Self or Others
                                !string.IsNullOrWhiteSpace(persistentAngerWithSelfOrOthers)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "E1b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", persistentAngerWithSelfOrOthers))
                                        )
                                    )
                                ) : null,
                                // E1c - Unrealistic Fears
                                !string.IsNullOrWhiteSpace(unrealisticFears)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "E1c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", unrealisticFears))
                                        )
                                    )
                                ) : null,
                                // E1d - Repetitive Health Complaints
                                !string.IsNullOrWhiteSpace(repetitiveHealthComplaints)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "E1d")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", repetitiveHealthComplaints))
                                        )
                                    )
                                ) : null,
                                // E1e - Repetitive Anxious Complaints/Concerns
                                !string.IsNullOrWhiteSpace(repetitiveAnxiousComplaints)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "E1e")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", repetitiveAnxiousComplaints))
                                        )
                                    )
                                ) : null,
                                // E1f - Sad, Pained or Worried Facial Expressions
                                !string.IsNullOrWhiteSpace(sadPainedOrWorriedFacialExpressions)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "E1f")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", sadPainedOrWorriedFacialExpressions))
                                        )
                                    )
                                ) : null,
                                // E1g - Crying, Tearfulness
                                !string.IsNullOrWhiteSpace(cryingTearfulness)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "E1g")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", cryingTearfulness))
                                        )
                                    )
                                ) : null,
                                // E1h - Recurrent Statements Something Terrible About to Happen
                                !string.IsNullOrWhiteSpace(recurrentStatementsTerribleAboutToHappen)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "E1h")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", recurrentStatementsTerribleAboutToHappen))
                                        )
                                    )
                                ) : null,
                                // E1i - Withdrawal From Activities of Interest
                                !string.IsNullOrWhiteSpace(withdrawalFromActivitiesOfInterest)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "E1i")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", withdrawalFromActivitiesOfInterest))
                                        )
                                    )
                                ) : null,
                                // E1j - Reduced Social Interactions
                                !string.IsNullOrWhiteSpace(reducedSocialInteractions)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "E1j")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", reducedSocialInteractions))
                                        )
                                    )
                                ) : null,
                                // E1k - Lack of Pleasure Expressions
                                !string.IsNullOrWhiteSpace(lackOfPleasureExpressions)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "E1k")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", lackOfPleasureExpressions))
                                        )
                                    )
                                ) : null,
                                // E2a - Self Report: Little Interest
                                !string.IsNullOrWhiteSpace(selfReportLittleInterest)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "E2a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", selfReportLittleInterest))
                                        )
                                    )
                                ) : null,
                                // E2b - Self Report: Anxious, Restless or Uneasy
                                !string.IsNullOrWhiteSpace(selfReportAnxiousRestlessUneasy)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "E2b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", selfReportAnxiousRestlessUneasy))
                                        )
                                    )
                                ) : null,
                                // E2c - Self Report: Sad, Depressed or Hopeless
                                !string.IsNullOrWhiteSpace(selfReportSadDepressedHopeless)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "E2c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", selfReportSadDepressedHopeless))
                                        )
                                    )
                                ) : null,
                                // E3a - Wandering
                                !string.IsNullOrWhiteSpace(wandering)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "E3a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", wandering))
                                        )
                                    )
                                ) : null,
                                // E3b - Verbal Abuse
                                !string.IsNullOrWhiteSpace(verbalAbuse)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "E3b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", verbalAbuse))
                                        )
                                    )
                                ) : null,
                                // E3c - Physical Abuse
                                !string.IsNullOrWhiteSpace(physicalAbuse)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "E3c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", physicalAbuse))
                                        )
                                    )
                                ) : null,
                                // E3d - Socially Inappropriate
                                !string.IsNullOrWhiteSpace(sociallyInappropriate)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "E3d")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", sociallyInappropriate))
                                        )
                                    )
                                ) : null,
                                // E3e - Inappropriate Sexual Behaviour
                                !string.IsNullOrWhiteSpace(inappropriateSexualBehaviour)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "E3e")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", inappropriateSexualBehaviour))
                                        )
                                    )
                                ) : null,
                                // E3f - Resists Care
                                !string.IsNullOrWhiteSpace(resistsCare)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "E3f")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", resistsCare))
                                        )
                                    )
                                ) : null
                            ),
                            // Section E END
                            // Section F
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", new XAttribute("value", "F")),
                                // F1a - Social Relationships — Participation in Long-Standing Activities
                                !string.IsNullOrWhiteSpace(socialParticipationLongStandingActivities)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "F1a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", socialParticipationLongStandingActivities))
                                        )
                                    )
                                ) : null,
                                // F1b - Social Relationships — Visit From Long-Standing Relation/Family Member
                                !string.IsNullOrWhiteSpace(socialVisitLongStandingRelation)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "F1b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", socialVisitLongStandingRelation))
                                        )
                                    )
                                ) : null,
                                // F1c - Social Relationships — Other Interaction With Long-Standing Relation/Family Member
                                !string.IsNullOrWhiteSpace(socialOtherInteractionLongStandingRelation)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "F1c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", socialOtherInteractionLongStandingRelation))
                                        )
                                    )
                                ) : null,
                                // F2a - At Ease Interacting With Others
                                !string.IsNullOrWhiteSpace(atEaseInteractingWithOthers)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "F2a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", atEaseInteractingWithOthers))
                                        )
                                    )
                                ) : null,
                                // F2b - At Ease Doing Planned Activities
                                !string.IsNullOrWhiteSpace(atEaseDoingPlannedActivities)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "F2b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", atEaseDoingPlannedActivities))
                                        )
                                    )
                                ) : null,
                                // F2c - Accepts Invitations
                                !string.IsNullOrWhiteSpace(acceptsInvitations)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "F2c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", acceptsInvitations))
                                        )
                                    )
                                ) : null,
                                // F2d - Pursues Involvement in Activities of Facility/Community
                                !string.IsNullOrWhiteSpace(pursuesInvolvementActivities)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "F2d")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", pursuesInvolvementActivities))
                                        )
                                    )
                                ) : null,
                                // F2e - Initiates Interactions(s) With Others
                                !string.IsNullOrWhiteSpace(initiatesInteractionsWithOthers)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "F2e")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", initiatesInteractionsWithOthers))
                                        )
                                    )
                                ) : null,
                                // F2f - Reacts Positively to Interactions Initiated by Others
                                !string.IsNullOrWhiteSpace(reactsPositivelyToInteractions)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "F2f")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", reactsPositivelyToInteractions))
                                        )
                                    )
                                ) : null,
                                // F2g - Adjusts Easily to Change in Routine
                                !string.IsNullOrWhiteSpace(adjustsEasilyToChangeInRoutine)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "F2g")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", adjustsEasilyToChangeInRoutine))
                                        )
                                    )
                                ) : null,
                                // F3a - Conflict With Other Care Recipients
                                !string.IsNullOrWhiteSpace(conflictWithOtherCareRecipients)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "F3a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", conflictWithOtherCareRecipients))
                                        )
                                    )
                                ) : null,
                                // F3b - Conflict With Staff
                                !string.IsNullOrWhiteSpace(conflictWithStaff)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "F3b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", conflictWithStaff))
                                        )
                                    )
                                ) : null,
                                // F3c - Staff Frustration
                                !string.IsNullOrWhiteSpace(staffFrustration)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "F3c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", staffFrustration))
                                        )
                                    )
                                ) : null,
                                // F3d - Family or Friends Overwhelmed
                                !string.IsNullOrWhiteSpace(familyOrFriendsOverwhelmed)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "F3d")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", familyOrFriendsOverwhelmed))
                                        )
                                    )
                                ) : null,
                                // F3e - Lonely
                                !string.IsNullOrWhiteSpace(lonely)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "F3e")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", lonely))
                                        )
                                    )
                                ) : null,
                                // F4 - Major Life Stressors
                                !string.IsNullOrWhiteSpace(majorLifeStressors)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "F4")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", majorLifeStressors))
                                        )
                                    )
                                ) : null,
                                // F5a - Consistent Positive Outlook
                                !string.IsNullOrWhiteSpace(consistentPositiveOutlook)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "F5a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", consistentPositiveOutlook))
                                        )
                                    )
                                ) : null,
                                // F5b - Finds Meaning in Day-to-Day Life
                                !string.IsNullOrWhiteSpace(findsMeaningInDayToDayLife)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "F5b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", findsMeaningInDayToDayLife))
                                        )
                                    )
                                ) : null,
                                // F5c - strongAndSupportiveRelationship
                                !string.IsNullOrWhiteSpace(strongAndSupportiveRelationship)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "F5c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", strongAndSupportiveRelationship))
                                        )
                                    )
                                ) : null
                            ),
                            // Section F END
                            // Section G
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", new XAttribute("value", "G")),

                                !string.IsNullOrWhiteSpace(adlBathingPerformance)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "G1a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", adlBathingPerformance))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(adlPersonalHygienePerformance)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "G1b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", adlPersonalHygienePerformance))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(adlDressingUpperBodyPerformance)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "G1c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", adlDressingUpperBodyPerformance))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(adlDressingLowerBodyPerformance)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "G1d")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", adlDressingLowerBodyPerformance))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(adlWalkingPerformance)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "G1e")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", adlWalkingPerformance))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(adlLocomotionPerformance)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "G1f")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", adlLocomotionPerformance))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(adlTransferToiletPerformance)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "G1g")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", adlTransferToiletPerformance))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(adlToiletUsePerformance)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "G1h")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", adlToiletUsePerformance))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(adlBedMobilityPerformance)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "G1i")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", adlBedMobilityPerformance))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(adlEatingPerformance)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "G1j")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", adlEatingPerformance))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(primaryModeOfLocomotion)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "G2a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", primaryModeOfLocomotion))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(timedWalk)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "G2b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", new XAttribute("value", timedWalk))
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(distanceWalked)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "G2c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", distanceWalked))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(distanceWheeledSelf)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "G2d")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", distanceWheeledSelf))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(totalHoursExerciseOrPhysicalActivity)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "G3a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", totalHoursExerciseOrPhysicalActivity))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(activityLevelDaysOutOfHouse)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "G3b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", activityLevelDaysOutOfHouse))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(personBelievesCanImprove)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "G4a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", personBelievesCanImprove))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(careProfessionalBelievesCanImprove)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "G4b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", careProfessionalBelievesCanImprove))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(changeInAdlStatus)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "G5")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", changeInAdlStatus))
                                        )
                                    )
                                 ) : null
                            ),
                            // Section G END
                            // Section H
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", new XAttribute("value", "H")),

                                !string.IsNullOrWhiteSpace(bladderContinence)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "H1")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", bladderContinence))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(urinaryCollectionDevice)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "H2")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", urinaryCollectionDevice))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(bowelContinence)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "H3")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", bowelContinence))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(ostomy)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "H4")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", ostomy))
                                        )
                                    )
                                ) : null
                            ),
                            // Section H END
                            // Section I
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", new XAttribute("value", "I")),

                                !string.IsNullOrWhiteSpace(hipFracture)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "I1a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", hipFracture))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(otherFracture)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "I1b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", otherFracture))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(alzheimers)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "I1c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", alzheimers))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(otherDementia)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "I1d")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", otherDementia))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(hemiplegia)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "I1e")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", hemiplegia))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(multipleSclerosis)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "I1f")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", multipleSclerosis))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(paraplegia)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "I1g")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", paraplegia))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(parkinsons)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "I1h")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", parkinsons))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(quadriplegia)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "I1i")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", quadriplegia))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(strokeCva)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "I1j")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", strokeCva))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(coronaryHeartDisease)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "I1k")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", coronaryHeartDisease))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(congestiveHeartFailure)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "I1m")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", congestiveHeartFailure))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(copd)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "I1l")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", copd))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(anxiety)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "I1n")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", anxiety))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(bipolar)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "I1o")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", bipolar))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(depression)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "I1p")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", depression))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(schizophrenia)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "I1q")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", schizophrenia))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(pneumonia)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "I1r")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", pneumonia))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(urinaryTractInfection)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "I1s")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", urinaryTractInfection))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(cancer)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "I1t")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", cancer))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(diabetesMellitus)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "I1u")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", diabetesMellitus))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(diseaseCode) ||
                                !string.IsNullOrWhiteSpace(diseaseDiagnosisICD10)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "I2")),
                                    // I2aa - Disease Code
                                    !string.IsNullOrWhiteSpace(diseaseCode)
                                    ? new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "I2aa")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", diseaseCode))
                                            )
                                        )
                                    ) : null,
                                    // I2ab - Disease Diagnosis ICD-10-CA
                                    !string.IsNullOrWhiteSpace(diseaseDiagnosisICD10)
                                    ? new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "I2ab")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueString", new XAttribute("value", diseaseDiagnosisICD10))
                                        )
                                    ) : null
                                ) : null
                            ),
                            // Section I END
                            // Section J
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", new XAttribute("value", "J")),

                                !string.IsNullOrWhiteSpace(fallsLast30Days) ||
                                !string.IsNullOrWhiteSpace(falls31To90DaysAgo) ||
                                !string.IsNullOrWhiteSpace(falls91To180DaysAgo)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "J1")),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J1a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", fallsLast30Days))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J1b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", falls31To90DaysAgo))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J1c")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", falls91To180DaysAgo))
                                            )
                                        )
                                    )
                                ) : null,
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "J2")),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J2a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", difficultyStanding))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J2b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", difficultyTurningAround))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J2c")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", dizziness))
                                            )
                                        )
                                    ),
                                     new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J2d")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", unsteadyGait))
                                            )
                                        )
                                    ),
                                       new XElement(ns + "item",
                                       new XElement(ns + "linkId", new XAttribute("value", "J2e")),
                                       new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", chestPain))
                                            )
                                       )
                                    ),
                                        new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J2f")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", difficultyClearingAirway))
                                            )
                                        )
                                    ),
                                           new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J2g")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", abnormalThoughtProcess))
                                            )
                                        )
                                    ),
                                             new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J2h")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", delusions))
                                            )
                                        )
                                    ),
                                               new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J2i")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", hallucinations))
                                            )
                                        )
                                    ),
                                                 new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J2j")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", aphasia))
                                            )
                                        )
                                    ),
                                                   new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J2k")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", acidReflux))
                                            )
                                        )
                                    ),
                                                     new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J2l")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", constipation))
                                            )
                                        )
                                    ),
                                                       new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J2m")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", diarrhea))
                                            )
                                        )
                                    ),
                                                         new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J2n")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", vomiting))
                                            )
                                        )
                                    ),
                                                           new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J2o")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", difficultyFallingAsleepOrStayingAsleep))
                                            )
                                        )
                                    ),
                                                             new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J2p")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", tooMuchSleep))
                                            )
                                        )
                                    ),
                                                               new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J2q")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", aspiration))
                                            )
                                        )
                                    ),
                                                                 new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J2r")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", fever))
                                            )
                                        )
                                    ),
                                                                   new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J2s")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", giOrGuBleeding))
                                            )
                                        )
                                    ),
                                                                     new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J2t")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", poorHygiene))
                                            )
                                        )
                                    ),
                                        new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J2u")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", peripheralEdema))
                                            )
                                        )
                                    )
                                ),
                                !string.IsNullOrWhiteSpace(dyspnea) ||
                                !string.IsNullOrWhiteSpace(fatigue)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "J3")),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J3a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", dyspnea))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J3b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", fatigue))
                                            )
                                        )
                                    )
                                ) : null,
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "J3")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", dyspnea))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "J4")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", dyspnea))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "J5")),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J5a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", painFrequency))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J5b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", painIntensity))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J5c")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", painConsistency))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J5d")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", breakthroughPain))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J5e")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", painControl))
                                            )
                                        )
                                    )
                                ),
                                 new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "J6")),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J6a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", conditionsUnstable))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J6b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", acuteEpisodeOrFlareUp))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J6c")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", endStageDisease))
                                            )
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "J7")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", selfReportedHealth))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "J8")),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J8a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", smokesTobaccoDaily))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "J8b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", alcohol))
                                            )
                                        )
                                    )
                                )
                            ),
                            // Section J END
                            // Section K
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", new XAttribute("value", "K")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "K1")),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "K1a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", heightCentimetres))
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "K1b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueDecimal", new XAttribute("value", weightKilograms))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "K2")),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "K2a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", weightLoss))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "K2b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", dehydrated))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "K2c")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", fluidIntake))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "K2d")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", fluidOutputExceedsInput))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "K2e")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", decreaseInFoodOrFluid))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "K2f")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", ateOneOrFewerMeals))
                                            )
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "K3")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", modeOfNutritionalIntake))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "K4")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", parenteralIntake))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "K5")),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "K5a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", wearsDenture))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "K5b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", brokenTeeth))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "K5c")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", reportsMouthFacialPain))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "K5d")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", reportsHavingDryMouth))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "K5e")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", reportsDifficultyChewing))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "K5f")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", gumInflammation))
                                            )
                                        )
                                    )
                                )
                            ),
                            // Section L
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", new XAttribute("value", "L")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "L1")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", mostSeverePressureUlcer))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "L2")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", priorPressureUlcer))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "L3")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", otherSkinUlcer))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "L4")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", majorSkinProblems))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "L5")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", skinTearsOrCuts))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "L6")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", otherSkinCondition))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "L7")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", footProblems))
                                        )
                                    )
                                )
                            ),
                            // Section M
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", new XAttribute("value", "M")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "M1")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", timeInvolvedInActivities))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "M2")),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "M2a")),
                                        new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", priorPressureUlcer))
                                        )
                                    )),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "M2b")),
                                        new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", computerActivity))
                                        )
                                    )),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "M2c")),
                                        new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", conversingTalkingOnPhone))
                                        )
                                    )),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "M2d")),
                                        new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", craftsOrArts))
                                        )
                                    )),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "M2e")),
                                        new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", dancing))
                                        )
                                    )),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "M2f")),
                                        new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", reminiscingAboutLife))
                                        )
                                    )),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "M2g")),
                                        new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", exerciseOrSports))
                                        )
                                    )),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "M2h")),
                                        new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", gardeningOrPlants))
                                        )
                                    )),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "M2i")),
                                        new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", helpingOthers))
                                        )
                                    )),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "M2j")),
                                        new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", musicOrSinging))
                                        )
                                    )),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "M2k")),
                                        new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", pets))
                                        )
                                    )),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "M2l")),
                                        new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", reading))
                                        )
                                    )),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "M2m")),
                                        new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", spiritualActivities))
                                        )
                                    )),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "M2n")),
                                        new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", tripsOrShopping))
                                        )
                                    )),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "M2o")),
                                        new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", walkingOrWheelingOutdoors))
                                        )
                                    )),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "M2p")),
                                        new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", watchingTvOrListeningToRadio))
                                        )
                                    ))
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "M3")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", timeAsleepDuringDay))
                                        )
                                    )
                                )
                            ),
                            // Section N
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", new XAttribute("value", "N")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "N2")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", drugAllergy))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "N3")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", new XAttribute("value", numberOfMedications))
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "N4")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", new XAttribute("value", numberOfHerbalNutritionalSupplements))
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "N5")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", recentlyChangedMedications))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "N6")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", selfReportNeedForMedicationReview))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "N7")),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "N7a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", antipsychoticLast7Days))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "N7b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", anxiolyticLast7Days))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "N7c")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", antidepressantLast7Days))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "N7d")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", hypnoticLast7Days))
                                            )
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "N8")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", medicationByDailyInjection))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "N9")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", cannabisUseTimeSinceUse))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "N10")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", medicinalUseOfCannabis))
                                        )
                                    )
                                )
                            ),
                            // Section O
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", new XAttribute("value", "O")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "O1")),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O1a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", bloodPressure))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O1b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", colonoscopy))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O1c")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", dentalExam))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O1d")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", eyeExam))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O1e")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", hearingExam))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O1f")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", influenzaVaccine))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O1g")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", mammogramOrBreastExam))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O1h")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", pneumovaxVaccine))
                                            )
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                new XElement(ns + "linkId", new XAttribute("value", "O2")),
                                    new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "O2a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", chemotherapy))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O2b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", dialysis))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O2c")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", infectionControlSegregation))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O2d")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", ivMedication))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                            new XElement(ns + "linkId", new XAttribute("value", "O2e")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", oxygenTherapy))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                            new XElement(ns + "linkId", new XAttribute("value", "O2f")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", radiation))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                            new XElement(ns + "linkId", new XAttribute("value", "O2g")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", suctioning))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                            new XElement(ns + "linkId", new XAttribute("value", "O2h")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", tracheostomyCare))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                            new XElement(ns + "linkId", new XAttribute("value", "O2i")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", transfusion))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                            new XElement(ns + "linkId", new XAttribute("value", "O2j")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", ventilator))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                            new XElement(ns + "linkId", new XAttribute("value", "O2k")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", woundCare))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                            new XElement(ns + "linkId", new XAttribute("value", "O2l")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", scheduledToiletingProgram))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                            new XElement(ns + "linkId", new XAttribute("value", "O2m")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", palliativeCare))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                            new XElement(ns + "linkId", new XAttribute("value", "O2n")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", turningProgram))
                                            )
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                new XElement(ns + "linkId", new XAttribute("value", "O3")),
                                    new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "O3ab")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", physicalTherapyDays))
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O3ac")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", physicalTherapyMinutes))
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O3aa")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", physicalTherapyScheduled))
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O3bb")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", occupationalTherapyDays))
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O3bc")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", occupationalTherapyMinutes))
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O3ba")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", occupationalTherapyScheduled))
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O3cb")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", speechLanguageTherapyDays))
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O3cc")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", speechLanguageTherapyMinutes))
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O3ca")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", speechLanguageTherapyScheduled))
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O3db")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", respiratoryTherapyDays))
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O3dc")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", respiratoryTherapyMinutes))
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O3da")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", respiratoryTherapyScheduled))
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O3eb")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", functionalRehabilitationDays))
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O3ec")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", functionalRehabilitationMinutes))
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O3ea")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", functionalRehabilitationScheduled))
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O3fb")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", psychologicalTherapiesDays))
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O3fc")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", psychologicalTherapiesMinutes))
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O3fa")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", psychologicalTherapiesScheduled))
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O3gb")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", recreationTherapyDays))
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O3gc")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", recreationTherapyMinutes))
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O3ga")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", recreationTherapyScheduled))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                new XElement(ns + "linkId", new XAttribute("value", "O4")),
                                    new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "O4a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", inpatientAcuteCareHospitalWithOvernightStay))
                                        )
                                    ),
                                    new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "O4b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueInteger", new XAttribute("value", emergencyRoomVisit))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                new XElement(ns + "linkId", new XAttribute("value", "O5")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", new XAttribute("value", numberOfDaysPhysicianVisits))
                                    )
                                ),
                                new XElement(ns + "item",
                                new XElement(ns + "linkId", new XAttribute("value", "O6")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", new XAttribute("value", numberOfDaysPhysicianOrders))
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", "O7")),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O7a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", fullBedRails))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O7b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", trunkRestraint))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", "O7c")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", chairPreventsRising))
                                            )
                                        )
                                    )
                                )
                            ),
                            // Section P
                            new XElement(ns + "item",
                                new XElement(ns + "linkId",
                                    new XAttribute("value", $"P")
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", $"P1")),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", $"P1a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", decisionMakerPersonalCare))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", $"P1b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", decisionMakerProperty))
                                            )
                                        )
                                    )
                                ),
                                 new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", $"P2")),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", $"P2a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", decisionMakerPersonalCare))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", $"P2b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", decisionMakerProperty))
                                            )
                                        )
                                    )
                                )
                            ),
                            // Section Q
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", new XAttribute("value", $"Q")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", $"Q1")),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", $"Q1a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", preferenceToReturnOrRemainInCommunity))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", $"Q1b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", supportPersonPositiveAboutDischarge))
                                            )
                                        )
                                    ),
                                    new XElement(ns + "item",
                                        new XElement(ns + "linkId", new XAttribute("value", $"Q1c")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", new XAttribute("value", hasHousingAvailableInCommunity))
                                            )
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                new XElement(ns + "linkId", new XAttribute("value", $"Q2")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", expectedLengthOfStay))
                                        )
                                    )
                                )
                            ),
                            // Section R
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", new XAttribute("value", $"R")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", $"R1")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", lastDayOfStay))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", $"R2")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", residentialLivingStatusAfterDischarge))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                new XElement(ns + "linkId", new XAttribute("value", $"R3")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", dischargeToFacilityNumber))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                new XElement(ns + "linkId", new XAttribute("value", $"R4")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", homeCareServicesScheduledAtDischarge))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                new XElement(ns + "linkId", new XAttribute("value", $"R5")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", new XAttribute("value", covid19Status))
                                        )
                                    )
                                )
                            ),
                            // Section S
                            new XElement(ns + "item",
                                 new XElement(ns + "linkId", new XAttribute("value", $"S")),
                                 new XElement(ns + "item",
                                    new XElement(ns + "linkId", new XAttribute("value", $"S2")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "ValueDate", new XAttribute("value", assessmentSignedAsComplete))
                                        )
                                    )
                                 )
                            )
                        )
                    ),
                new XElement(ns + "request",
                    new XElement(ns + "method", new XAttribute("value", "POST")),
                    new XElement(ns + "url", new XAttribute("value", "QuestionnaireResponse"))
                )
            );

            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), bundle);
        }
    }
}
