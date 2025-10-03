/*
 * FILE          : Program.cs
 * PROJECT       : IFIC-XML
 * PROGRAMMER    : Darryl Poworoznyk
 * FIRST VERSION : 2025-08-02
 * DESCRIPTION   :
 *   Main entry point for the IFIC Runner.
 *
 *   Modes:
 *     - Default Mode:
 *         Reads all *.dat files from the Queued folder (oldest → newest),
 *         parses them, builds FHIR Patient/Encounter/Questionnaire resources,
 *         saves and submits to CIHI, then moves each source file to
 *         Processed/Errored under <Fiscal>\QX-YYYY.
 *
 *     - Simulation Mode (--simulate <filename>):
 *         Submits an existing XML file to CIHI without reading queued files.
 *
 *   Configuration:
 *     - Root folder (TransmitRoot) is defined in appsettings.json:
 *         {
 *           "TransmitRoot": "C:\\Data\\LTCF Transmit"
 *         }
 *     - Program looks for input in <TransmitRoot>\Queued.
 *     - If not set, falls back to a repo-relative default path.
 */
using IFIC.ClarityClient;   // ADO.NET client + options
using IFIC.FileIngestor;   // ClarityLTCFUpdateService + AdminMetadataKeys
using IFIC.ApiClient;
using IFIC.Auth;
using IFIC.FileIngestor.Models;      
using IFIC.FileIngestor.Parsers;
using IFIC.FileIngestor.Transformers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Runtime.CompilerServices;

namespace IFIC.Runner
{
    public class Program
    {
        /// <summary>
        /// Application entry point. Sets up DI, logging, and executes either simulation mode
        /// (submit existing XML) or default mode (process ALL queued .dat files).
        /// TransmitRoot is read from appsettings.json (key: "TransmitRoot") with a safe fallback.
        /// </summary>
        /// <param name="args">Command-line arguments. Use "--simulate &lt;filename&gt;" to submit an existing XML from SampleXML.</param>
        public static async Task Main(string[] args)
        {
            Dictionary<string, string> SavedIdsByKey = new Dictionary<string, string>();
            Console.WriteLine("===== IFIC Runner Starting =====");

            IHost host = CreateHostBuilder(args).Build();
            var ltcfClient = host.Services.GetRequiredService<IClarityClient>();

            bool dbOk = await ltcfClient.PingDatabaseAsync(CancellationToken.None);

            if (dbOk)
            {
                Console.WriteLine("Clarity LTCF database connection succeeded.");
            }
            else
            {
                Console.WriteLine("Failed to connect to Clarity LTCF database. Check connection string.");
            }


            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var apiClient = host.Services.GetRequiredService<IApiClient>();
            var ltcfUpdateService = host.Services.GetRequiredService<ClarityLTCFUpdateService>();
            var config = host.Services.GetRequiredService<IConfiguration>();
            if (config != null && apiClient != null)
            {
                // Per-run output + logs
                string baseDir = AppContext.BaseDirectory;
                string outputFolder = Path.Combine(baseDir, "Output");
                Directory.CreateDirectory(outputFolder);

                string logFolder = Path.Combine(baseDir, "Logs");
                Directory.CreateDirectory(logFolder);

                string runTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string runLogFile = Path.Combine(logFolder, $"runlog_{runTimestamp}.txt");

                try
                {
                    // SIMULATION MODE: Submit an existing XML (core logic unchanged)
                    if (args.Length >= 2 && args[0].Equals("--simulate", StringComparison.OrdinalIgnoreCase))
                    {
                        string xmlFileName = args[1];
                        string sampleFolder = Path.Combine(baseDir, "..", "..", "..", "..", "IFIC.Runner", "SampleXML");
                        string xmlFilePath = Path.Combine(sampleFolder, xmlFileName);

                        logger.LogInformation("[SIMULATE] Submitting {File} to CIHI API...", xmlFilePath);
                        File.AppendAllText(runLogFile, $"Simulation mode. Submitting file: {xmlFilePath}{Environment.NewLine}");

                        if (!File.Exists(xmlFilePath))
                        {
                            logger.LogError("Simulation XML file not found: {Path}", xmlFilePath);
                            File.AppendAllText(runLogFile, $"ERROR: XML file not found: {xmlFilePath}{Environment.NewLine}");
                            return;
                        }

                        string xmlContent = await File.ReadAllTextAsync(xmlFilePath);
                        if (!xmlContent.TrimStart().StartsWith("<?xml"))
                        {
                            xmlContent = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + xmlContent;
                        }

                        SaveWithDeclaration(xmlFilePath, xmlContent);
                        await apiClient.SubmitXmlAsync(xmlContent);

                        if (!string.IsNullOrWhiteSpace(IRRSApiClient.clientMessage))
                        {
                            File.AppendAllText(runLogFile, IRRSApiClient.clientMessage + Environment.NewLine);
                        }

                        logger.LogInformation("Simulation completed successfully.");
                        File.AppendAllText(runLogFile, "Simulation completed successfully." + Environment.NewLine);
                        return;
                    }
                    // DEFAULT MODE: Process ALL .dat files in <TransmitRoot>\Queued
                    // TransmitRoot from appsettings.json (fallback if missing)
                    string transmitRoot = config["TransmitRoot"];
                    if (string.IsNullOrWhiteSpace(transmitRoot))
                    {
                        transmitRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "IFIC.Runner", "LTCF Transmit"));
                    }

                    string queuedFolder = Path.Combine(transmitRoot, "Queued");
                    Directory.CreateDirectory(queuedFolder);

                    var datFiles = GetQueuedDatFilesOldestFirst(queuedFolder);
                    if (datFiles.Count == 0)
                    {
                        logger.LogWarning("No .dat files found in Queued.");
                        File.AppendAllText(runLogFile, "No .dat files found in Queued." + Environment.NewLine);
                        return;
                    }

                    logger.LogInformation("TransmitRoot: {Root}", transmitRoot);
                    logger.LogInformation("Found {Count} queued .dat file(s).", datFiles.Count);
                    File.AppendAllText(runLogFile, $"TransmitRoot: {transmitRoot}{Environment.NewLine}");
                    File.AppendAllText(runLogFile, $"Found {datFiles.Count} queued .dat file(s).{Environment.NewLine}");

                    // Process each .dat oldest → newest
                    foreach (var datPath in datFiles)
                    {
                        string fileTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                        // Pre-derive minimal routing info from filename (fallbacks if ADMIN missing)
                        var initialFiscal = DeriveFiscalFromFileName(datPath) ?? "Unknown";
                        var initialQuarter = DeriveQuarterFromFileName(datPath) ?? "Q1";

                        try
                        {
                            logger.LogInformation("Processing flat file: {File}", datPath);
                            File.AppendAllText(runLogFile, $"Processing flat file: {datPath}{Environment.NewLine}");

                            var (passed, adminMeta) = await ProcessSingleFileAsync(
                                datPath,
                                outputFolder,
                                fileTimestamp,
                                apiClient,
                                logger,
                                runLogFile,
                                initialFiscal,
                                initialQuarter,
                                SavedIdsByKey,
                                ltcfUpdateService   
                            );


                            RouteDatFile(datPath, adminMeta, passed, transmitRoot);
                            logger.LogInformation("Routed {File} to {Status}.", Path.GetFileName(datPath), passed ? "Processed" : "Errored");

                            // move output <BUNDLE> xml to folder
                            RouteDatFile(outputFolder+"\\"+ Path.GetFileNameWithoutExtension(datPath) + ".xml", adminMeta, passed, transmitRoot);
                            logger.LogInformation("Routed {File} to {Status}.", Path.GetFileNameWithoutExtension(datPath)+".xml", passed ? "Processed" : "Errored");

                        }
                        catch (Exception ex)
                        {
                            File.AppendAllText(runLogFile, ex + Environment.NewLine);
                            logger.LogError(ex, "An error occurred during processing of {File}.", datPath);

                            try
                            {
                                // On catastrophic failure, route under filename-derived fiscal/quarter.
                                var fallbackMeta = new AdminMetadata
                                {
                                    Fiscal = initialFiscal,
                                    Quarter = $"{(NormalizeQuarter(initialQuarter) ?? initialQuarter)}-{initialFiscal}"
                                };
                                RouteDatFile(datPath, fallbackMeta, false, transmitRoot);
                                logger.LogInformation("Routed {File} to Errored.", Path.GetFileName(datPath));
                            }
                            catch (Exception routeEx)
                            {
                                logger.LogError(routeEx, "Routing failed for {File}", datPath);
                                File.AppendAllText(runLogFile, $"Routing failed for {datPath}: {routeEx}{Environment.NewLine}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    File.AppendAllText(runLogFile, ex + Environment.NewLine);
                    logger.LogError(ex, "A fatal error occurred.");
                }
            }
        }

        /*  
        * FUNCTION      : ProcessSingleFileAsync
        * DESCRIPTION   : Processes one .dat file end-to-end, including:
        *                 - parsing
        *                 - building the bundle
        *                 - submitting to CIHI
        *                 - evaluating PASS/FAIL
        *                 - updating Clarity LTCF tables via ClarityLTCFUpdateService
        * PARAMETERS    : string datPath
        *                 string outputFolder
        *                 string timestamp
        *                 IApiClient apiClient
        *                 ILogger logger
        *                 string runLogFile
        *                 string fallbackFiscal
        *                 string fallbackQuarterRaw
        *                 Dictionary<string,string> savedIdsByKey
        *                 ClarityLTCFUpdateService ltcfUpdateService   // NEW
        * RETURNS       : (bool Passed, AdminMetadata Admin)
        */
        private static async Task<(bool Passed, AdminMetadata Admin)> ProcessSingleFileAsync(
            string datPath,
            string outputFolder,
            string timestamp,
            IApiClient apiClient,
            ILogger logger,
            string runLogFile,
            string fallbackFiscal,
            string fallbackQuarterRaw,
            Dictionary<string, string> savedIdsByKey,
            ClarityLTCFUpdateService ltcfUpdateService)
        {
            // Parse the flat file into structured data
            var parser = new FlatFileParser();
            var parsedFile = parser.Parse(datPath);

            // pull the baseFilename out of the full "datPath" file - use this base name
            //  - NOTE: in order to keep the "baseFileName" string in scope - I couldn't do
            //          proper error checking on the lastBackSlash position found - assuming
            //          it will always find one :)
            //
            //  ** Sorry Darryl - I noticed you had some methods for finding the filename,
            //     fileextension, etc - after I wrote this code
            int lastBackSlash = datPath.LastIndexOf('\\');
            string fullDatFilename = datPath.Substring(lastBackSlash + 1);
            string baseFileName = fullDatFilename.Substring(0, (fullDatFilename.Length - 4));

            // also use the lastBackSlash (which points to the "Queued" folder to create the
            // "RunLogs" output folder for the CIHI response output
            string runLogsDir = datPath.Substring(0, (lastBackSlash - 7)) + "\\RunLogs";
            
            // Build strongly-typed ADMIN from the parser's dictionary
            AdminMetadata adminMeta = AdminMetadata.FromParsedFlatFile(parsedFile);

            // Normalize IDs/operations and push back into parsedFile.Admin so builders see the same values
            adminMeta = NormalizeAdminIdsAndOperations(adminMeta, parsedFile.Admin);

            // Ensure routing fields exist (prefer ADMIN; otherwise use filename fallbacks)
            var quarterOnly = NormalizeQuarter(adminMeta.Quarter) ?? NormalizeQuarter(fallbackQuarterRaw) ?? "Q1";
            var fiscalYear = string.IsNullOrWhiteSpace(adminMeta.Fiscal) ? fallbackFiscal : adminMeta.Fiscal!;
            adminMeta = new AdminMetadata
            {
                FhirPatID = adminMeta.FhirPatID,
                FhirPatKey = adminMeta.FhirPatKey,
                PatOper = adminMeta.PatOper,
                FhirEncID = adminMeta.FhirEncID,
                FhirEncKey = adminMeta.FhirEncKey,
                EncOper = adminMeta.EncOper,
                FhirAsmID = adminMeta.FhirAsmID,
                RecId = adminMeta.RecId,
                AsmOper = adminMeta.AsmOper,
                AsmType = adminMeta.AsmType,
                IsReturn = adminMeta.IsReturn,
                Fiscal = fiscalYear,
                Quarter = $"{quarterOnly}-{fiscalYear}"
            };

            // If not FIRST ASSESSMENT or RETURN, try to reuse existing CIHI IDs
            if (!string.Equals(adminMeta.AsmType, "FIRST ASSESSMENT", StringComparison.OrdinalIgnoreCase) && !string.Equals(adminMeta.AsmType, "RETURN ASSESSMENT", StringComparison.OrdinalIgnoreCase))
            {
                // Patient ID lookup
                if (!string.IsNullOrWhiteSpace(adminMeta.FhirPatKey) &&
                    savedIdsByKey.TryGetValue(adminMeta.FhirPatKey, out var cachedPatId))
                {
                    adminMeta.FhirPatID = cachedPatId;
                }

                // Encounter ID lookup
                if (!string.IsNullOrWhiteSpace(adminMeta.FhirEncKey) &&
                    savedIdsByKey.TryGetValue(adminMeta.FhirEncKey, out var cachedEncId))
                {
                    adminMeta.FhirEncID = cachedEncId;
                }

                // QuestionnaireResponse ID lookup
                if (!string.IsNullOrWhiteSpace(adminMeta.RecId) &&
                    savedIdsByKey.TryGetValue(adminMeta.RecId, out var cachedAsmId))
                {
                    adminMeta.FhirAsmID = cachedAsmId;
                }

                //Guard clause: UPDATE / DELETE requires cached IDs
                if ((string.Equals(adminMeta.PatOper, "UPDATE", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(adminMeta.PatOper, "DELETE", StringComparison.OrdinalIgnoreCase)) &&
                    string.IsNullOrWhiteSpace(adminMeta.FhirPatID))
                {
                    logger.LogError("Missing required Patient ID for {Op} operation.", adminMeta.PatOper);
                    File.AppendAllText(runLogFile, $"ERROR: Missing required Patient ID for {adminMeta.PatOper} operation.{Environment.NewLine}");
                    return (false, adminMeta);
                }

                if ((string.Equals(adminMeta.EncOper, "UPDATE", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(adminMeta.EncOper, "DELETE", StringComparison.OrdinalIgnoreCase)) &&
                    string.IsNullOrWhiteSpace(adminMeta.FhirEncID))
                {
                    logger.LogError("Missing required Encounter ID for {Op} operation.", adminMeta.EncOper);
                    File.AppendAllText(runLogFile, $"ERROR: Missing required Encounter ID for {adminMeta.EncOper} operation.{Environment.NewLine}");
                    return (false, adminMeta);
                }
            }
            // Build bundles as normal
            var patientBuilder = parsedFile.Patient.Any() ? new PatientXmlBuilder(adminMeta) : null;
            var encounterBuilder = parsedFile.Encounter.Any() ? new EncounterXmlBuilder(adminMeta) : null;
            var questionnaireResponseBuilder = parsedFile.AssessmentSections.Any() ? new QuestionnaireResponseBuilder(adminMeta) : null;

            //if (adminMeta.PatOper != "USE")
            //    patientBuilder?.BuildPatientBundle(parsedFile);
            //if (adminMeta.EncOper != "USE")
            //    encounterBuilder?.BuildEncounterBundle(parsedFile);
            //questionnaireResponseBuilder?.BuildQuestionnaireResponseBundle(parsedFile);

            // Build the final full bundle
            var bundleBuilder = new BundleXmlBuilder();
            var bundleResponseDoc = bundleBuilder.BuildFullBundle(
                parsedFile,
                patientBuilder,
                encounterBuilder,
                questionnaireResponseBuilder,
                adminMeta
            );

            bundleResponseDoc.Declaration = new XDeclaration("1.0", "UTF-8", null);

            // Save final bundle to disk
            //string outputPath = Path.Combine(outputFolder, $"fhir_bundle_{timestamp}.xml");

            // use the same baseFilename as the input data file
            string outputPath = Path.Combine(outputFolder, baseFileName+".xml");
            SaveWithDeclaration(outputPath, bundleResponseDoc.ToString());
            logger.LogInformation("FHIR Bundle saved to: {OutputPath}", outputPath);
            File.AppendAllText(runLogFile, $"FHIR Bundle saved: {outputPath}{Environment.NewLine}");

            // Submit the bundle
            logger.LogInformation("Submitting bundle to CIHI...");
            File.AppendAllText(runLogFile, $"Submitting bundle at: {DateTime.Now}{Environment.NewLine}");
            await apiClient.SubmitXmlAsync(bundleResponseDoc.ToString());

            string apiResponse = IRRSApiClient.clientMessage ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(apiResponse))
            {
                File.AppendAllText(runLogFile, apiResponse + Environment.NewLine);
            }

            // Decide pass/fail FROM the API RESPONSE  
            //  - had to pass "datPath" in so I could write out the "runlog_..." error in the 
            //    <catch> block - sorry Darryl :)
            bool passed = EvaluatePassFailFromApiResponse(apiResponse, datPath);

            // If it passes and creates new IDs → parse the response and update dictionary
            if (passed)
            {
                CihiResponseParser.ExtractResourceIds(
                    apiResponse,
                    out string? patId,
                    out string? encId,
                    out string? qId);

                adminMeta.FhirPatID = patId;
                adminMeta.FhirEncID = encId;
                adminMeta.FhirAsmID = qId;
                UpdateSavedIds(savedIdsByKey, adminMeta, patId, encId, qId);
            }

            logger.LogInformation("Submission evaluated as: {Status}", passed ? "PASS" : "FAIL");
            File.AppendAllText(runLogFile, $"Evaluated as {(passed ? "PASS" : "FAIL")}{Environment.NewLine}");

            int bundStart = apiResponse.IndexOf('<');
            string respBund = apiResponse.Substring(bundStart);
            string respFile = runLogsDir + "\\Errored\\runlog_" + baseFileName + ".xml";
            if(passed) respFile = runLogsDir + "\\Processed\\runlog_" + baseFileName + ".xml";
            SaveWithDeclaration(respFile, respBund);

            // SEANNIE
            // - update the LTCF SubmissionStatus table 
            //    "update SubmssionStatus set status='{PASS|FAIL}' where status='QUEUED'
            //    and rec_id="+adminMeta.RecId
            // ================================
            // NEW: Persist to Clarity LTCF DB
            // ================================

            // I write to the database using my business rules:
            // - On CREATE + PASS → write CIHI IDs for Patient/Encounter/Assessment
            // - On Assessment (CREATE/CORRECTION/DELETE) with rec_id → set SubmissionStatus = PASS/FAIL
            try
            {
                string finalStatus = passed ? "PASS" : "FAIL";
                await ltcfUpdateService.ApplyUpdatesAsync(
                    adminMeta, 
                    finalStatus,
                    IRRSApiClient.responseContent,
                    System.Threading.CancellationToken.None);
                File.AppendAllText(runLogFile, $"Clarity LTCF DB updated with status {finalStatus}.{Environment.NewLine}");
            }
            catch (Exception dbEx)
            {
                // I log and continue routing; DB write failures should not crash the run
                logger.LogError(dbEx, "Clarity LTCF update failed for {File}.", datPath);
                File.AppendAllText(runLogFile, $"ERROR: Clarity LTCF update failed: {dbEx}{Environment.NewLine}");
            }

            return (passed, adminMeta);
        }


        /// <summary>
        /// Updates or inserts CIHI resource IDs into the savedIdsByKey dictionary
        /// based on AdminMetadata keys (Patient, Encounter, QuestionnaireResponse).
        /// </summary>
        /// <param name="savedIdsByKey">Dictionary storing key → CIHI ID mappings.</param>
        /// <param name="adminMeta">AdminMetadata containing keys for lookup.</param>
        /// <param name="patientId">Optional Patient ID returned from CIHI (null if not returned).</param>
        /// <param name="encounterId">Optional Encounter ID returned from CIHI (null if not returned).</param>
        /// <param name="questionnaireId">Optional QuestionnaireResponse ID returned from CIHI (null if not returned).</param>
        public static void UpdateSavedIds(
            Dictionary<string, string> savedIdsByKey,
            AdminMetadata adminMeta,
            string? patientId,
            string? encounterId,
            string? questionnaireId)
        {
            if (savedIdsByKey == null) throw new ArgumentNullException(nameof(savedIdsByKey));
            if (adminMeta == null) throw new ArgumentNullException(nameof(adminMeta));

            // Patient
            if (!string.IsNullOrWhiteSpace(adminMeta.FhirPatKey) && !string.IsNullOrWhiteSpace(patientId))
            {
                savedIdsByKey[adminMeta.FhirPatKey] = patientId; // insert or update
            }

            // Encounter
            if (!string.IsNullOrWhiteSpace(adminMeta.FhirEncKey) && !string.IsNullOrWhiteSpace(encounterId))
            {
                savedIdsByKey[adminMeta.FhirEncKey] = encounterId; // insert or update
            }

            // QuestionnaireResponse
            if (!string.IsNullOrWhiteSpace(adminMeta.RecId) && !string.IsNullOrWhiteSpace(questionnaireId))
            {
                savedIdsByKey[adminMeta.RecId] = questionnaireId; // insert or update
            }
        }

        /// <summary>
        /// Ensures the three resource IDs and operations are set and consistent:
        /// - If ID is blank, generate a new UUID.
        /// - If operation is blank, default to USE (Patient/Encounter) or CREATE (QuestionnaireResponse).
        /// Also writes the normalized values back to <paramref name="adminDict"/> so builders that
        /// read parsedFile.Admin see the same values.
        /// </summary>
        /// <param name="adminMeta">Admin metadata created via AdminMetadata.FromParsedFlatFile(...).</param>
        /// <param name="adminDict">The raw parsedFile.Admin dictionary (may be null).</param>
        /// <returns>The updated AdminMetadata.</returns>
        /// <exception cref="ArgumentNullException">Thrown when adminMeta is null.</exception>
        private static AdminMetadata NormalizeAdminIdsAndOperations(AdminMetadata adminMeta, Dictionary<string, string>? adminDict)
        {
            if (adminMeta == null) throw new ArgumentNullException(nameof(adminMeta));

            adminDict ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            static (string id, string oper) Normalize(string? id, string? oper, string defaultOperIfBlank)
            {
                var idVal = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString() : id!.Trim();
                var operVal = string.IsNullOrWhiteSpace(oper) ? defaultOperIfBlank : oper!.Trim();
                return (idVal, operVal);
            }

            // Patient
            var (patId, patOper) = Normalize(adminMeta.FhirPatID, adminMeta.PatOper, "USE");
            // Encounter
            var (encId, encOper) = Normalize(adminMeta.FhirEncID, adminMeta.EncOper, "USE");
            // QuestionnaireResponse
            var (asmId, asmOper) = Normalize(adminMeta.FhirAsmID, adminMeta.AsmOper, "CREATE");

            // Push normalized values back to the dictionary for downstream code
            adminDict["fhirPatID"] = patId;
            adminDict["patOper"] = patOper;
            adminDict["fhirEncID"] = encId;
            adminDict["encOper"] = encOper;
            adminDict["fhirAsmID"] = asmId;
            adminDict["asmOper"] = asmOper;

            // Return a new AdminMetadata snapshot with normalized values
            return new AdminMetadata
            {
                FhirPatID = patId,
                FhirPatKey = adminMeta.FhirPatKey,
                PatOper = patOper,

                FhirEncID = encId,
                FhirEncKey = adminMeta.FhirEncKey,
                EncOper = encOper,

                FhirAsmID = asmId,
                RecId = adminMeta.RecId,
                AsmOper = asmOper,
                AsmType = adminMeta.AsmType,
                IsReturn = adminMeta.IsReturn,

                Fiscal = adminMeta.Fiscal,
                Quarter = adminMeta.Quarter
            };
        }

        /// <summary>
        /// Evaluates pass/fail using the API response text captured after submission.
        /// Rule: Must be a transaction-response Bundle; no OperationOutcome with error/fatal severity;
        /// all entry.response.status must be 2xx (e.g., 200, 201).
        /// </summary>
        /// <param name="apiResponse">Raw response text captured from the API client.</param>
        /// <returns>True if considered pass; false if considered fail.</returns>
        private static bool EvaluatePassFailFromApiResponse(string apiResponse, string datPath)
        {
            if (string.IsNullOrWhiteSpace(apiResponse))
            {
                return true;
            }

            try
            {
                int firstLt = apiResponse.IndexOf('<');
                string xml = firstLt >= 0 ? apiResponse.Substring(firstLt) : apiResponse;

                var ns = (XNamespace)"http://hl7.org/fhir";
                var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);

                var anyErrorOutcome =
                    doc.Descendants(ns + "OperationOutcome")
                       .Descendants(ns + "issue")
                       .Any(issue =>
                       {
                           var severity = issue.Element(ns + "severity")?.Attribute("value")?.Value?.Trim().ToLowerInvariant();
                           return severity == "error" || severity == "fatal";
                       });

                if (anyErrorOutcome)
                    return false;

                var bundle = doc.Root;
                if (bundle == null || bundle.Name != ns + "Bundle")
                    return false;

                var typeVal = bundle.Element(ns + "type")?.Attribute("value")?.Value?.Trim().ToLowerInvariant();
                if (typeVal != "transaction-response")
                    return false;

                var statuses =
                    bundle.Elements(ns + "entry")
                          .Select(e => e.Element(ns + "response"))
                          .Where(r => r != null)
                          .Select(r => r!.Element(ns + "status")?.Attribute("value")?.Value?.Trim())
                          .Where(v => !string.IsNullOrWhiteSpace(v))
                          .ToList();

                if (statuses.Count == 0)
                    return false;

                foreach (var s in statuses)
                {
                    if (!int.TryParse(s, out int code) || code < 200 || code >= 300)
                        return false;
                }

                return true;
            }
            catch
            {
                int lastBackSlash = datPath.LastIndexOf('\\');
                string fullDatFilename = datPath.Substring(lastBackSlash + 1);
                string baseFileName = fullDatFilename.Substring(0, (fullDatFilename.Length - 4));

                // also use the lastBackSlash (which points to the "Queued" folder to create the
                // "RunLogs" output folder for the CIHI response output
                string runLogsDir = datPath.Substring(0, (lastBackSlash - 7)) + "\\RunLogs";

                // since we are in a <catch> block, we are clearly in a FAIL position
                int bundStart = apiResponse.IndexOf('<');
                string respBund = apiResponse.Substring(bundStart);
                string respFile = runLogsDir + "\\Errored\\runlog_" + baseFileName + ".xml";
                //if (passed) respFile = runLogsDir + "\\Processed\\runlog_" + baseFileName + ".xml";
                SaveWithDeclaration(respFile, respBund);

                // SEANNIE
                // - update the LTCF SubmissionStatus table
                //    - in this case it is always FAIL?
                //    "update SubmssionStatus set status='{PASS|FAIL}' where status='QUEUED' and rec_id="+adminMeta.RecId


                var lower = apiResponse.ToLowerInvariant();
                if (lower.Contains("operationoutcome") && lower.Contains("severity") && (lower.Contains("error") || lower.Contains("fatal")))
                    return false;

                if (lower.Contains(" status value=\"4") || lower.Contains(" status value=\"5"))
                    return false;

                return true;
            }
        }

        /// <summary>
        /// Returns queued .dat files sorted from oldest to newest.
        /// Sorting prefers a timestamp in the filename (yyyyMMdd-HHmmss…); if not present,
        /// falls back to CreationTimeUtc.
        /// </summary>
        /// <param name="queuedFolder">Path to the Queued folder.</param>
        /// <returns>List of full file paths, sorted oldest → newest.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown if the Queued folder is missing.</exception>
        private static List<string> GetQueuedDatFilesOldestFirst(string queuedFolder)
        {
            if (!Directory.Exists(queuedFolder))
            {
                throw new DirectoryNotFoundException($"Queued folder not found: {queuedFolder}");
            }

            return new DirectoryInfo(queuedFolder)
                .GetFiles("*.dat", SearchOption.TopDirectoryOnly)
                .Select(f => new
                {
                    File = f,
                    Score = TryParseTimestampFromName(f.Name, out var ts) ? ts : f.CreationTimeUtc
                })
                .OrderBy(x => x.Score)
                .Select(x => x.File.FullName)
                .ToList();
        }

        /// <summary>
        /// Attempts to parse a UTC timestamp from a filename stem that begins with
        /// "yyyyMMdd-HHmmss..." (e.g., 20250726-163602117-...).
        /// </summary>
        /// <param name="name">Filename (with extension).</param>
        /// <param name="tsUtc">Parsed UTC DateTime if successful; otherwise DateTime.MinValue.</param>
        /// <returns>True if a timestamp was parsed; otherwise false.</returns>
        private static bool TryParseTimestampFromName(string name, out DateTime tsUtc)
        {
            tsUtc = DateTime.MinValue;
            var stem = Path.GetFileNameWithoutExtension(name);
            var parts = stem.Split('-');
            if (parts.Length < 2) return false;

            var datePart = parts[0]; // yyyyMMdd
            var timePart = parts[1]; // HHmmss...

            if (datePart.Length != 8) return false;
            if (!DateTime.TryParseExact(datePart, "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
                return false;

            if (timePart.Length < 6) return false;
            if (!int.TryParse(timePart.Substring(0, 2), out var H)) return false;
            if (!int.TryParse(timePart.Substring(2, 2), out var M)) return false;
            if (!int.TryParse(timePart.Substring(4, 2), out var S)) return false;

            try
            {
                tsUtc = new DateTime(date.Year, date.Month, date.Day, H, M, S, DateTimeKind.Utc);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Moves the processed .dat (and matching .xml, if present) into:
        /// &lt;TransmitRoot&gt;\&lt;Fiscal&gt;\QX-YYYY\Processed or \Errored.
        /// </summary>
        /// <param name="datPath">Full path to the .dat file.</param>
        /// <param name="admin">AdminMetadata for routing (Fiscal, Quarter formatted as "QX-YYYY").</param>
        /// <param name="passed">True to route to Processed; false to route to Errored.</param>
        /// <param name="transmitRoot">Root folder of the LTCF Transmit tree.</param>
        /// <exception cref="IOException">Thrown if file moves fail due to I/O errors.</exception>
        private static void RouteDatFile(string datPath, AdminMetadata admin, bool passed, string transmitRoot)
        {
            if (string.IsNullOrWhiteSpace(datPath) || !File.Exists(datPath))
                return;

            string fiscal = string.IsNullOrWhiteSpace(admin?.Fiscal) ? "Unknown" : admin!.Fiscal!;
            // Expect admin.Quarter formatted like "Q2-2025"; normalize if necessary
            string qOnly = NormalizeQuarter(admin?.Quarter) ?? "Q1";
            string quarterFolder = $"{qOnly}-{fiscal}";

            string statusFolder = passed ? "Processed" : "Errored";
            string destFolder = Path.Combine(transmitRoot, fiscal, quarterFolder, statusFolder);
            Directory.CreateDirectory(destFolder);

            // Move .dat
            string fileName = Path.GetFileName(datPath);
            string targetDat = Path.Combine(destFolder, fileName);
            File.Move(datPath, targetDat, overwrite: true);

            // Move matching .xml if present (same basename)
//            string xmlCandidate = Path.ChangeExtension(datPath, ".xml");
//            if (File.Exists(xmlCandidate))
//            {
//                string targetXml = Path.Combine(destFolder, Path.GetFileName(xmlCandidate));
//                File.Move(xmlCandidate, targetXml, overwrite: true);
//            }
        }

        /// <summary>
        /// Returns canonical "Q1".."Q4" for various quarter inputs (e.g., "1","Q2","Q3-2025","Quarter4").
        /// Returns null if unrecognized.
        /// </summary>
        /// <param name="q">Raw quarter string.</param>
        /// <returns>Normalized quarter ("Q1".."Q4") or null.</returns>
        private static string? NormalizeQuarter(string? q)
        {
            if (string.IsNullOrWhiteSpace(q)) return null;
            var s = q.Trim().ToUpperInvariant();

            // If it contains '-', try to extract the part that starts with Q
            if (s.Contains('-'))
            {
                var part = s.Split('-').FirstOrDefault(x => x.StartsWith("Q", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(part))
                    s = part;
            }

            if (s.StartsWith("Q"))
            {
                var n = new string(s.Skip(1).TakeWhile(char.IsDigit).ToArray());
                if (n == "1" || n == "01") return "Q1";
                if (n == "2" || n == "02") return "Q2";
                if (n == "3" || n == "03") return "Q3";
                if (n == "4" || n == "04") return "Q4";
            }
            else
            {
                var n = new string(s.TakeWhile(char.IsDigit).ToArray());
                if (n == "1" || n == "01") return "Q1";
                if (n == "2" || n == "02") return "Q2";
                if (n == "3" || n == "03") return "Q3";
                if (n == "4" || n == "04") return "Q4";
            }

            if (s.StartsWith("QUARTER"))
            {
                var n = new string(s.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
                if (n == "1") return "Q1";
                if (n == "2") return "Q2";
                if (n == "3") return "Q3";
                if (n == "4") return "Q4";
            }

            return null;
        }

        /// <summary>
        /// Derives Fiscal year (YYYY) from filename "yyyyMMdd-..." (first 4 digits), if possible.
        /// Returns null if not derivable.
        /// </summary>
        /// <param name="datPath">Path to .dat file.</param>
        /// <returns>Fiscal string or null.</returns>
        private static string? DeriveFiscalFromFileName(string datPath)
        {
            var name = Path.GetFileNameWithoutExtension(datPath);
            var parts = name.Split('-');
            if (parts.Length >= 1 && parts[0].Length >= 4)
            {
                var y = parts[0].Substring(0, 4);
                if (int.TryParse(y, out _)) return y;
            }
            return null;
        }

        /// <summary>
        /// Derives Quarter ("Q1".."Q4") from filename last token "-0N.dat" where 01..04 maps to Q1..Q4.
        /// Returns null if not derivable.
        /// </summary>
        /// <param name="datPath">Path to .dat file.</param>
        /// <returns>Quarter string or null.</returns>
        private static string? DeriveQuarterFromFileName(string datPath)
        {
            var name = Path.GetFileNameWithoutExtension(datPath);
            var parts = name.Split('-');
            if (parts.Length >= 4)
            {
                var last = parts[^1]; // e.g., "01"
                return NormalizeQuarter(last);
            }
            return null;
        }

        /// <summary>
        /// Saves XML to a file with declaration and UTF-8 encoding.
        /// </summary>
        /// <param name="xmlPath">Destination path.</param>
        /// <param name="xmlContent">XML string to save.</param>
        /// <exception cref="ArgumentNullException">Thrown if xmlPath or xmlContent is null/empty.</exception>
        /// <exception cref="XmlException">Thrown if the XML content cannot be parsed.</exception>
        /// <exception cref="IOException">Thrown if writing to disk fails.</exception>
        private static void SaveWithDeclaration(string xmlPath, string xmlContent)
        {
            if (string.IsNullOrWhiteSpace(xmlPath))
                throw new ArgumentNullException(nameof(xmlPath));
            if (string.IsNullOrWhiteSpace(xmlContent))
                throw new ArgumentNullException(nameof(xmlContent));

            var doc = XDocument.Parse(xmlContent, LoadOptions.PreserveWhitespace);
            doc.Declaration ??= new XDeclaration("1.0", "UTF-8", null);

            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                OmitXmlDeclaration = false
            };

            using var writer = XmlWriter.Create(xmlPath, settings);
            doc.Save(writer);
        }

        //
        // FUNCTION      : CreateHostBuilder
        // DESCRIPTION   : Configures configuration, services, and logging for the IFIC Runner.
        //                  Adds the Clarity LTCF database client (IClarityClient) and the
        //                  ClarityLTCFUpdateService so I can persist PASS/FAIL and CIHI IDs.
        // PARAMETERS    : string[] args : command-line arguments
        // RETURNS       : IHostBuilder  : configured host builder
        //
        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    config.AddEnvironmentVariables();
                })
                .ConfigureServices((context, services) =>
                {
                    // Existing services
                    services.AddHttpClient();
                    services.AddSingleton<IAuthManager, AuthManager>();
                    services.AddSingleton<IApiClient, IRRSApiClient>();

                    // ============================
                    // Clarity LTCF DB wiring (NEW)
                    // ============================
                    var clarityClientOptions = new IFIC.ClarityClient.ClarityClientOptions
                    {
                        ConnectionString = context.Configuration["ClarityClient:ConnectionString"] ?? "",
                        FhirIdMaxLength = int.TryParse(context.Configuration["ClarityClient:FhirIdMaxLength"], out var idLen) ? idLen : 60,
                        StatusMaxLength = int.TryParse(context.Configuration["ClarityClient:StatusMaxLength"], out var stLen) ? stLen : 10,
                        CommandTimeoutSec = int.TryParse(context.Configuration["ClarityClient:CommandTimeoutSec"], out var to) ? to : 15,
                        ElementMappingPath = context.Configuration["ClarityClient:ElementMappingPath"] ?? ""
                    };

                    // Register options + client + update service
                    services.AddSingleton(clarityClientOptions);
                    services.AddSingleton<IFIC.ClarityClient.IClarityClient, IFIC.ClarityClient.ClarityClient>();
                    services.AddSingleton<IFIC.FileIngestor.ClarityLTCFUpdateService>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                });

    }
}
