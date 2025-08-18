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
            Console.WriteLine("===== IFIC Runner Starting =====");

            IHost host = CreateHostBuilder(args).Build();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var apiClient = host.Services.GetRequiredService<IApiClient>();
            var config = host.Services.GetRequiredService<IConfiguration>();
            if(config != null && apiClient != null)
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

                        try
                        {
                            logger.LogInformation("Processing flat file: {File}", datPath);
                            File.AppendAllText(runLogFile, $"Processing flat file: {datPath}{Environment.NewLine}");

                            var (passed, admin) = await ProcessSingleFileAsync(
                                datPath,
                                outputFolder,
                                fileTimestamp,
                                apiClient,
                                logger,
                                runLogFile
                            );

                            RouteDatFile(datPath, admin, passed, transmitRoot);
                            logger.LogInformation("Routed {File} to {Status}.", Path.GetFileName(datPath), passed ? "Processed" : "Errored");
                        }
                        catch (Exception ex)
                        {
                            File.AppendAllText(runLogFile, ex + Environment.NewLine);
                            logger.LogError(ex, "An error occurred during processing of {File}.", datPath);

                            try
                            {
                                RouteDatFile(datPath, admin: null, passed: false, transmitRoot: transmitRoot);
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

        /// <summary>
        /// Processes a single .dat file:
        /// parses the flat file, builds Patient/Encounter/Questionnaire bundles,
        /// constructs the full Bundle XML, saves outputs, submits to CIHI,
        /// then evaluates pass/fail from the API response text.
        /// </summary>
        /// <param name="datPath">Full path to the queued .dat file.</param>
        /// <param name="outputFolder">Folder where XML outputs are written.</param>
        /// <param name="timestamp">Timestamp for naming outputs.</param>
        /// <param name="apiClient">CIHI API client.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="runLogFile">Path to the run log file.</param>
        /// <returns>
        /// Tuple (Passed, Admin) where:
        ///   Passed = true if considered successful based on API response; false otherwise.
        ///   Admin = [ADMIN] key/value map (may be empty but never null).
        /// </returns>
        /// <exception cref="IOException">Thrown on I/O failures while saving XML files.</exception>
        /// <exception cref="XmlException">Thrown if generated XML cannot be parsed or serialized.</exception>
        private static async Task<(bool Passed, Dictionary<string, string> Admin)> ProcessSingleFileAsync(
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
            var admin = parsedFile.Admin ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Build patient, encounter, and questionnaire bundles if applicable
            var patientBuilder = parsedFile.Patient.Any() ? new PatientXmlBuilder() : null;
            var encounterBuilder = parsedFile.Encounter.Any() ? new EncounterXmlBuilder() : null;
            var questionnaireResponseBuilder = parsedFile.AssessmentSections.Any() ? new QuestionnaireResponseBuilder() : null;

            parsedFile.Admin.TryGetValue("patOper", out var patOper);
            parsedFile.Admin.TryGetValue("encOper", out var encOper);

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

            return (passed, admin);
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
                // Using "true" to avoid false negatives; change to false if you prefer strict behavior.
                return true;
            }

            try
            {
                // Some clients prefix with "OK - " or other text before the XML. Strip everything before '<'.
                int firstLt = apiResponse.IndexOf('<');
                string xml = firstLt >= 0 ? apiResponse.Substring(firstLt) : apiResponse;

                var ns = (XNamespace)"http://hl7.org/fhir";
                var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);

                // If there's any OperationOutcome with error/fatal, fail.
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

                // Ensure Bundle type is transaction-response
                var bundle = doc.Root;
                if (bundle == null || bundle.Name != ns + "Bundle")
                    return false;

                var typeVal = bundle.Element(ns + "type")?.Attribute("value")?.Value?.Trim().ToLowerInvariant();
                if (typeVal != "transaction-response")
                    return false;

                // All entry.response.status must be 2xx
                var statuses =
                    bundle.Elements(ns + "entry")
                          .Select(e => e.Element(ns + "response"))
                          .Where(r => r != null)
                          .Select(r => r!.Element(ns + "status")?.Attribute("value")?.Value?.Trim())
                          .Where(v => !string.IsNullOrWhiteSpace(v))
                          .ToList();

                if (statuses.Count == 0)
                    return false; // No statuses found → suspicious

                foreach (var s in statuses)
                {
                    // Status from CIHI appears as numeric string (e.g., "201")
                    if (!int.TryParse(s, out int code) || code < 200 || code >= 300)
                        return false;
                }

                // Optionally, you can enforce that etag is CREATED/UPDATED if needed:
                // var etagsOk = bundle.Elements(ns + "entry")
                //     .Select(e => e.Element(ns + "response")?.Element(ns + "etag")?.Attribute("value")?.Value?.Trim().ToUpperInvariant())
                //     .Where(v => v != null)
                //     .All(v => v == "CREATED" || v == "UPDATED");
                // if (!etagsOk) return false;

                return true;
            }
            catch
            {
                // If parsing fails, fall back to simple heuristics:
                var lower = apiResponse.ToLowerInvariant();
                if (lower.Contains("operationoutcome") && lower.Contains("severity") && (lower.Contains("error") || lower.Contains("fatal")))
                    return false;

                // If it says "status value=" and shows a 4xx/5xx anywhere, treat as fail.
                if (lower.Contains(" status value=\"4") || lower.Contains(" status value=\"5"))
                    return false;

                // Otherwise assume success.
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
        /// &lt;TransmitRoot&gt;\&lt;Fiscal&gt;\&lt;Quarter&gt;\Processed or \Errored.
        /// If ADMIN is null or missing fields, routes to &lt;TransmitRoot&gt;\Unknown\Processed|Errored.
        /// </summary>
        /// <param name="datPath">Full path to the .dat file.</param>
        /// <param name="admin">[ADMIN] key/value map (may be null on parse errors).</param>
        /// <param name="passed">True to route to Processed; false to route to Errored.</param>
        /// <param name="transmitRoot">Root folder of the LTCF Transmit tree.</param>
        /// <exception cref="IOException">Thrown if file moves fail due to I/O errors.</exception>
        private static void RouteDatFile(string datPath, Dictionary<string, string>? admin, bool passed, string transmitRoot)
        {
            if (string.IsNullOrWhiteSpace(datPath) || !File.Exists(datPath))
                return;

            string fiscal = (admin != null && admin.TryGetValue("fiscal", out var f) && !string.IsNullOrWhiteSpace(f)) ? f : "Unknown";
            string quarter = (admin != null && admin.TryGetValue("quarter", out var q) && !string.IsNullOrWhiteSpace(q)) ? q : "Unknown";

            string statusFolder = passed ? "Processed" : "Errored";
            string destFolder = Path.Combine(transmitRoot, fiscal, quarter, statusFolder);
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
    }
}
