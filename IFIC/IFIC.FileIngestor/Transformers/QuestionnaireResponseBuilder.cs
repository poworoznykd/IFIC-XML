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

        // === Added helpers to prune empty nodes and sections ===
        private static bool HasValueAttribute(XElement e)
        {
            var attr = e.Attribute("value");
            return attr != null && !string.IsNullOrWhiteSpace(attr.Value);
        }

        /// <summary>
        /// Recursively remove empty leaf nodes and prunes any <item> that contains only linkId and no actual answers/sub-items.
        /// Also removes <answer> blocks that have no valid child value nodes.
        /// </summary>
        private void PruneQuestionnaireResponseEntry(XElement entryRoot)
        {
            if (entryRoot == null) return;

            // Work only inside the QuestionnaireResponse resource of this entry
            XNamespace n = ns;
            var qr = entryRoot.Descendants(n + "QuestionnaireResponse").FirstOrDefault();
            if (qr == null) return;

            // Remove any value nodes that have no @value (e.g., <valueString/>, <valueDate/>, <code/> without value attr)
            var valueNodeNames = new[] { "valueString", "valueDecimal", "valueInteger", "valueDate", "valueTime", "code" };
            foreach (var v in qr.Descendants().Where(x => valueNodeNames.Contains(x.Name.LocalName)).ToList())
            {
                if (!HasValueAttribute(v))
                    v.Remove();
            }

            // Remove <valueCoding> with no <code>
            foreach (var vc in qr.Descendants(n + "valueCoding").ToList())
            {
                if (!vc.Elements(n + "code").Any())
                    vc.Remove();
            }

            // Remove <answer> with no children after previous pruning
            foreach (var ans in qr.Descendants(n + "answer").ToList())
            {
                if (!ans.Elements().Any())
                    ans.Remove();
            }

            // Recursively prune empty <item> nodes: keep if it has any sub-items or answers after pruning
            bool removed;
            do
            {
                removed = false;
                foreach (var item in qr.Descendants(n + "item").ToList())
                {
                    // Keep the section/item if it has at least one non-linkId child element
                    var childElems = item.Elements().Where(e => e.Name.LocalName != "linkId").ToList();
                    if (childElems.Count == 0)
                    {
                        item.Remove();
                        removed = true;
                    }
                }
            } while (removed);
        }


        /// <summary>
        /// Builds a FHIR QuestionnaireResponse entry for the Bundle using parsed flat file data.
        /// </summary>
        /// <param name="parsedFile"></param>
        /// <param name="patientId"></param>
        /// <param name="encounterId"></param>
        /// <param name="questionnaireResponseId"></param>
        /// <returns></returns>
        public XElement BuildQuestionnaireResponseEntry(
            ParsedFlatFile parsedFile,
            string patientId,
            string encounterId,
            string questionnaireResponseId)
        {

            XAttribute SafeAttr(XName name, string value) => string.IsNullOrWhiteSpace(value) ? null : new XAttribute(name, value);

            #region Initialize Variables
            var genderIdentity = "";
            var assessmentReason = "";
            var assessmentRefDate = "";
            var primaryCareGoal = "";
            var expressedCareGoal = "";
            var timeSinceLastVisit = "";

            var levelOfControl = "";
            var indigenousIdentityFirstNations = "";
            var indigenousIdentityMetis = "";
            var indigenousIdentityInuit = "";
            var residentialStatus = "";
            var priorLivingArrangement = "";
            var residentialHistoryA = "";
            var residentialHistoryB = "";
            var residentialHistoryC = "";
            var residentialHistoryD = "";
            var residentialHistoryE = "";
            var residentialHistoryF = "";
            var historyMentalIllness = "";
            var interpreterNeeded = "";

            var dailyDecisionMaking = "";
            var shortTermMemory = "";
            var longTermMemory = "";
            var proceduralMemory = "";
            var SituationalMemory = "";
            var easilyDistracted = "";
            var disorganizedSpeech = "";
            var varyingMentalFunction = "";
            var changeInMentalStatus = "";
            var changeInDecisionMaking = "";

            var makingSelfUnderstood = "";
            var abilityToUnderstandOthers = "";
            var hearing = "";
            var hearingAidUsed = "";
            var visionAdequateLight = "";
            var visionApplianceUsed = "";

            var negativeStatements = "";
            var persistentAngerWithSelfOrOthers = "";
            var unrealisticFears = "";
            var repetitiveHealthComplaints = "";
            var repetitiveAnxiousComplaints = "";
            var sadPainedOrWorriedFacialExpressions = "";
            var cryingTearfulness = "";
            var recurrentStatementsTerribleAboutToHappen = "";
            var withdrawalFromActivitiesOfInterest = "";
            var reducedSocialInteractions = "";
            var lackOfPleasureExpressions = "";
            var selfReportLittleInterest = "";
            var selfReportAnxiousRestlessUneasy = "";
            var selfReportSadDepressedHopeless = "";
            var wandering = "";
            var verbalAbuse = "";
            var physicalAbuse = "";
            var sociallyInappropriate = "";
            var inappropriateSexualBehaviour = "";
            var resistsCare = "";

            var socialParticipationLongStandingActivities = "";
            var socialVisitLongStandingRelation = "";
            var socialOtherInteractionLongStandingRelation = "";
            var atEaseInteractingWithOthers = "";
            var atEaseDoingPlannedActivities = "";
            var acceptsInvitations = "";
            var pursuesInvolvementActivities = "";
            var initiatesInteractionsWithOthers = "";
            var reactsPositivelyToInteractions = "";
            var adjustsEasilyToChangeInRoutine = "";
            var conflictWithOtherCareRecipients = "";
            var conflictWithStaff = "";
            var staffFrustration = "";
            var familyOrFriendsOverwhelmed = "";
            var lonely = "";
            var majorLifeStressors = "";
            var consistentPositiveOutlook = "";
            var findsMeaningInDayToDayLife = "";
            var strongAndSupportiveRelationship = "";

            var adlBathingPerformance = "";
            var adlPersonalHygienePerformance = "";
            var adlDressingUpperBodyPerformance = "";
            var adlDressingLowerBodyPerformance = "";
            var adlWalkingPerformance = "";
            var adlLocomotionPerformance = "";
            var adlTransferToiletPerformance = "";
            var adlToiletUsePerformance = "";
            var adlBedMobilityPerformance = "";
            var adlEatingPerformance = "";
            var primaryModeOfLocomotion = "";
            var timedWalk = "";
            var distanceWalked = "";
            var distanceWheeledSelf = "";
            var totalHoursExerciseOrPhysicalActivity = "";
            var activityLevelDaysOutOfHouse = "";
            var personBelievesCanImprove = "";
            var careProfessionalBelievesCanImprove = "";
            var changeInAdlStatus = "";

            var bladderContinence = "";
            var urinaryCollectionDevice = "";
            var bowelContinence = "";
            var ostomy = "";

            var hipFracture = "";
            var otherFracture = "";
            var alzheimers = "";
            var otherDementia = "";
            var hemiplegia = "";
            var multipleSclerosis = "";
            var paraplegia = "";
            var parkinsons = "";
            var quadriplegia = "";
            var strokeCva = "";
            var coronaryHeartDisease = "";
            var congestiveHeartFailure = "";
            var copd = "";
            var anxiety = "";
            var bipolar = "";
            var depression = "";
            var schizophrenia = "";
            var pneumonia = "";
            var urinaryTractInfection = "";
            var cancer = "";
            var diabetesMellitus = "";
            var diseaseCode = "";
            var diseaseDiagnosisICD10 = "";
            var diseaseCode_2 = "";
            var diseaseDiagnosisICD10_2 = "";
            var diseaseCode_3 = "";
            var diseaseDiagnosisICD10_3 = "";
            var diseaseCode_4 = "";
            var diseaseDiagnosisICD10_4 = "";
            var diseaseCode_5 = "";
            var diseaseDiagnosisICD10_5 = "";
            var diseaseCode_6 = "";
            var diseaseDiagnosisICD10_6 = "";

            var fallsLast30Days = "";
            var falls31To90DaysAgo = "";
            var falls91To180DaysAgo = "";
            var difficultyStanding = "";
            var difficultyTurningAround = "";
            var dizziness = "";
            var unsteadyGait = "";
            var chestPain = "";
            var difficultyClearingAirway = "";
            var abnormalThoughtProcess = "";
            var delusions = "";
            var hallucinations = "";
            var aphasia = "";
            var acidReflux = "";
            var constipation = "";
            var diarrhea = "";
            var vomiting = "";
            var difficultyFallingAsleepOrStayingAsleep = "";
            var tooMuchSleep = "";
            var aspiration = "";
            var fever = "";
            var giOrGuBleeding = "";
            var poorHygiene = "";
            var peripheralEdema = "";
            var dyspnea = "";
            var fatigue = "";
            var painFrequency = "";
            var painIntensity = "";
            var painConsistency = "";
            var breakthroughPain = "";
            var painControl = "";
            var conditionsUnstable = "";
            var acuteEpisodeOrFlareUp = "";
            var endStageDisease = "";
            var selfReportedHealth = "";
            var smokesTobaccoDaily = "";
            var alcohol = "";

            var heightCentimetres = "";
            var weightKilograms = "";
            var weightLoss = "";
            var dehydrated = "";
            var fluidIntake = "";
            var fluidOutputExceedsInput = "";
            var decreaseInFoodOrFluid = "";
            var ateOneOrFewerMeals = "";
            var modeOfNutritionalIntake = "";
            var parenteralIntake = "";
            var wearsDenture = "";
            var brokenTeeth = "";
            var reportsMouthFacialPain = "";
            var reportsHavingDryMouth = "";
            var reportsDifficultyChewing = "";
            var gumInflammation = "";

            var mostSeverePressureUlcer = "";
            var priorPressureUlcer = "";
            var otherSkinUlcer = "";
            var majorSkinProblems = "";
            var skinTearsOrCuts = "";
            var otherSkinCondition = "";
            var footProblems = "";

            var timeInvolvedInActivities = "";
            var cardsGamesOrPuzzles = "";
            var computerActivity = "";
            var conversingTalkingOnPhone = "";
            var craftsOrArts = "";
            var dancing = "";
            var reminiscingAboutLife = "";
            var exerciseOrSports = "";
            var gardeningOrPlants = "";
            var helpingOthers = "";
            var musicOrSinging = "";
            var pets = "";
            var reading = "";
            var spiritualActivities = "";
            var tripsOrShopping = "";
            var walkingOrWheelingOutdoors = "";
            var watchingTvOrListeningToRadio = "";
            var timeAsleepDuringDay = "";

            var drugAllergy = "";
            var numberOfMedications = "";
            var numberOfHerbalNutritionalSupplements = "";
            var recentlyChangedMedications = "";
            var selfReportNeedForMedicationReview = "";
            var antipsychoticLast7Days = "";
            var anxiolyticLast7Days = "";
            var antidepressantLast7Days = "";
            var hypnoticLast7Days = "";
            var medicationByDailyInjection = "";
            var cannabisUseTimeSinceUse = "";
            var medicinalUseOfCannabis = "";

            var bloodPressure = "";
            var colonoscopy = "";
            var dentalExam = "";
            var eyeExam = "";
            var hearingExam = "";
            var influenzaVaccine = "";
            var mammogramOrBreastExam = "";
            var pneumovaxVaccine = "";
            var chemotherapy = "";
            var dialysis = "";
            var infectionControlSegregation = "";
            var ivMedication = "";
            var oxygenTherapy = "";
            var radiation = "";
            var suctioning = "";
            var tracheostomyCare = "";
            var transfusion = "";
            var ventilator = "";
            var woundCare = "";
            var scheduledToiletingProgram = "";
            var palliativeCare = "";
            var turningProgram = "";
            var physicalTherapyDays = "";
            var physicalTherapyMinutes = "";
            var physicalTherapyScheduled = "";
            var occupationalTherapyDays = "";
            var occupationalTherapyMinutes = "";
            var occupationalTherapyScheduled = "";
            var speechLanguageTherapyDays = "";
            var speechLanguageTherapyMinutes = "";
            var speechLanguageTherapyScheduled = "";
            var respiratoryTherapyDays = "";
            var respiratoryTherapyMinutes = "";
            var respiratoryTherapyScheduled = "";
            var functionalRehabilitationDays = "";
            var functionalRehabilitationMinutes = "";
            var functionalRehabilitationScheduled = "";
            var psychologicalTherapiesDays = "";
            var psychologicalTherapiesMinutes = "";
            var psychologicalTherapiesScheduled = "";
            var recreationTherapyDays = "";
            var recreationTherapyMinutes = "";
            var recreationTherapyScheduled = "";
            var inpatientAcuteCareHospitalWithOvernightStay = "";
            var emergencyRoomVisit = "";
            var fullBedRails = "";
            var trunkRestraint = "";
            var chairPreventsRising = "";
            var numberOfDaysPhysicianVisits = "";
            var numberOfDaysPhysicianOrders = "";

            var decisionMakerPersonalCare = "";
            var decisionMakerProperty = "";
            var advanceDirectiveDoNotResuscitate = "";
            var advanceDirectiveDoNotHospitalize = "";

            var preferenceToReturnOrRemainInCommunity = "";
            var supportPersonPositiveAboutDischarge = "";
            var hasHousingAvailableInCommunity = "";
            var expectedLengthOfStay = "";

            var lastDayOfStay = "";
            var residentialLivingStatusAfterDischarge = "";
            var dischargeToFacilityNumber = "";
            var homeCareServicesScheduledAtDischarge = "";
            var covid19Status = "";

            var assessmentSignedAsComplete = "";
            #endregion

            #region Section A
            parsedFile.AssessmentSections.TryGetValue("SECTION A", out var sectionA);

            if (sectionA != null)
            {
                sectionA.TryGetValue("A2b", out genderIdentity);
                sectionA.TryGetValue("A8", out assessmentReason);
                sectionA.TryGetValue("A9", out assessmentRefDate);
                sectionA.TryGetValue("A10", out primaryCareGoal);
                sectionA.TryGetValue("A10a", out expressedCareGoal);
                sectionA.TryGetValue("A11", out timeSinceLastVisit);
            }
            #endregion
            #region Section B
            parsedFile.AssessmentSections.TryGetValue("SECTION B", out var sectionB);
            if (sectionB != null)
            {

                sectionB.TryGetValue("B1", out levelOfControl);
                sectionB.TryGetValue("B3a", out indigenousIdentityFirstNations);
                sectionB.TryGetValue("B3b", out indigenousIdentityMetis);
                sectionB.TryGetValue("B3c", out indigenousIdentityInuit);
                sectionB.TryGetValue("B5c", out residentialStatus);
                sectionB.TryGetValue("B7", out priorLivingArrangement);
                sectionB.TryGetValue("B8a", out residentialHistoryA);
                sectionB.TryGetValue("B8b", out residentialHistoryB);
                sectionB.TryGetValue("B8c", out residentialHistoryC);
                sectionB.TryGetValue("B8d", out residentialHistoryD);
                sectionB.TryGetValue("B8e", out residentialHistoryE);
                sectionB.TryGetValue("B8f", out residentialHistoryF);
                sectionB.TryGetValue("B9", out historyMentalIllness);
                sectionB.TryGetValue("B10", out interpreterNeeded);
            }
            #endregion
            #region Section C
            parsedFile.AssessmentSections.TryGetValue("SECTION C", out var sectionC);
            if (sectionC != null)
            {
                sectionC.TryGetValue("C1", out dailyDecisionMaking);
                sectionC.TryGetValue("C2a", out shortTermMemory);
                sectionC.TryGetValue("C2b", out longTermMemory);
                sectionC.TryGetValue("C2c", out proceduralMemory);
                sectionC.TryGetValue("C2d", out SituationalMemory);
                sectionC.TryGetValue("C3a", out easilyDistracted);
                sectionC.TryGetValue("C3b", out disorganizedSpeech);
                sectionC.TryGetValue("C3c", out varyingMentalFunction);
                sectionC.TryGetValue("C4", out changeInMentalStatus);
                sectionC.TryGetValue("C5", out changeInDecisionMaking);
            }
            #endregion
            #region Section D

            parsedFile.AssessmentSections.TryGetValue("SECTION D", out var sectionD);
            if (sectionD != null)
            {
                sectionD.TryGetValue("D1", out makingSelfUnderstood);
                sectionD.TryGetValue("D2", out abilityToUnderstandOthers);
                sectionD.TryGetValue("D3a", out hearing);
                sectionD.TryGetValue("D3b", out hearingAidUsed);
                sectionD.TryGetValue("D4a", out visionAdequateLight);
                sectionD.TryGetValue("D4b", out visionApplianceUsed);
            }

            #endregion
            #region Section E
            parsedFile.AssessmentSections.TryGetValue("SECTION E", out var sectionE);
            if (sectionE != null)
            {
                sectionE.TryGetValue("E1a", out negativeStatements);
                sectionE.TryGetValue("E1b", out persistentAngerWithSelfOrOthers);
                sectionE.TryGetValue("E1c", out unrealisticFears);
                sectionE.TryGetValue("E1d", out repetitiveHealthComplaints);
                sectionE.TryGetValue("E1e", out repetitiveAnxiousComplaints);
                sectionE.TryGetValue("E1f", out sadPainedOrWorriedFacialExpressions);
                sectionE.TryGetValue("E1g", out cryingTearfulness);
                sectionE.TryGetValue("E1h", out recurrentStatementsTerribleAboutToHappen);
                sectionE.TryGetValue("E1i", out withdrawalFromActivitiesOfInterest);
                sectionE.TryGetValue("E1j", out reducedSocialInteractions);
                sectionE.TryGetValue("E1k", out lackOfPleasureExpressions);
                sectionE.TryGetValue("E2a", out selfReportLittleInterest);
                sectionE.TryGetValue("E2b", out selfReportAnxiousRestlessUneasy);
                sectionE.TryGetValue("E2c", out selfReportSadDepressedHopeless);
                sectionE.TryGetValue("E3a", out wandering);
                sectionE.TryGetValue("E3b", out verbalAbuse);
                sectionE.TryGetValue("E3c", out physicalAbuse);
                sectionE.TryGetValue("E3d", out sociallyInappropriate);
                sectionE.TryGetValue("E3e", out inappropriateSexualBehaviour);
                sectionE.TryGetValue("E3f", out resistsCare);
            }


            #endregion
            #region Section F
            parsedFile.AssessmentSections.TryGetValue("SECTION F", out var sectionF);
            if (sectionF != null)
            {
                sectionF.TryGetValue("F1a", out socialParticipationLongStandingActivities);
                sectionF.TryGetValue("F1b", out socialVisitLongStandingRelation);
                sectionF.TryGetValue("F1c", out socialOtherInteractionLongStandingRelation);

                sectionF.TryGetValue("F2a", out atEaseInteractingWithOthers);
                sectionF.TryGetValue("F2b", out atEaseDoingPlannedActivities);
                sectionF.TryGetValue("F2c", out acceptsInvitations);
                sectionF.TryGetValue("F2d", out pursuesInvolvementActivities);
                sectionF.TryGetValue("F2e", out initiatesInteractionsWithOthers);
                sectionF.TryGetValue("F2f", out reactsPositivelyToInteractions);
                sectionF.TryGetValue("F2g", out adjustsEasilyToChangeInRoutine);

                sectionF.TryGetValue("F3a", out conflictWithOtherCareRecipients);
                sectionF.TryGetValue("F3b", out conflictWithStaff);
                sectionF.TryGetValue("F3c", out staffFrustration);
                sectionF.TryGetValue("F3d", out familyOrFriendsOverwhelmed);
                sectionF.TryGetValue("F3e", out lonely);

                sectionF.TryGetValue("F4", out majorLifeStressors);

                sectionF.TryGetValue("F5a", out consistentPositiveOutlook);
                sectionF.TryGetValue("F5b", out findsMeaningInDayToDayLife);
                sectionF.TryGetValue("F5c", out strongAndSupportiveRelationship);
            }

            #endregion
            #region Section G
            parsedFile.AssessmentSections.TryGetValue("SECTION G", out var sectionG);
            if (sectionG != null)
            {
                sectionG.TryGetValue("G1a", out adlBathingPerformance);
                sectionG.TryGetValue("G1b", out adlPersonalHygienePerformance);
                sectionG.TryGetValue("G1c", out adlDressingUpperBodyPerformance);
                sectionG.TryGetValue("G1d", out adlDressingLowerBodyPerformance);
                sectionG.TryGetValue("G1e", out adlWalkingPerformance);
                sectionG.TryGetValue("G1f", out adlLocomotionPerformance);
                sectionG.TryGetValue("G1g", out adlTransferToiletPerformance);
                sectionG.TryGetValue("G1h", out adlToiletUsePerformance);
                sectionG.TryGetValue("G1i", out adlBedMobilityPerformance);
                sectionG.TryGetValue("G1j", out adlEatingPerformance);

                sectionG.TryGetValue("G2a", out primaryModeOfLocomotion);
                sectionG.TryGetValue("G2b", out timedWalk);
                sectionG.TryGetValue("G2c", out distanceWalked);
                sectionG.TryGetValue("G2d", out distanceWheeledSelf);

                sectionG.TryGetValue("G3a", out totalHoursExerciseOrPhysicalActivity);
                sectionG.TryGetValue("G3b", out activityLevelDaysOutOfHouse);

                sectionG.TryGetValue("G4a", out personBelievesCanImprove);
                sectionG.TryGetValue("G4b", out careProfessionalBelievesCanImprove);

                sectionG.TryGetValue("G5", out changeInAdlStatus);
            }

            #endregion
            #region Section H
            parsedFile.AssessmentSections.TryGetValue("SECTION H", out var sectionH);
            if (sectionH != null)
            {
                sectionH.TryGetValue("H1", out bladderContinence);
                sectionH.TryGetValue("H2", out urinaryCollectionDevice);
                sectionH.TryGetValue("H3", out bowelContinence);
                sectionH.TryGetValue("H4", out ostomy);
            }

            #endregion
            #region Section I
            // Section I variables
            parsedFile.AssessmentSections.TryGetValue("SECTION I", out var sectionI);
            if (sectionI != null)
            {
                sectionI.TryGetValue("I1a", out hipFracture);
                sectionI.TryGetValue("I1b", out otherFracture);
                sectionI.TryGetValue("I1c", out alzheimers);
                sectionI.TryGetValue("I1d", out otherDementia);
                sectionI.TryGetValue("I1e", out hemiplegia);
                sectionI.TryGetValue("I1f", out multipleSclerosis);
                sectionI.TryGetValue("I1g", out paraplegia);
                sectionI.TryGetValue("I1h", out parkinsons);
                sectionI.TryGetValue("I1i", out quadriplegia);
                sectionI.TryGetValue("I1j", out strokeCva);
                sectionI.TryGetValue("I1k", out coronaryHeartDisease);
                sectionI.TryGetValue("I1m", out congestiveHeartFailure);
                sectionI.TryGetValue("I1l", out copd);
                sectionI.TryGetValue("I1n", out anxiety);
                sectionI.TryGetValue("I1o", out bipolar);
                sectionI.TryGetValue("I1p", out depression);
                sectionI.TryGetValue("I1q", out schizophrenia);
                sectionI.TryGetValue("I1r", out pneumonia);
                sectionI.TryGetValue("I1s", out urinaryTractInfection);
                sectionI.TryGetValue("I1t", out cancer);
                sectionI.TryGetValue("I1u", out diabetesMellitus);

                sectionI.TryGetValue("I2aa", out diseaseCode);
                sectionI.TryGetValue("I2ab", out diseaseDiagnosisICD10);
                sectionI.TryGetValue("I2ba", out diseaseCode_2);
                sectionI.TryGetValue("I2bb", out diseaseDiagnosisICD10_2);
                sectionI.TryGetValue("I2ca", out diseaseCode_3);
                sectionI.TryGetValue("I2cb", out diseaseDiagnosisICD10_3);
                sectionI.TryGetValue("I2da", out diseaseCode_4);
                sectionI.TryGetValue("I2db", out diseaseDiagnosisICD10_4);
                sectionI.TryGetValue("I2ea", out diseaseCode_5);
                sectionI.TryGetValue("I2eb", out diseaseDiagnosisICD10_5);
                sectionI.TryGetValue("I2fa", out diseaseCode_6);
                sectionI.TryGetValue("I2fb", out diseaseDiagnosisICD10_6);
            }

            #endregion
            #region Section J
            parsedFile.AssessmentSections.TryGetValue("SECTION J", out var sectionJ);
            if (sectionJ != null)
            {
                sectionJ.TryGetValue("J1a", out fallsLast30Days);
                sectionJ.TryGetValue("J1b", out falls31To90DaysAgo);
                sectionJ.TryGetValue("J1c", out falls91To180DaysAgo);

                sectionJ.TryGetValue("J2a", out difficultyStanding);
                sectionJ.TryGetValue("J2b", out difficultyTurningAround);
                sectionJ.TryGetValue("J2c", out dizziness);
                sectionJ.TryGetValue("J2d", out unsteadyGait);
                sectionJ.TryGetValue("J2e", out chestPain);
                sectionJ.TryGetValue("J2f", out difficultyClearingAirway);
                sectionJ.TryGetValue("J2g", out abnormalThoughtProcess);
                sectionJ.TryGetValue("J2h", out delusions);
                sectionJ.TryGetValue("J2i", out hallucinations);
                sectionJ.TryGetValue("J2j", out aphasia);
                sectionJ.TryGetValue("J2k", out acidReflux);
                sectionJ.TryGetValue("J2l", out constipation);
                sectionJ.TryGetValue("J2m", out diarrhea);
                sectionJ.TryGetValue("J2n", out vomiting);
                sectionJ.TryGetValue("J2o", out difficultyFallingAsleepOrStayingAsleep);
                sectionJ.TryGetValue("J2p", out tooMuchSleep);
                sectionJ.TryGetValue("J2q", out aspiration);
                sectionJ.TryGetValue("J2r", out fever);
                sectionJ.TryGetValue("J2s", out giOrGuBleeding);
                sectionJ.TryGetValue("J2t", out poorHygiene);
                sectionJ.TryGetValue("J2u", out peripheralEdema);

                sectionJ.TryGetValue("J3", out dyspnea);
                sectionJ.TryGetValue("J4", out fatigue);

                sectionJ.TryGetValue("J5a", out painFrequency);
                sectionJ.TryGetValue("J5b", out painIntensity);
                sectionJ.TryGetValue("J5c", out painConsistency);
                sectionJ.TryGetValue("J5d", out breakthroughPain);
                sectionJ.TryGetValue("J5e", out painControl);

                sectionJ.TryGetValue("J6a", out conditionsUnstable);
                sectionJ.TryGetValue("J6b", out acuteEpisodeOrFlareUp);
                sectionJ.TryGetValue("J6c", out endStageDisease);

                sectionJ.TryGetValue("J7", out selfReportedHealth);

                sectionJ.TryGetValue("J8a", out smokesTobaccoDaily);
                sectionJ.TryGetValue("J8b", out alcohol);
            }

            #endregion
            #region Section K
            parsedFile.AssessmentSections.TryGetValue("SECTION K", out var sectionK);
            if (sectionK != null)
            {
                sectionK.TryGetValue("K1a", out heightCentimetres);
                sectionK.TryGetValue("K1b", out weightKilograms);

                sectionK.TryGetValue("K2a", out weightLoss);
                sectionK.TryGetValue("K2b", out dehydrated);
                sectionK.TryGetValue("K2c", out fluidIntake);
                sectionK.TryGetValue("K2d", out fluidOutputExceedsInput);
                sectionK.TryGetValue("K2e", out decreaseInFoodOrFluid);
                sectionK.TryGetValue("K2f", out ateOneOrFewerMeals);

                sectionK.TryGetValue("K3", out modeOfNutritionalIntake);
                sectionK.TryGetValue("K4", out parenteralIntake);

                sectionK.TryGetValue("K5a", out wearsDenture);
                sectionK.TryGetValue("K5b", out brokenTeeth);
                sectionK.TryGetValue("K5c", out reportsMouthFacialPain);
                sectionK.TryGetValue("K5d", out reportsHavingDryMouth);
                sectionK.TryGetValue("K5e", out reportsDifficultyChewing);
                sectionK.TryGetValue("K5f", out gumInflammation);
            }

            #endregion
            #region Section L
            parsedFile.AssessmentSections.TryGetValue("SECTION L", out var sectionL);
            if (sectionL != null)
            {
                sectionL.TryGetValue("L1", out mostSeverePressureUlcer);
                sectionL.TryGetValue("L2", out priorPressureUlcer);
                sectionL.TryGetValue("L3", out otherSkinUlcer);
                sectionL.TryGetValue("L4", out majorSkinProblems);
                sectionL.TryGetValue("L5", out skinTearsOrCuts);
                sectionL.TryGetValue("L6", out otherSkinCondition);
                sectionL.TryGetValue("L7", out footProblems);
            }

            #endregion
            #region Section M
            parsedFile.AssessmentSections.TryGetValue("SECTION M", out var sectionM);
            if (sectionM != null)
            {
                sectionM.TryGetValue("M1", out timeInvolvedInActivities);
                sectionM.TryGetValue("M2a", out cardsGamesOrPuzzles);
                sectionM.TryGetValue("M2b", out computerActivity);
                sectionM.TryGetValue("M2c", out conversingTalkingOnPhone);
                sectionM.TryGetValue("M2d", out craftsOrArts);
                sectionM.TryGetValue("M2e", out dancing);
                sectionM.TryGetValue("M2f", out reminiscingAboutLife);
                sectionM.TryGetValue("M2g", out exerciseOrSports);
                sectionM.TryGetValue("M2h", out gardeningOrPlants);
                sectionM.TryGetValue("M2i", out helpingOthers);
                sectionM.TryGetValue("M2j", out musicOrSinging);
                sectionM.TryGetValue("M2k", out pets);
                sectionM.TryGetValue("M2l", out reading);
                sectionM.TryGetValue("M2m", out spiritualActivities);
                sectionM.TryGetValue("M2n", out tripsOrShopping);
                sectionM.TryGetValue("M2o", out walkingOrWheelingOutdoors);
                sectionM.TryGetValue("M2p", out watchingTvOrListeningToRadio);

                sectionM.TryGetValue("M3", out timeAsleepDuringDay);
            }

            #endregion
            #region Section N
            parsedFile.AssessmentSections.TryGetValue("SECTION N", out var sectionN);
            if (sectionN != null)
            {
                sectionN.TryGetValue("N2", out drugAllergy);
                sectionN.TryGetValue("N3", out numberOfMedications);
                sectionN.TryGetValue("N4", out numberOfHerbalNutritionalSupplements);
                sectionN.TryGetValue("N5", out recentlyChangedMedications);
                sectionN.TryGetValue("N6", out selfReportNeedForMedicationReview);

                sectionN.TryGetValue("N7a", out antipsychoticLast7Days);
                sectionN.TryGetValue("N7b", out anxiolyticLast7Days);
                sectionN.TryGetValue("N7c", out antidepressantLast7Days);
                sectionN.TryGetValue("N7d", out hypnoticLast7Days);

                sectionN.TryGetValue("N8", out medicationByDailyInjection);
                sectionN.TryGetValue("N9", out cannabisUseTimeSinceUse);
                sectionN.TryGetValue("N10", out medicinalUseOfCannabis);
            }

            #endregion
            #region Section O
            parsedFile.AssessmentSections.TryGetValue("SECTION O", out var sectionO);
            if (sectionO != null)
            {
                sectionO.TryGetValue("O1a", out bloodPressure);
                sectionO.TryGetValue("O1b", out colonoscopy);
                sectionO.TryGetValue("O1c", out dentalExam);
                sectionO.TryGetValue("O1d", out eyeExam);
                sectionO.TryGetValue("O1e", out hearingExam);
                sectionO.TryGetValue("O1f", out influenzaVaccine);
                sectionO.TryGetValue("O1g", out mammogramOrBreastExam);
                sectionO.TryGetValue("O1h", out pneumovaxVaccine);

                sectionO.TryGetValue("O2a", out chemotherapy);
                sectionO.TryGetValue("O2b", out dialysis);
                sectionO.TryGetValue("O2c", out infectionControlSegregation);
                sectionO.TryGetValue("O2d", out ivMedication);
                sectionO.TryGetValue("O2e", out oxygenTherapy);
                sectionO.TryGetValue("O2f", out radiation);
                sectionO.TryGetValue("O2g", out suctioning);
                sectionO.TryGetValue("O2h", out tracheostomyCare);
                sectionO.TryGetValue("O2i", out transfusion);
                sectionO.TryGetValue("O2j", out ventilator);
                sectionO.TryGetValue("O2k", out woundCare);
                sectionO.TryGetValue("O2l", out scheduledToiletingProgram);
                sectionO.TryGetValue("O2m", out palliativeCare);
                sectionO.TryGetValue("O2n", out turningProgram);

                sectionO.TryGetValue("O3ab", out physicalTherapyDays);
                sectionO.TryGetValue("O3ac", out physicalTherapyMinutes);
                sectionO.TryGetValue("O3aa", out physicalTherapyScheduled);

                sectionO.TryGetValue("O3bb", out occupationalTherapyDays);
                sectionO.TryGetValue("O3bc", out occupationalTherapyMinutes);
                sectionO.TryGetValue("O3ba", out occupationalTherapyScheduled);

                sectionO.TryGetValue("O3cb", out speechLanguageTherapyDays);
                sectionO.TryGetValue("O3cc", out speechLanguageTherapyMinutes);
                sectionO.TryGetValue("O3ca", out speechLanguageTherapyScheduled);

                sectionO.TryGetValue("O3db", out respiratoryTherapyDays);
                sectionO.TryGetValue("O3dc", out respiratoryTherapyMinutes);
                sectionO.TryGetValue("O3da", out respiratoryTherapyScheduled);

                sectionO.TryGetValue("O3eb", out functionalRehabilitationDays);
                sectionO.TryGetValue("O3ec", out functionalRehabilitationMinutes);
                sectionO.TryGetValue("O3ea", out functionalRehabilitationScheduled);

                sectionO.TryGetValue("O3fb", out psychologicalTherapiesDays);
                sectionO.TryGetValue("O3fc", out psychologicalTherapiesMinutes);
                sectionO.TryGetValue("O3fa", out psychologicalTherapiesScheduled);

                sectionO.TryGetValue("O3gb", out recreationTherapyDays);
                sectionO.TryGetValue("O3gc", out recreationTherapyMinutes);
                sectionO.TryGetValue("O3ga", out recreationTherapyScheduled);

                sectionO.TryGetValue("O4a", out inpatientAcuteCareHospitalWithOvernightStay);
                sectionO.TryGetValue("O4b", out emergencyRoomVisit);

                sectionO.TryGetValue("O7a", out fullBedRails);
                sectionO.TryGetValue("O7b", out trunkRestraint);
                sectionO.TryGetValue("O7c", out chairPreventsRising);

                sectionO.TryGetValue("O5", out numberOfDaysPhysicianVisits);
                sectionO.TryGetValue("O6", out numberOfDaysPhysicianOrders);
            }


            #endregion
            #region Section P
            parsedFile.AssessmentSections.TryGetValue("SECTION P", out var sectionP);
            if (sectionP != null)
            {
                sectionP.TryGetValue("P1a", out decisionMakerPersonalCare);
                sectionP.TryGetValue("P1b", out decisionMakerProperty);

                sectionP.TryGetValue("P2a", out advanceDirectiveDoNotResuscitate);
                sectionP.TryGetValue("P2b", out advanceDirectiveDoNotHospitalize);
            }



            #endregion
            #region Section Q
            parsedFile.AssessmentSections.TryGetValue("SECTION Q", out var sectionQ);
            if (sectionQ != null)
            {
                sectionQ.TryGetValue("Q1a", out preferenceToReturnOrRemainInCommunity);
                sectionQ.TryGetValue("Q1b", out supportPersonPositiveAboutDischarge);
                sectionQ.TryGetValue("Q1c", out hasHousingAvailableInCommunity);
                sectionQ.TryGetValue("Q2", out expectedLengthOfStay);
            }


            #endregion
            #region Section R
            parsedFile.AssessmentSections.TryGetValue("SECTION R", out var sectionR);
            if (sectionR != null)
            {
                sectionR.TryGetValue("R1", out lastDayOfStay);
                sectionR.TryGetValue("R2", out residentialLivingStatusAfterDischarge);
                sectionR.TryGetValue("R4", out dischargeToFacilityNumber);
                sectionR.TryGetValue("R3", out homeCareServicesScheduledAtDischarge);
                sectionR.TryGetValue("R5", out covid19Status);
            }


            #endregion
            #region Section S
            parsedFile.AssessmentSections.TryGetValue("SECTION S", out var sectionS);
            if (sectionS != null)
            {
                sectionS.TryGetValue("S2", out assessmentSignedAsComplete);
            }

            #endregion

            parsedFile.Patient.TryGetValue("OrgID", out var orgId);
            var entry = new XElement(ns + "entry",
                new XElement(ns + "fullUrl", SafeAttr("value", $"urn:uuid:{questionnaireResponseId}")),
                new XElement(ns + "resource",
                    new XElement(ns + "QuestionnaireResponse",
                        new XAttribute("xmlns", ns),
                            new XElement(ns + "id",
                                SafeAttr("value", questionnaireResponseId)
                            ),

                        new XElement(ns + "questionnaire",
                            new XElement(ns + "reference",
                                SafeAttr("value", "Questionnaire/irrs-ltcf")
                            )
                        ),
                        //Status
                        //Questionnaire status
                        new XElement(ns + "status",
                            SafeAttr("value", "completed")
                        ),
                        // < !--Patient ID-- >
                        new XElement(ns + "subject",
                            new XElement(ns + "reference",
                                SafeAttr("value", $"urn:uuid:{patientId}")
                            )
                        ),
                            //< !--Encounter ID-- >
                            new XElement(ns + "context",
                            new XElement(ns + "reference",
                                SafeAttr("value", $"urn:uuid:{encounterId}")
                            )
                        ),
                        // Section A
                        new XElement(ns + "item",
                            new XElement(ns + "linkId",
                                SafeAttr("value", $"A")
                            ),
                            !string.IsNullOrWhiteSpace(genderIdentity)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", $"A2")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", $"A2b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", genderIdentity))
                                        )
                                    )
                                )
                            ) : null,
                            !string.IsNullOrWhiteSpace(assessmentReason)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", $"A8")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", assessmentReason))
                                    )
                                )
                            ) : null,
                            !string.IsNullOrWhiteSpace(assessmentRefDate)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", $"A9")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueDate", SafeAttr("value", assessmentRefDate))
                                )
                            ) : null,
                            !string.IsNullOrWhiteSpace(primaryCareGoal)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "A10g")),

                                // Primary Care Goal (always added if primaryCareGoal is not empty)
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "A10")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueString", SafeAttr("value", primaryCareGoal))
                                    )
                                ),

                                // Expressed Goal (only added if expressedCareGoal is not empty)
                                !string.IsNullOrWhiteSpace(expressedCareGoal)
                                    ? new XElement(ns + "item",
                                        new XElement(ns + "linkId", SafeAttr("value", "A10a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueString", SafeAttr("value", expressedCareGoal))
                                        )
                                    )
                                    : null
                            )
                            : null,
                            !string.IsNullOrWhiteSpace(timeSinceLastVisit)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", $"A11")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", timeSinceLastVisit))
                                    )
                                )
                            ) : null
                        ),
                        // Section B
                        new XElement(ns + "item",
                            new XElement(ns + "linkId",
                                SafeAttr("value", $"B")
                            ),
                            !string.IsNullOrWhiteSpace(levelOfControl)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "B1")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", levelOfControl))
                                    )
                                )
                            )
                            : null,
                            !string.IsNullOrWhiteSpace(indigenousIdentityFirstNations) ||
                            !string.IsNullOrWhiteSpace(indigenousIdentityMetis) ||
                            !string.IsNullOrWhiteSpace(indigenousIdentityInuit)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "B3")),
                                // First Nations Identity
                                !string.IsNullOrWhiteSpace(indigenousIdentityFirstNations)
                                    ? new XElement(ns + "item",
                                        new XElement(ns + "linkId", SafeAttr("value", "B3a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", SafeAttr("value", indigenousIdentityFirstNations))
                                            )
                                        )
                                    )
                                    : null,
                                // Metis Identity
                                !string.IsNullOrWhiteSpace(indigenousIdentityMetis)
                                    ? new XElement(ns + "item",
                                        new XElement(ns + "linkId", SafeAttr("value", "B3b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", SafeAttr("value", indigenousIdentityMetis))
                                            )
                                        )
                                    )
                                    : null,
                                // Inuit Identity
                                !string.IsNullOrWhiteSpace(indigenousIdentityInuit)
                                    ? new XElement(ns + "item",
                                        new XElement(ns + "linkId", SafeAttr("value", "B3c")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", SafeAttr("value", indigenousIdentityInuit))
                                            )
                                        )
                                    )
                                    : null
                                )
                                : null,
                            !string.IsNullOrWhiteSpace(residentialStatus)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "B5")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "B5c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", residentialStatus))
                                        )
                                    )
                                )
                            )
                            : null,
                            !string.IsNullOrWhiteSpace(priorLivingArrangement)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "B7")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", priorLivingArrangement))
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
                                new XElement(ns + "linkId", SafeAttr("value", "B8")),
                                !string.IsNullOrWhiteSpace(residentialHistoryA)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "B8a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", residentialHistoryA))
                                        )
                                    )
                                )
                                : null,
                                !string.IsNullOrWhiteSpace(residentialHistoryB)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "B8b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", residentialHistoryB))
                                        )
                                    )
                                )
                                : null,
                                !string.IsNullOrWhiteSpace(residentialHistoryC)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "B8c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", residentialHistoryC))
                                        )
                                    )
                                )
                                : null,
                                !string.IsNullOrWhiteSpace(residentialHistoryD)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "B8d")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", residentialHistoryD))
                                        )
                                    )
                                )
                                : null,
                                !string.IsNullOrWhiteSpace(residentialHistoryE)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "B8e")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", residentialHistoryE))
                                        )
                                    )
                                )
                                : null,
                                !string.IsNullOrWhiteSpace(residentialHistoryF)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "B8f")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", residentialHistoryF))
                                        )
                                    )
                                )
                                : null
                            )
                            : null,
                            !string.IsNullOrWhiteSpace(historyMentalIllness)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "B9")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", historyMentalIllness))
                                    )
                                )
                            )
                            : null,
                            !string.IsNullOrWhiteSpace(interpreterNeeded)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "B10")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", interpreterNeeded))
                                    )
                                )
                            )
                            : null
                        ),
                        // Section C
                        new XElement(ns + "item",
                            new XElement(ns + "linkId",
                                SafeAttr("value", $"C")
                            ),
                            !string.IsNullOrWhiteSpace(timeSinceLastVisit)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", $"C1")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", timeSinceLastVisit))
                                    )
                                )
                            ) : null,
                            !string.IsNullOrWhiteSpace(shortTermMemory) ||
                            !string.IsNullOrWhiteSpace(longTermMemory) ||
                            !string.IsNullOrWhiteSpace(proceduralMemory) ||
                            !string.IsNullOrWhiteSpace(SituationalMemory)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "C2")),
                                !string.IsNullOrWhiteSpace(shortTermMemory)
                                    ? new XElement(ns + "item",
                                        new XElement(ns + "linkId", SafeAttr("value", "C2a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", SafeAttr("value", shortTermMemory))
                                            )
                                        )
                                    )
                                    : null,
                                !string.IsNullOrWhiteSpace(longTermMemory)
                                    ? new XElement(ns + "item",
                                        new XElement(ns + "linkId", SafeAttr("value", "C2b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", SafeAttr("value", longTermMemory))
                                            )
                                        )
                                    )
                                    : null,
                                !string.IsNullOrWhiteSpace(proceduralMemory)
                                    ? new XElement(ns + "item",
                                        new XElement(ns + "linkId", SafeAttr("value", "C2c")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", SafeAttr("value", proceduralMemory))
                                            )
                                        )
                                    )
                                    : null,
                                !string.IsNullOrWhiteSpace(SituationalMemory)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "C2d")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", SituationalMemory))
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
                                new XElement(ns + "linkId", SafeAttr("value", "C3")),
                                !string.IsNullOrWhiteSpace(easilyDistracted)
                                    ? new XElement(ns + "item",
                                        new XElement(ns + "linkId", SafeAttr("value", "C3a")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", SafeAttr("value", easilyDistracted))
                                            )
                                        )
                                    )
                                    : null,
                                !string.IsNullOrWhiteSpace(disorganizedSpeech)
                                    ? new XElement(ns + "item",
                                        new XElement(ns + "linkId", SafeAttr("value", "C3b")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", SafeAttr("value", disorganizedSpeech))
                                            )
                                        )
                                    )
                                    : null,
                                !string.IsNullOrWhiteSpace(varyingMentalFunction)
                                    ? new XElement(ns + "item",
                                        new XElement(ns + "linkId", SafeAttr("value", "C3c")),
                                        new XElement(ns + "answer",
                                            new XElement(ns + "valueCoding",
                                                new XElement(ns + "code", SafeAttr("value", varyingMentalFunction))
                                            )
                                        )
                                    )
                                    : null
                            ) : null,
                            !string.IsNullOrWhiteSpace(changeInMentalStatus)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", $"C4")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", changeInMentalStatus))
                                    )
                                )
                            ) : null,
                            !string.IsNullOrWhiteSpace(changeInDecisionMaking)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", $"C5")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", changeInDecisionMaking))
                                    )
                                )
                            ) : null
                        //Section C END
                        ),
                        // Section D
                        new XElement(ns + "item",
                            new XElement(ns + "linkId",
                                SafeAttr("value", $"D")
                            ),
                            !string.IsNullOrWhiteSpace(makingSelfUnderstood)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", $"D1")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", makingSelfUnderstood))
                                    )
                                )
                            ) : null,
                            !string.IsNullOrWhiteSpace(abilityToUnderstandOthers)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", $"D2")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", abilityToUnderstandOthers))
                                    )
                                )
                            ) : null,
                            !string.IsNullOrWhiteSpace(hearing) ||
                            !string.IsNullOrWhiteSpace(hearingAidUsed)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "D3")),
                                !string.IsNullOrWhiteSpace(hearing)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "D3a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", hearing))
                                        )
                                    )
                                )
                                : null,
                                !string.IsNullOrWhiteSpace(hearingAidUsed)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "D3b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", hearingAidUsed))
                                        )
                                    )
                                ) : null
                            ) : null,
                            !string.IsNullOrWhiteSpace(visionAdequateLight) ||
                            !string.IsNullOrWhiteSpace(visionApplianceUsed)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "D4")),
                                !string.IsNullOrWhiteSpace(visionAdequateLight)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "D4a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", visionAdequateLight))
                                        )
                                    )
                                )
                                : null,
                                !string.IsNullOrWhiteSpace(visionApplianceUsed)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "D4b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", visionApplianceUsed))
                                        )
                                    )
                                ) : null
                            ) : null
                        //Section D END
                        ),
                        // Section E
                        new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "E")),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "E1")),
                                !string.IsNullOrWhiteSpace(negativeStatements)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "E1a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", negativeStatements))
                                        )
                                    )
                                ) : null,
                                // E1b - Persistent Anger With Self or Others
                                !string.IsNullOrWhiteSpace(persistentAngerWithSelfOrOthers)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "E1b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", persistentAngerWithSelfOrOthers))
                                        )
                                    )
                                ) : null,
                                // E1c - Unrealistic Fears
                                !string.IsNullOrWhiteSpace(unrealisticFears)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "E1c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", unrealisticFears))
                                        )
                                    )
                                ) : null,
                                // E1d - Repetitive Health Complaints
                                !string.IsNullOrWhiteSpace(repetitiveHealthComplaints)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "E1d")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", repetitiveHealthComplaints))
                                        )
                                    )
                                ) : null,
                                // E1e - Repetitive Anxious Complaints/Concerns
                                !string.IsNullOrWhiteSpace(repetitiveAnxiousComplaints)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "E1e")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", repetitiveAnxiousComplaints))
                                        )
                                    )
                                ) : null,
                                // E1f - Sad, Pained or Worried Facial Expressions
                                !string.IsNullOrWhiteSpace(sadPainedOrWorriedFacialExpressions)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "E1f")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", sadPainedOrWorriedFacialExpressions))
                                        )
                                    )
                                ) : null,
                                // E1g - Crying, Tearfulness
                                !string.IsNullOrWhiteSpace(cryingTearfulness)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "E1g")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", cryingTearfulness))
                                        )
                                    )
                                ) : null,
                                // E1h - Recurrent Statements Something Terrible About to Happen
                                !string.IsNullOrWhiteSpace(recurrentStatementsTerribleAboutToHappen)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "E1h")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", recurrentStatementsTerribleAboutToHappen))
                                        )
                                    )
                                ) : null,
                                // E1i - Withdrawal From Activities of Interest
                                !string.IsNullOrWhiteSpace(withdrawalFromActivitiesOfInterest)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "E1i")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", withdrawalFromActivitiesOfInterest))
                                        )
                                    )
                                ) : null,
                                // E1j - Reduced Social Interactions
                                !string.IsNullOrWhiteSpace(reducedSocialInteractions)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "E1j")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", reducedSocialInteractions))
                                        )
                                    )
                                ) : null,
                                // E1k - Lack of Pleasure Expressions
                                !string.IsNullOrWhiteSpace(lackOfPleasureExpressions)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "E1k")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", lackOfPleasureExpressions))
                                        )
                                    )
                                ) : null
                            ),
                             new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "E2")),
                                // E2a - Self Report: Little Interest
                                !string.IsNullOrWhiteSpace(selfReportLittleInterest)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "E2a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", selfReportLittleInterest))
                                        )
                                    )
                                ) : null,
                                // E2b - Self Report: Anxious, Restless or Uneasy
                                !string.IsNullOrWhiteSpace(selfReportAnxiousRestlessUneasy)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "E2b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", selfReportAnxiousRestlessUneasy))
                                        )
                                    )
                                ) : null,
                                // E2c - Self Report: Sad, Depressed or Hopeless
                                !string.IsNullOrWhiteSpace(selfReportSadDepressedHopeless)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "E2c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", selfReportSadDepressedHopeless))
                                        )
                                    )
                                ) : null
                             ),
                              new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "E3")),
                                // E3a - Wandering
                                !string.IsNullOrWhiteSpace(wandering)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "E3a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", wandering))
                                        )
                                    )
                                ) : null,
                                // E3b - Verbal Abuse
                                !string.IsNullOrWhiteSpace(verbalAbuse)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "E3b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", verbalAbuse))
                                        )
                                    )
                                ) : null,
                                // E3c - Physical Abuse
                                !string.IsNullOrWhiteSpace(physicalAbuse)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "E3c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", physicalAbuse))
                                        )
                                    )
                                ) : null,
                                // E3d - Socially Inappropriate
                                !string.IsNullOrWhiteSpace(sociallyInappropriate)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "E3d")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", sociallyInappropriate))
                                        )
                                    )
                                ) : null,
                                // E3e - Inappropriate Sexual Behaviour
                                !string.IsNullOrWhiteSpace(inappropriateSexualBehaviour)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "E3e")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", inappropriateSexualBehaviour))
                                        )
                                    )
                                ) : null,
                                // E3f - Resists Care
                                !string.IsNullOrWhiteSpace(resistsCare)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "E3f")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", resistsCare))
                                        )
                                    )
                                ) : null
                            )
                        ),
                        // Section E END
                        // Section F
                        new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "F")),
                             new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "F1")),
                                // F1a - Social Relationships — Participation in Long-Standing Activities
                                !string.IsNullOrWhiteSpace(socialParticipationLongStandingActivities)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "F1a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", socialParticipationLongStandingActivities))
                                        )
                                    )
                                ) : null,
                                // F1b - Social Relationships — Visit From Long-Standing Relation/Family Member
                                !string.IsNullOrWhiteSpace(socialVisitLongStandingRelation)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "F1b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", socialVisitLongStandingRelation))
                                        )
                                    )
                                ) : null,
                                // F1c - Social Relationships — Other Interaction With Long-Standing Relation/Family Member
                                !string.IsNullOrWhiteSpace(socialOtherInteractionLongStandingRelation)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "F1c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", socialOtherInteractionLongStandingRelation))
                                        )
                                    )
                                ) : null
                             ),
                              new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "F2")),
                                // F2a - At Ease Interacting With Others
                                !string.IsNullOrWhiteSpace(atEaseInteractingWithOthers)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "F2a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", atEaseInteractingWithOthers))
                                        )
                                    )
                                ) : null,
                                // F2b - At Ease Doing Planned Activities
                                !string.IsNullOrWhiteSpace(atEaseDoingPlannedActivities)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "F2b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", atEaseDoingPlannedActivities))
                                        )
                                    )
                                ) : null,
                                // F2c - Accepts Invitations
                                !string.IsNullOrWhiteSpace(acceptsInvitations)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "F2c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", acceptsInvitations))
                                        )
                                    )
                                ) : null,
                                // F2d - Pursues Involvement in Activities of Facility/Community
                                !string.IsNullOrWhiteSpace(pursuesInvolvementActivities)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "F2d")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", pursuesInvolvementActivities))
                                        )
                                    )
                                ) : null,
                                // F2e - Initiates Interactions(s) With Others
                                !string.IsNullOrWhiteSpace(initiatesInteractionsWithOthers)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "F2e")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", initiatesInteractionsWithOthers))
                                        )
                                    )
                                ) : null,
                                // F2f - Reacts Positively to Interactions Initiated by Others
                                !string.IsNullOrWhiteSpace(reactsPositivelyToInteractions)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "F2f")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", reactsPositivelyToInteractions))
                                        )
                                    )
                                ) : null,
                                // F2g - Adjusts Easily to Change in Routine
                                !string.IsNullOrWhiteSpace(adjustsEasilyToChangeInRoutine)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "F2g")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", adjustsEasilyToChangeInRoutine))
                                        )
                                    )
                                ) : null
                            ),
                               new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "F3")),
                                // F3a - Conflict With Other Care Recipients
                                !string.IsNullOrWhiteSpace(conflictWithOtherCareRecipients)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "F3a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", conflictWithOtherCareRecipients))
                                        )
                                    )
                                ) : null,
                                // F3b - Conflict With Staff
                                !string.IsNullOrWhiteSpace(conflictWithStaff)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "F3b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", conflictWithStaff))
                                        )
                                    )
                                ) : null,
                                // F3c - Staff Frustration
                                !string.IsNullOrWhiteSpace(staffFrustration)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "F3c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", staffFrustration))
                                        )
                                    )
                                ) : null,
                                // F3d - Family or Friends Overwhelmed
                                !string.IsNullOrWhiteSpace(familyOrFriendsOverwhelmed)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "F3d")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", familyOrFriendsOverwhelmed))
                                        )
                                    )
                                ) : null,
                                // F3e - Lonely
                                !string.IsNullOrWhiteSpace(lonely)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "F3e")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", lonely))
                                        )
                                    )
                                ) : null
                            ),
                            // F4 - Major Life Stressors
                            !string.IsNullOrWhiteSpace(majorLifeStressors)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "F4")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", majorLifeStressors))
                                    )
                                )
                            ) : null,
                            new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "F5")),
                                // F5a - Consistent Positive Outlook
                                !string.IsNullOrWhiteSpace(consistentPositiveOutlook)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "F5a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", consistentPositiveOutlook))
                                        )
                                    )
                                ) : null,
                                // F5b - Finds Meaning in Day-to-Day Life
                                !string.IsNullOrWhiteSpace(findsMeaningInDayToDayLife)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "F5b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", findsMeaningInDayToDayLife))
                                        )
                                    )
                                ) : null,
                                // F5c - strongAndSupportiveRelationship
                                !string.IsNullOrWhiteSpace(strongAndSupportiveRelationship)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "F5c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", strongAndSupportiveRelationship))
                                        )
                                    )
                                ) : null
                            )
                        ),
                        // Section F END
                        // Section G
                        new XElement(ns + "item",
                        new XElement(ns + "linkId", SafeAttr("value", "G")),
                            new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "G1")),
                                !string.IsNullOrWhiteSpace(adlBathingPerformance)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "G1a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", adlBathingPerformance))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(adlPersonalHygienePerformance)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "G1b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", adlPersonalHygienePerformance))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(adlDressingUpperBodyPerformance)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "G1c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", adlDressingUpperBodyPerformance))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(adlDressingLowerBodyPerformance)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "G1d")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", adlDressingLowerBodyPerformance))
                                        )
                                    )
                                ) : null,
                                !string.IsNullOrWhiteSpace(adlWalkingPerformance)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "G1e")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", adlWalkingPerformance))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(adlLocomotionPerformance)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "G1f")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", adlLocomotionPerformance))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(adlTransferToiletPerformance)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "G1g")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", adlTransferToiletPerformance))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(adlToiletUsePerformance)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "G1h")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", adlToiletUsePerformance))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(adlBedMobilityPerformance)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "G1i")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", adlBedMobilityPerformance))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(adlEatingPerformance)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "G1j")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", adlEatingPerformance))
                                        )
                                    )
                                ) : null
                            ),
                            new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "G2")),
                                !string.IsNullOrWhiteSpace(primaryModeOfLocomotion)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "G2a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", primaryModeOfLocomotion))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(timedWalk)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "G2b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", timedWalk))
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(distanceWalked)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "G2c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", distanceWalked))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(distanceWheeledSelf)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "G2d")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", distanceWheeledSelf))
                                        )
                                    )
                                ) : null
                            ),
                            new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "G3")),
                                !string.IsNullOrWhiteSpace(totalHoursExerciseOrPhysicalActivity)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "G3a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", totalHoursExerciseOrPhysicalActivity))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(activityLevelDaysOutOfHouse)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "G3b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", activityLevelDaysOutOfHouse))
                                        )
                                    )
                                ) : null
                            ),
                            new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "G4")),
                                !string.IsNullOrWhiteSpace(personBelievesCanImprove)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "G4a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", personBelievesCanImprove))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(careProfessionalBelievesCanImprove)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "G4b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", careProfessionalBelievesCanImprove))
                                        )
                                    )
                                ) : null
                            ),
                            !string.IsNullOrWhiteSpace(changeInAdlStatus)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "G5")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", changeInAdlStatus))
                                    )
                                )
                            ) : null
                        ),
                        // Section G END
                        // Section H
                        new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "H")),

                            !string.IsNullOrWhiteSpace(bladderContinence)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "H1")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", bladderContinence))
                                    )
                                )
                            ) : null,

                            !string.IsNullOrWhiteSpace(urinaryCollectionDevice)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "H2")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", urinaryCollectionDevice))
                                    )
                                )
                            ) : null,

                            !string.IsNullOrWhiteSpace(bowelContinence)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "H3")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", bowelContinence))
                                    )
                                )
                            ) : null,

                            !string.IsNullOrWhiteSpace(ostomy)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "H4")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", ostomy))
                                    )
                                )
                            ) : null
                        ),
                        // Section H END
                        // Section I
                        new XElement(ns + "item",
                        new XElement(ns + "linkId", SafeAttr("value", "I")),
                            new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "I1")),
                                !string.IsNullOrWhiteSpace(hipFracture)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I1a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", hipFracture))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(otherFracture)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I1b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", otherFracture))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(alzheimers)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I1c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", alzheimers))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(otherDementia)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I1d")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", otherDementia))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(hemiplegia)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I1e")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", hemiplegia))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(multipleSclerosis)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I1f")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", multipleSclerosis))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(paraplegia)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I1g")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", paraplegia))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(parkinsons)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I1h")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", parkinsons))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(quadriplegia)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I1i")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", quadriplegia))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(strokeCva)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I1j")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", strokeCva))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(coronaryHeartDisease)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I1k")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", coronaryHeartDisease))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(copd)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I1l")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", copd))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(congestiveHeartFailure)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I1m")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", congestiveHeartFailure))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(anxiety)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I1n")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", anxiety))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(bipolar)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I1o")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", bipolar))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(depression)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I1p")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", depression))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(schizophrenia)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I1q")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", schizophrenia))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(pneumonia)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I1r")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", pneumonia))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(urinaryTractInfection)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I1s")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", urinaryTractInfection))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(cancer)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I1t")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", cancer))
                                        )
                                    )
                                ) : null,

                                !string.IsNullOrWhiteSpace(diabetesMellitus)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I1u")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", diabetesMellitus))
                                        )
                                    )
                                ) : null
                            ),
                            new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "I2")),
                                !string.IsNullOrWhiteSpace(diseaseCode)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I2aa")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", diseaseCode))
                                        )
                                    )
                                ) : null,
                                // I2ab 
                                !string.IsNullOrWhiteSpace(diseaseDiagnosisICD10)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I2ab")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding", SafeAttr("value", diseaseDiagnosisICD10))
                                    )
                                ) : null
                            ),
                            new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "I2")),
                                !string.IsNullOrWhiteSpace(diseaseCode_2)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I2ba")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", diseaseCode_2))
                                        )
                                    )
                                ) : null,
                                !string.IsNullOrWhiteSpace(diseaseDiagnosisICD10_2)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I2bb")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding", SafeAttr("value", diseaseDiagnosisICD10_2))
                                    )
                                ) : null
                            ),
                            new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "I2")),
                                !string.IsNullOrWhiteSpace(diseaseCode_3)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I2ca")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", diseaseCode_3))
                                        )
                                    )
                                ) : null,
                                !string.IsNullOrWhiteSpace(diseaseDiagnosisICD10_3)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I2cb")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding", SafeAttr("value", diseaseDiagnosisICD10_3))
                                    )
                                ) : null
                            ),
                            new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "I2")),
                                !string.IsNullOrWhiteSpace(diseaseCode_4)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I2da")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", diseaseCode_4))
                                        )
                                    )
                                ) : null,
                                !string.IsNullOrWhiteSpace(diseaseDiagnosisICD10_4)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I2db")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding", SafeAttr("value", diseaseDiagnosisICD10_4))
                                    )
                                ) : null
                            ),
                            new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "I2")),
                                !string.IsNullOrWhiteSpace(diseaseCode_5)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I2ea")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", diseaseCode_5))
                                        )
                                    )
                                ) : null,
                                !string.IsNullOrWhiteSpace(diseaseDiagnosisICD10_5)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I2eb")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding", SafeAttr("value", diseaseDiagnosisICD10_5))
                                    )
                                ) : null
                            ),
                            new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "I2")),
                                !string.IsNullOrWhiteSpace(diseaseCode_6)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I2fa")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", diseaseCode_6))
                                        )
                                    )
                                ) : null,
                                !string.IsNullOrWhiteSpace(diseaseDiagnosisICD10_6)
                                ? new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "I2fb")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding", SafeAttr("value", diseaseDiagnosisICD10_6))
                                    )
                                ) : null
                            )
                        ),
                        // Section I END
                        // Section J
                        new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "J")),

                            !string.IsNullOrWhiteSpace(fallsLast30Days) ||
                            !string.IsNullOrWhiteSpace(falls31To90DaysAgo) ||
                            !string.IsNullOrWhiteSpace(falls91To180DaysAgo)
                            ? new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "J1")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J1a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", fallsLast30Days))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J1b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", falls31To90DaysAgo))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J1c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", falls91To180DaysAgo))
                                        )
                                    )
                                )
                            ) : null,
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "J2")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J2a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", difficultyStanding))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J2b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", difficultyTurningAround))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J2c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", dizziness))
                                        )
                                    )
                                ),
                                    new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J2d")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", unsteadyGait))
                                        )
                                    )
                                ),
                                    new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J2e")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", chestPain))
                                        )
                                    )
                                ),
                                    new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J2f")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", difficultyClearingAirway))
                                        )
                                    )
                                ),
                                        new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J2g")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", abnormalThoughtProcess))
                                        )
                                    )
                                ),
                                            new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J2h")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", delusions))
                                        )
                                    )
                                ),
                                            new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J2i")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", hallucinations))
                                        )
                                    )
                                ),
                                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J2j")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", aphasia))
                                        )
                                    )
                                ),
                                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J2k")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", acidReflux))
                                        )
                                    )
                                ),
                                                    new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J2l")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", constipation))
                                        )
                                    )
                                ),
                                                    new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J2m")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", diarrhea))
                                        )
                                    )
                                ),
                                                        new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J2n")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", vomiting))
                                        )
                                    )
                                ),
                                                        new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J2o")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", difficultyFallingAsleepOrStayingAsleep))
                                        )
                                    )
                                ),
                                                            new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J2p")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", tooMuchSleep))
                                        )
                                    )
                                ),
                                                            new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J2q")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", aspiration))
                                        )
                                    )
                                ),
                                                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J2r")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", fever))
                                        )
                                    )
                                ),
                                                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J2s")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", giOrGuBleeding))
                                        )
                                    )
                                ),
                                                                    new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J2t")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", poorHygiene))
                                        )
                                    )
                                ),
                                    new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J2u")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", peripheralEdema))
                                        )
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "J3")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", dyspnea))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "J4")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", fatigue))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "J5")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J5a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", painFrequency))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J5b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", painIntensity))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J5c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", painConsistency))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J5d")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", breakthroughPain))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J5e")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", painControl))
                                        )
                                    )
                                )
                            ),
                                new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "J6")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J6a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", conditionsUnstable))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J6b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", acuteEpisodeOrFlareUp))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J6c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", endStageDisease))
                                        )
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "J7")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", selfReportedHealth))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "J8")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J8a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", smokesTobaccoDaily))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "J8b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", alcohol))
                                        )
                                    )
                                )
                            )
                        ),
                        // Section J END
                        // Section K
                        new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "K")),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "K1")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "K1a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", heightCentimetres))
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "K1b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueDecimal", SafeAttr("value", weightKilograms))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "K2")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "K2a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", weightLoss))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "K2b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", dehydrated))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "K2c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", fluidIntake))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "K2d")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", fluidOutputExceedsInput))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "K2e")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", decreaseInFoodOrFluid))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "K2f")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", ateOneOrFewerMeals))
                                        )
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "K3")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", modeOfNutritionalIntake))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "K4")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", parenteralIntake))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "K5")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "K5a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", wearsDenture))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "K5b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", brokenTeeth))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "K5c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", reportsMouthFacialPain))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "K5d")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", reportsHavingDryMouth))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "K5e")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", reportsDifficultyChewing))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "K5f")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", gumInflammation))
                                        )
                                    )
                                )
                            )
                        ),
                        // Section L
                        new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "L")),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "L1")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", mostSeverePressureUlcer))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "L2")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", priorPressureUlcer))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "L3")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", otherSkinUlcer))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "L4")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", majorSkinProblems))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "L5")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", skinTearsOrCuts))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "L6")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", otherSkinCondition))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "L7")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", footProblems))
                                    )
                                )
                            )
                        ),
                        // Section M
                        new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "M")),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "M1")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", timeInvolvedInActivities))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "M2")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "M2a")),
                                    new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", priorPressureUlcer))
                                    )
                                )),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "M2b")),
                                    new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", computerActivity))
                                    )
                                )),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "M2c")),
                                    new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", conversingTalkingOnPhone))
                                    )
                                )),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "M2d")),
                                    new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", craftsOrArts))
                                    )
                                )),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "M2e")),
                                    new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", dancing))
                                    )
                                )),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "M2f")),
                                    new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", reminiscingAboutLife))
                                    )
                                )),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "M2g")),
                                    new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", exerciseOrSports))
                                    )
                                )),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "M2h")),
                                    new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", gardeningOrPlants))
                                    )
                                )),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "M2i")),
                                    new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", helpingOthers))
                                    )
                                )),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "M2j")),
                                    new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", musicOrSinging))
                                    )
                                )),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "M2k")),
                                    new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", pets))
                                    )
                                )),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "M2l")),
                                    new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", reading))
                                    )
                                )),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "M2m")),
                                    new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", spiritualActivities))
                                    )
                                )),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "M2n")),
                                    new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", tripsOrShopping))
                                    )
                                )),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "M2o")),
                                    new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", walkingOrWheelingOutdoors))
                                    )
                                )),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "M2p")),
                                    new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", watchingTvOrListeningToRadio))
                                    )
                                ))
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "M3")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", timeAsleepDuringDay))
                                    )
                                )
                            )
                        ),
                        // Section N
                        new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "N")),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "N2")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", drugAllergy))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "N3")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueInteger", SafeAttr("value", numberOfMedications))
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "N4")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueInteger", SafeAttr("value", numberOfHerbalNutritionalSupplements))
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "N5")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", recentlyChangedMedications))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "N6")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", selfReportNeedForMedicationReview))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "N7")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "N7a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", antipsychoticLast7Days))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "N7b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", anxiolyticLast7Days))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "N7c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", antidepressantLast7Days))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "N7d")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", hypnoticLast7Days))
                                        )
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "N8")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", medicationByDailyInjection))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "N9")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", cannabisUseTimeSinceUse))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "N10")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", medicinalUseOfCannabis))
                                    )
                                )
                            )
                        ),
                        // Section O
                        new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "O")),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "O1")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O1a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", bloodPressure))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O1b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", colonoscopy))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O1c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", dentalExam))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O1d")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", eyeExam))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O1e")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", hearingExam))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O1f")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", influenzaVaccine))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O1g")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", mammogramOrBreastExam))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O1h")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", pneumovaxVaccine))
                                        )
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "O2")),
                                new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "O2a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", chemotherapy))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O2b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", dialysis))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O2c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", infectionControlSegregation))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O2d")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", ivMedication))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                        new XElement(ns + "linkId", SafeAttr("value", "O2e")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", oxygenTherapy))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                        new XElement(ns + "linkId", SafeAttr("value", "O2f")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", radiation))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                        new XElement(ns + "linkId", SafeAttr("value", "O2g")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", suctioning))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                        new XElement(ns + "linkId", SafeAttr("value", "O2h")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", tracheostomyCare))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                        new XElement(ns + "linkId", SafeAttr("value", "O2i")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", transfusion))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                        new XElement(ns + "linkId", SafeAttr("value", "O2j")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", ventilator))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                        new XElement(ns + "linkId", SafeAttr("value", "O2k")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", woundCare))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                        new XElement(ns + "linkId", SafeAttr("value", "O2l")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", scheduledToiletingProgram))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                        new XElement(ns + "linkId", SafeAttr("value", "O2m")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", palliativeCare))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                        new XElement(ns + "linkId", SafeAttr("value", "O2n")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", turningProgram))
                                        )
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "O3")),
                                new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "O3aa")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", physicalTherapyScheduled))
                                    )
                                ),
                                new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "O3ab")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", physicalTherapyDays))
                                    )
                                ),
                                new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "O3ac")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", physicalTherapyMinutes))
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O3ba")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", occupationalTherapyScheduled))
                                    )
                                ),
                                new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "O3bb")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", occupationalTherapyDays))
                                    )
                                ),
                                new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "O3bc")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", occupationalTherapyMinutes))
                                    )
                                ),
                                 new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O3ca")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", speechLanguageTherapyScheduled))
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O3cb")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", speechLanguageTherapyDays))
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O3cc")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", speechLanguageTherapyMinutes))
                                    )
                                ),
                               new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O3da")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", respiratoryTherapyScheduled))
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O3db")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", respiratoryTherapyDays))
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O3dc")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", respiratoryTherapyMinutes))
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O3ea")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", functionalRehabilitationScheduled))
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O3eb")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", functionalRehabilitationDays))
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O3ec")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", functionalRehabilitationMinutes))
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O3fa")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", psychologicalTherapiesScheduled))
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O3fb")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", psychologicalTherapiesDays))
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O3fc")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", psychologicalTherapiesMinutes))
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O3ga")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", recreationTherapyScheduled))
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O3gb")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", recreationTherapyDays))
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O3gc")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", recreationTherapyMinutes))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "O4")),
                                new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "O4a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", inpatientAcuteCareHospitalWithOvernightStay))
                                    )
                                ),
                                new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "O4b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueInteger", SafeAttr("value", emergencyRoomVisit))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "O5")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueInteger", SafeAttr("value", numberOfDaysPhysicianVisits))
                                )
                            ),
                            new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", "O6")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueInteger", SafeAttr("value", numberOfDaysPhysicianOrders))
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", "O7")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O7a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", fullBedRails))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O7b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", trunkRestraint))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", "O7c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", chairPreventsRising))
                                        )
                                    )
                                )
                            )
                        ),
                        // Section P
                        new XElement(ns + "item",
                            new XElement(ns + "linkId",
                                SafeAttr("value", $"P")
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", $"P1")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", $"P1a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", decisionMakerPersonalCare))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", $"P1b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", decisionMakerProperty))
                                        )
                                    )
                                )
                            ),
                                new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", $"P2")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", $"P2a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", decisionMakerPersonalCare))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", $"P2b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", decisionMakerProperty))
                                        )
                                    )
                                )
                            )
                        ),
                        // Section Q
                        new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", $"Q")),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", $"Q1")),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", $"Q1a")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", preferenceToReturnOrRemainInCommunity))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", $"Q1b")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", supportPersonPositiveAboutDischarge))
                                        )
                                    )
                                ),
                                new XElement(ns + "item",
                                    new XElement(ns + "linkId", SafeAttr("value", $"Q1c")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueCoding",
                                            new XElement(ns + "code", SafeAttr("value", hasHousingAvailableInCommunity))
                                        )
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", $"Q2")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", expectedLengthOfStay))
                                    )
                                )
                            )
                        ),
                        // Section R
                        new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", $"R")),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", $"R1")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", lastDayOfStay))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", $"R2")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", residentialLivingStatusAfterDischarge))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", $"R3")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", dischargeToFacilityNumber))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", $"R4")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", homeCareServicesScheduledAtDischarge))
                                    )
                                )
                            ),
                            new XElement(ns + "item",
                            new XElement(ns + "linkId", SafeAttr("value", $"R5")),
                                new XElement(ns + "answer",
                                    new XElement(ns + "valueCoding",
                                        new XElement(ns + "code", SafeAttr("value", covid19Status))
                                    )
                                )
                            )
                        ),
                        // Section S
                        new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", $"S")),
                                new XElement(ns + "item",
                                new XElement(ns + "linkId", SafeAttr("value", $"S2")),
                                    new XElement(ns + "answer",
                                        new XElement(ns + "valueDate", SafeAttr("value", assessmentSignedAsComplete))
                                    )
                                )
                                )
                        )
                    ),
                    new XElement(ns + "request",
                        new XElement(ns + "method", SafeAttr("value", "POST")),
                        new XElement(ns + "url", new XAttribute("value", $"urn:uuid:{questionnaireResponseId}"))
                    )
                );

            // Prune empty sections/items/answers with missing values
            PruneQuestionnaireResponseEntry(entry);

            return entry;
        }

        /// <summary>
        /// Builds the header for a FHIR QuestionnaireResponse Bundle using parsed flat file data.
        /// </summary>
        /// <param name="parsedFile"></param>
        /// <returns></returns>
        public XElement BuildQuestionnaireResponseBundleHeader(
            ParsedFlatFile parsedFile,
            string bundleId,
            string patientId,
            string encounterId,
            string questionnaireResponseId)
        {
            XAttribute SafeAttr(XName name, string value) => string.IsNullOrWhiteSpace(value) ? null : new XAttribute(name, value);

            // Generate unique IDs for resources
            bundleId = string.IsNullOrEmpty(bundleId) ? Guid.NewGuid().ToString() : bundleId;
            patientId = string.IsNullOrEmpty(patientId) ? Guid.NewGuid().ToString() : patientId;
            encounterId = string.IsNullOrEmpty(encounterId) ? Guid.NewGuid().ToString() : encounterId;
            questionnaireResponseId = string.IsNullOrEmpty(questionnaireResponseId) ? Guid.NewGuid().ToString() : questionnaireResponseId;

            parsedFile.Patient.TryGetValue("OrgID", out var orgId);

            return new XElement(ns + "Bundle", new XAttribute("xmlns", ns),
                new XElement(ns + "id", SafeAttr("value", bundleId)),
                new XElement(ns + "type", SafeAttr("value", "transaction")),
                BuildQuestionnaireResponseEntry(
                    parsedFile,
                    patientId,
                    encounterId,
                    questionnaireResponseId)
            );
        }

        /// <summary>
        /// Builds a FHIR Bundle containing a Patient resource using parsed flat file data.
        /// </summary>
        /// <param name="parsedFile">The parsed flat file containing patient data.</param>
        /// <returns>XDocument representing the FHIR Bundle XML.</returns>
        public XDocument BuildQuestionnaireResponseBundle(ParsedFlatFile parsedFile)
        {
            if (parsedFile == null)
            {
                throw new ArgumentNullException(nameof(parsedFile), "Parsed flat file cannot be null.");
            }
            XElement bundle = BuildQuestionnaireResponseBundleHeader(
                parsedFile,
                null,
                null,
                null,
                null);
            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), bundle);
        }
    }
}
