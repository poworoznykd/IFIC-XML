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

                        // Run per-file logic and get (passed, admin)
                        var (passed, admin) = await ProcessSingleFileAsync(
                            datPath,
                            outputFolder,
                            fileTimestamp,
                            apiClient,
                            logger,
                            runLogFile
                        );

                        // Route based on pass/fail
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

        /// <summary>
        /// Processes a single .dat file using existing logic.
        /// </summary>
        /// <param name="datPath">Full path to the queued .dat file.</param>
        /// <param name="outputFolder">Folder where XML outputs are written.</param>
        /// <param name="timestamp">Timestamp for naming outputs.</param>
        /// <param name="apiClient">CIHI API client.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="runLogFile">Path to the run log file.</param>
        /// <returns>
        /// Tuple (Passed, Admin) where:
        ///   Passed = true if routed to Processed; false if routed to Errored.
        ///   Admin = [ADMIN] key/value map (may be empty but never null).
        /// </returns>
        private static async Task<(bool Passed, Dictionary<string, string> Admin)> ProcessSingleFileAsync(
            string datPath,
            string outputFolder,
            string timestamp,
            IApiClient apiClient,
            ILogger logger,
            string runLogFile)
        {
            var parser = new FlatFileParser();
            var parsedFile = parser.Parse(datPath);
            var admin = parsedFile.Admin ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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

            var bundleBuilder = new BundleXmlBuilder();
            var bundleResponseDoc = bundleBuilder.BuildFullBundle(parsedFile, patientBuilder, encounterBuilder, questionnaireResponseBuilder);
            bundleResponseDoc.Declaration = new XDeclaration("1.0", "UTF-8", null);

            string outputPath = Path.Combine(outputFolder, $"fhir_bundle_{timestamp}.xml");
            SaveWithDeclaration(outputPath, bundleResponseDoc.ToString());

            var probePath = Path.Combine(outputFolder, $"_probe_sent_{timestamp}.xml");
            SaveWithDeclaration(probePath, bundleResponseDoc.ToString());

            bool passed = true;
            // Add your pass/fail rules here, defaulting to true

            if (passed)
            {
                await apiClient.SubmitXmlAsync(bundleResponseDoc.ToString());
            }

            return (passed, admin);
        }

        /// <summary>
        /// Returns queued .dat files sorted from oldest to newest.
        /// </summary>
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
        /// Attempts to parse a UTC timestamp from a filename stem (yyyyMMdd-HHmmss…).
        /// </summary>
        private static bool TryParseTimestampFromName(string name, out DateTime tsUtc)
        {
            tsUtc = DateTime.MinValue;
            var stem = Path.GetFileNameWithoutExtension(name);
            var parts = stem.Split('-');
            if (parts.Length < 2) return false;

            var datePart = parts[0];
            var timePart = parts[1];

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
        /// Routes a .dat file into Processed/Errored under fiscal/quarter folders.
        /// </summary>
        private static void RouteDatFile(string datPath, Dictionary<string, string>? admin, bool passed, string transmitRoot)
        {
            if (string.IsNullOrWhiteSpace(datPath) || !File.Exists(datPath))
                return;

            string fiscal = (admin != null && admin.TryGetValue("fiscal", out var f) && !string.IsNullOrWhiteSpace(f)) ? f : "Unknown";
            string quarter = (admin != null && admin.TryGetValue("quarter", out var q) && !string.IsNullOrWhiteSpace(q)) ? q : "Unknown";

            string statusFolder = passed ? "Processed" : "Errored";
            string destFolder = Path.Combine(transmitRoot, fiscal, quarter, statusFolder);
            Directory.CreateDirectory(destFolder);

            string fileName = Path.GetFileName(datPath);
            string targetDat = Path.Combine(destFolder, fileName);
            File.Move(datPath, targetDat, overwrite: true);

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
        private static void SaveWithDeclaration(string xmlPath, string xmlContent)
        {
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
