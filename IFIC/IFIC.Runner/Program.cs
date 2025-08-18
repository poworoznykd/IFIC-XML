/************************************************************************************
* FILE          : Program.cs
* PROJECT       : IFIC-XML
* PROGRAMMER    : Darryl Poworoznyk
* FIRST VERSION : 2025-08-02
* DESCRIPTION   :
*   Main entry point for IFIC Runner. Supports:
*     - Default Mode: Read ALL *.dat from Queued (oldest → newest) → Parse → Build FHIR
*                     Patient/Encounter/Questionnaire → Save + Submit to CIHI → Move
*                     each source file to Processed/Errored under <Fiscal>\<Quarter>.
*     - Simulation Mode (--simulate <filename>): Submits an existing XML file to CIHI.
*
*   CONFIGURATION:
*     - The root folder (TransmitRoot) is configurable via appsettings.json:
*         {
*           "TransmitRoot": "C:\\Data\\LTCF Transmit"
*         }
*     - The program then looks in: <TransmitRoot>\Queued
*     - If not set, it falls back to a repo-relative default path.
************************************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using IFIC.ApiClient;
using IFIC.Auth;
using IFIC.FileIngestor.Parsers;
using IFIC.FileIngestor.Transformers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using IFIC.FileIngestor.Models; // AdminMetadata

namespace IFIC.Runner
{
    public class Program
    {
        /// <summary>
        /// Application entry point. Sets up DI, logging, and executes either simulation mode
        /// (submit existing XML) or default mode (process ALL queued .dat files).
        /// TransmitRoot is read from appsettings.json (key: "TransmitRoot") with a safe fallback.
        /// </summary>
        /// <param name="args">Command-line arguments. Use "--simulate &lt;filename&gt;" to submit an existing XML.</param>
        public static async Task Main(string[] args)
        {
            Console.WriteLine("===== IFIC Runner Starting =====");

            IHost host = CreateHostBuilder(args).Build();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var apiClient = host.Services.GetRequiredService<IApiClient>();
            var config = host.Services.GetRequiredService<IConfiguration>();
            if (config == null || apiClient == null)
            {
                Console.Error.WriteLine("Configuration or ApiClient not available.");
                return;
            }

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
                // ----------------------------------------------------------------
                // SIMULATION MODE: Submit an existing XML (core logic unchanged)
                // ----------------------------------------------------------------
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

                // ----------------------------------------------------------------
                // DEFAULT MODE: Process ALL .dat files in <TransmitRoot>\Queued
                // ----------------------------------------------------------------

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

                    // Build ADMIN **before** processing so routing never falls back to Unknown
                    var initialAdmin = BuildInitialAdminMetadata(datPath, logger);

                    try
                    {
                        logger.LogInformation("Processing flat file: {File}", datPath);
                        File.AppendAllText(runLogFile, $"Processing flat file: {datPath}{Environment.NewLine}");

                        var (passed, finalAdmin) = await ProcessSingleFileAsync(
                            datPath,
                            outputFolder,
                            fileTimestamp,
                            apiClient,
                            logger,
                            runLogFile
                        );

                        // Prefer ADMIN from processing; if it is incomplete, fall back to the initial ADMIN we built up-front
                        var adminForRouting = CoalesceAdmin(finalAdmin, initialAdmin);

                        RouteDatFile(datPath, adminForRouting, passed, transmitRoot);
                        logger.LogInformation("Routed {File} to {Status}.", Path.GetFileName(datPath), passed ? "Processed" : "Errored");
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText(runLogFile, ex + Environment.NewLine);
                        logger.LogError(ex, "An error occurred during processing of {File}.", datPath);

                        try
                        {
                            // Use the initial ADMIN gathered before processing to avoid Unknown
                            RouteDatFile(datPath, admin: initialAdmin, passed: false, transmitRoot: transmitRoot);
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

        /// <summary>
        /// Processes a single .dat file:
        /// parses the flat file, builds Patient/Encounter/Questionnaire bundles,
        /// constructs the full Bundle XML, saves outputs, submits to CIHI,
        /// then evaluates pass/fail from the API response text. Returns AdminMetadata
        /// derived from the parsed ADMIN dictionary (never null; may be partial).
        /// </summary>
        /// <param name="datPath">Full path to the queued .dat file.</param>
        /// <param name="outputFolder">Folder where XML outputs are written.</param>
        /// <param name="timestamp">Timestamp for naming outputs.</param>
        /// <param name="apiClient">CIHI API client.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="runLogFile">Path to the run log file.</param>
        /// <returns>Tuple (Passed, Admin) where Admin is AdminMetadata for routing.</returns>
        /// <exception cref="IOException">Thrown on I/O failures while saving XML files.</exception>
        /// <exception cref="XmlException">Thrown if generated XML cannot be parsed or serialized.</exception>
        private static async Task<(bool Passed, AdminMetadata Admin)> ProcessSingleFileAsync(
            string datPath,
            string outputFolder,
            string timestamp,
            IApiClient apiClient,
            ILogger logger,
            string runLogFile)
        {
            // Parse the flat file into structured data
            var parser = new FlatFileParser();
            var parsedFile = parser.Parse(datPath);
            var adminDict = parsedFile.Admin ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Build patient, encounter, and questionnaire bundles if applicable
            var patientBuilder = parsedFile.Patient.Any() ? new PatientXmlBuilder() : null;
            var encounterBuilder = parsedFile.Encounter.Any() ? new EncounterXmlBuilder() : null;
            var questionnaireResponseBuilder = parsedFile.AssessmentSections.Any() ? new QuestionnaireResponseBuilder() : null;

            adminDict.TryGetValue("patOper", out var patOper);
            adminDict.TryGetValue("encOper", out var encOper);

            if (patOper != "USE")
                patientBuilder?.BuildPatientBundle(parsedFile);
            if (encOper != "USE")
                encounterBuilder?.BuildEncounterBundle(parsedFile);
            questionnaireResponseBuilder?.BuildQuestionnaireResponseBundle(parsedFile);

            // Build the final full bundle
            var bundleBuilder = new BundleXmlBuilder();
            var bundleResponseDoc = bundleBuilder.BuildFullBundle(
                parsedFile,
                patientBuilder,
                encounterBuilder,
                questionnaireResponseBuilder
            );

            bundleResponseDoc.Declaration = new XDeclaration("1.0", "UTF-8", null);

            // Save final bundle to disk (ensuring XML declaration)
            string outputPath = Path.Combine(outputFolder, $"fhir_bundle_{timestamp}.xml");
            SaveWithDeclaration(outputPath, bundleResponseDoc.ToString());
            logger.LogInformation("FHIR Bundle saved to: {OutputPath}", outputPath);
            File.AppendAllText(runLogFile, $"FHIR Bundle saved: {outputPath}{Environment.NewLine}");

            // Save probe copy
            var probePath = Path.Combine(outputFolder, $"_probe_sent_{timestamp}.xml");
            SaveWithDeclaration(probePath, bundleResponseDoc.ToString());

            // Submit the bundle
            logger.LogInformation("Submitting bundle to CIHI...");
            File.AppendAllText(runLogFile, $"Submitting bundle at: {DateTime.Now}{Environment.NewLine}");
            await apiClient.SubmitXmlAsync(bundleResponseDoc.ToString());

            // Capture API response text (if your client exposes it)
            string apiResponse = IRRSApiClient.clientMessage ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(apiResponse))
            {
                File.AppendAllText(runLogFile, apiResponse + Environment.NewLine);
            }

            // Decide pass/fail FROM the API RESPONSE
            bool passed = EvaluatePassFailFromApiResponse(apiResponse);

            logger.LogInformation("Submission evaluated as: {Status}", passed ? "PASS" : "FAIL");
            File.AppendAllText(runLogFile, $"Evaluated as {(passed ? "PASS" : "FAIL")}{Environment.NewLine}");

            // Convert dictionary ADMIN → AdminMetadata
            var adminMeta = ToAdminMetadata(adminDict, datPath);

            return (passed, adminMeta);
        }

        /// <summary>
        /// Evaluates pass/fail using the API response text captured after submission.
        /// Rule: Must be a transaction-response Bundle; no OperationOutcome with error/fatal severity;
        /// all entry.response.status must be 2xx (e.g., 200, 201).
        /// </summary>
        /// <param name="apiResponse">Raw response text captured from the API client.</param>
        /// <returns>True if considered pass; false if considered fail.</returns>
        private static bool EvaluatePassFailFromApiResponse(string apiResponse)
        {
            if (string.IsNullOrWhiteSpace(apiResponse))
            {
                // If client didn't provide a response, choose your default.
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
        /// Moves the processed .dat (and matching .xml, if present) into either
        /// &lt;TransmitRoot&gt;\&lt;Fiscal&gt;\QX-YYYY\Processed or \Errored.
        /// Falls back to Unknown if Admin metadata and filename parsing both fail.
        /// </summary>
        /// <param name="datPath">Full path to the .dat file.</param>
        /// <param name="admin">Admin metadata parsed from [ADMIN].</param>
        /// <param name="passed">True to route to Processed; false to route to Errored.</param>
        /// <param name="transmitRoot">Root folder of the LTCF Transmit tree.</param>
        private static void RouteDatFile(string datPath, AdminMetadata admin, bool passed, string transmitRoot)
        {
            if (string.IsNullOrWhiteSpace(datPath) || !File.Exists(datPath))
                return;

            // Normalize routing values
            string fiscal = string.IsNullOrWhiteSpace(admin?.Fiscal)
                ? DeriveFiscalFromFileName(datPath) ?? "Unknown"
                : admin!.Fiscal!;

            string quarter = string.IsNullOrWhiteSpace(admin?.Quarter)
                ? DeriveQuarterFromFileName(datPath) ?? "Unknown"
                : NormalizeQuarter(admin!.Quarter);

            // Build "Qx-YYYY" subfolder
            string quarterFiscalFolder = $"{quarter}-{fiscal}";

            string statusFolder = passed ? "Processed" : "Errored";

            // ✅ Final destination:
            // <TransmitRoot>\<Fiscal>\Qx-YYYY\Processed
            string destFolder = Path.Combine(transmitRoot, fiscal, quarterFiscalFolder, statusFolder);
            Directory.CreateDirectory(destFolder);

            // Move .dat
            string fileName = Path.GetFileName(datPath);
            string targetDat = Path.Combine(destFolder, fileName);
            File.Move(datPath, targetDat, overwrite: true);

            // Move matching .xml if present (same basename)
            string xmlCandidate = Path.ChangeExtension(datPath, ".xml");
            if (File.Exists(xmlCandidate))
            {
                string targetXml = Path.Combine(destFolder, Path.GetFileName(xmlCandidate));
                File.Move(xmlCandidate, targetXml, overwrite: true);
            }
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
            if (string.IsNullOrWhiteSpace(xmlPath)) throw new ArgumentNullException(nameof(xmlPath));
            if (string.IsNullOrWhiteSpace(xmlContent)) throw new ArgumentNullException(nameof(xmlContent));

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

        /// <summary>
        /// Configures DI, configuration, and logging.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>A configured host builder.</returns>
        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    config.AddEnvironmentVariables();
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddHttpClient();
                    services.AddSingleton<TokenService>();
                    services.AddSingleton<IAuthManager, AuthManager>();
                    services.AddSingleton<IApiClient, IRRSApiClient>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                });

        // ---------------------------
        // Admin helpers (new)
        // ---------------------------

        /// <summary>
        /// Builds initial AdminMetadata PRIOR to processing, by reading the queued .dat file's
        /// [ADMIN] section. If not found or incomplete, falls back to the filename pattern.
        /// </summary>
        /// <param name="datPath">Full path to .dat file.</param>
        /// <param name="logger">Logger.</param>
        /// <returns>AdminMetadata with at least Fiscal and Quarter when possible.</returns>
        private static AdminMetadata BuildInitialAdminMetadata(string datPath, ILogger logger)
        {
            var meta = new AdminMetadata();

            try
            {
                var lines = File.ReadAllLines(datPath);
                bool inAdmin = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (line.Length == 0) continue;

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        inAdmin = string.Equals(line, "[ADMIN]", StringComparison.OrdinalIgnoreCase);
                        if (!inAdmin && meta.Fiscal != null && meta.Quarter != null)
                            break; // we got what we need
                        continue;
                    }

                    if (!inAdmin) continue;
                    // Parse key=value
                    var eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    var key = line.Substring(0, eq).Trim();
                    var val = line[(eq + 1)..].Trim();

                    switch (key.ToLowerInvariant())
                    {
                        case "fhirpatid": meta = meta with { FhirPatID = val }; break;
                        case "fhirpatkey": meta = meta with { FhirPatKey = val }; break;
                        case "patoper": meta = meta with { PatOper = val }; break;
                        case "fhirencid": meta = meta with { FhirEncID = val }; break;
                        case "fhirenckey": meta = meta with { FhirEncKey = val }; break;
                        case "encoper": meta = meta with { EncOper = val }; break;
                        case "fhirasmid": meta = meta with { FhirAsmID = val }; break;
                        case "recid": meta = meta with { RecId = val }; break;
                        case "asmoper": meta = meta with { AsmOper = val }; break;
                        case "asmtype": meta = meta with { AsmType = val }; break;
                        case "fiscal": meta = meta with { Fiscal = val }; break;
                        case "quarter": meta = meta with { Quarter = val }; break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to pre-parse [ADMIN] from {File}; will fall back to filename for routing.", datPath);
            }

            // Fallbacks from filename if missing
            if (string.IsNullOrWhiteSpace(meta.Fiscal))
            {
                meta = meta with { Fiscal = DeriveFiscalFromFileName(datPath) };
            }
            if (string.IsNullOrWhiteSpace(meta.Quarter))
            {
                meta = meta with { Quarter = DeriveQuarterFromFileName(datPath) };
            }

            return meta;
        }

        /// <summary>
        /// Converts a parsed ADMIN dictionary into AdminMetadata. Falls back to filename
        /// for missing Fiscal/Quarter.
        /// </summary>
        /// <param name="admin">Dictionary parsed by the FlatFileParser.</param>
        /// <param name="datPath">File path for fallbacks.</param>
        /// <returns>AdminMetadata instance.</returns>
        private static AdminMetadata ToAdminMetadata(Dictionary<string, string> admin, string datPath)
        {
            admin ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            admin.TryGetValue("FhirPatID", out var fhirPatId);
            admin.TryGetValue("FhirPatKey", out var fhirPatKey);
            admin.TryGetValue("patOper", out var patOper);

            admin.TryGetValue("FhirEncID", out var fhirEncId);
            admin.TryGetValue("FhirEncKey", out var fhirEncKey);
            admin.TryGetValue("encOper", out var encOper);

            admin.TryGetValue("FhirAsmID", out var fhirAsmId);
            admin.TryGetValue("recId", out var recId);
            admin.TryGetValue("asmOper", out var asmOper);

            admin.TryGetValue("asmType", out var asmType);

            admin.TryGetValue("fiscal", out var fiscal);
            admin.TryGetValue("quarter", out var quarter);

            if (string.IsNullOrWhiteSpace(fiscal))
                fiscal = DeriveFiscalFromFileName(datPath);

            if (string.IsNullOrWhiteSpace(quarter))
                quarter = DeriveQuarterFromFileName(datPath);

            return new AdminMetadata
            {
                FhirPatID = fhirPatId,
                FhirPatKey = fhirPatKey,
                PatOper = patOper,
                FhirEncID = fhirEncId,
                FhirEncKey = fhirEncKey,
                EncOper = encOper,
                FhirAsmID = fhirAsmId,
                RecId = recId,
                AsmOper = asmOper,
                AsmType = asmType,
                Fiscal = fiscal,
                Quarter = quarter
            };
        }

        /// <summary>
        /// Coalesces two AdminMetadata instances, preferring non-empty values from primary;
        /// falls back to secondary for missing fields. Ensures Fiscal and Quarter are filled if possible.
        /// </summary>
        /// <param name="primary">Preferred (e.g., from parse).</param>
        /// <param name="secondary">Fallback (e.g., from pre-scan/filename).</param>
        /// <returns>Combined AdminMetadata.</returns>
        private static AdminMetadata CoalesceAdmin(AdminMetadata primary, AdminMetadata secondary)
        {
            // Prefer primary entries; if null/empty, use secondary
            string pick(string? a, string? b) => !string.IsNullOrWhiteSpace(a) ? a! : (b ?? string.Empty);

            var merged = new AdminMetadata
            {
                FhirPatID = pick(primary?.FhirPatID, secondary?.FhirPatID),
                FhirPatKey = pick(primary?.FhirPatKey, secondary?.FhirPatKey),
                PatOper = pick(primary?.PatOper, secondary?.PatOper),
                FhirEncID = pick(primary?.FhirEncID, secondary?.FhirEncID),
                FhirEncKey = pick(primary?.FhirEncKey, secondary?.FhirEncKey),
                EncOper = pick(primary?.EncOper, secondary?.EncOper),
                FhirAsmID = pick(primary?.FhirAsmID, secondary?.FhirAsmID),
                RecId = pick(primary?.RecId, secondary?.RecId),
                AsmOper = pick(primary?.AsmOper, secondary?.AsmOper),
                AsmType = pick(primary?.AsmType, secondary?.AsmType),
                Fiscal = pick(primary?.Fiscal, secondary?.Fiscal),
                Quarter = pick(primary?.Quarter, secondary?.Quarter),
            };

            return merged;
        }

        /// <summary>
        /// Normalizes various Quarter strings to "Q1".."Q4".
        /// Accepts "1","01","Q1","Q1-2025","Quarter1" etc. Returns null if not recognized.
        /// </summary>
        /// <param name="q">Raw quarter string.</param>
        /// <returns>Normalized "Q1".."Q4" or null.</returns>
        private static string? NormalizeQuarter(string? q)
        {
            if (string.IsNullOrWhiteSpace(q)) return null;
            var s = q.Trim().ToUpperInvariant();

            // Remove any trailing fiscal decorations, keep the part containing Q#
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
                // Just digits?
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
        /// Derives Fiscal (year string "YYYY") from filename "yyyyMMdd-..." (first 4 digits), if possible.
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
                var q = NormalizeQuarter(last);
                return q;
            }
            return null;
        }
    }
}
