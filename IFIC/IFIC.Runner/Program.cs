/************************************************************************************
* FILE          : Program.cs
* PROJECT       : IFIC-XML
* PROGRAMMER    : Darryl Poworoznyk
* FIRST VERSION : 2025-08-02
* DESCRIPTION   :
*   Main entry point for IFIC Runner. Supports:
*     - Default Mode: Parse flat file → Build FHIR Patient Bundle → Save + Submit to CIHI
*     - Simulation Mode (--simulate <filename>): Submits an existing XML file to CIHI
************************************************************************************/

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using IFIC.ApiClient;
using IFIC.Auth;
using IFIC.FileIngestor.Parsers;
using IFIC.FileIngestor.Transformers;
using System.Text;
using System.Xml.Linq;
using System.Xml;

namespace IFIC.Runner
{
    public class Program
    {
        private static readonly string logFile;

        /// <summary>
        /// Application entry point. Sets up DI, logging, and executes the submission workflow.
        /// </summary>
        public static async Task Main(string[] args)
        {
            Console.WriteLine("===== IFIC Runner Starting =====");

            // Build the host with configured services and logging
            IHost host = CreateHostBuilder(args).Build();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var apiClient = host.Services.GetRequiredService<IApiClient>();

            // Prepare output and log directories
            string baseDir = AppContext.BaseDirectory;
            string outputFolder = Path.Combine(baseDir, "Output");
            Directory.CreateDirectory(outputFolder);

            string logFolder = Path.Combine(baseDir, "Logs");
            Directory.CreateDirectory(logFolder);

            // Timestamp for file naming
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string logFile = Path.Combine(logFolder, $"runlog_{timestamp}.txt");

            try
            {
                // Simulation Mode: Send an existing XML file
                if (args.Length >= 2 && args[0].Equals("--simulate", StringComparison.OrdinalIgnoreCase))
                {
                    string xmlFileName = args[1];
                    string sampleFolder = Path.Combine(baseDir, "..", "..", "..", "..", "IFIC.Runner", "SampleXML");
                    string xmlFilePath = Path.Combine(sampleFolder, xmlFileName);

                    logger.LogInformation("[SIMULATE] Submitting {File} to CIHI API...", xmlFilePath);
                    File.AppendAllText(logFile, $"Simulation mode. Submitting file: {xmlFilePath}{Environment.NewLine}");

                    if (!File.Exists(xmlFilePath))
                    {
                        logger.LogError("Simulation XML file not found: {Path}", xmlFilePath);
                        File.AppendAllText(logFile, $"ERROR: XML file not found: {xmlFilePath}{Environment.NewLine}");
                        return;
                    }

                    // Read XML and ensure declaration
                    string xmlContent = await File.ReadAllTextAsync(xmlFilePath);
                    if (!xmlContent.TrimStart().StartsWith("<?xml"))
                    {
                        xmlContent = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + xmlContent;
                    }

                    SaveWithDeclaration(xmlFilePath, xmlContent); // Save with guaranteed XML declaration
                    await apiClient.SubmitXmlAsync(xmlContent);

                    logger.LogInformation("Simulation completed successfully.");
                    File.AppendAllText(logFile, "Simulation completed successfully." + Environment.NewLine);
                }
                else
                {
                    // Default Mode: Process flat file → Build bundle → Save + Submit

                    // Path to flat file
                    string flatFilePath = Path.Combine(baseDir, "..", "..", "..", "..", "IFIC.Runner", "SimpleFlatFiles", "Simple-Bundle.dat");
                    logger.LogInformation("Processing flat file: {File}", flatFilePath);
                    File.AppendAllText(logFile, $"Processing flat file: {flatFilePath}{Environment.NewLine}");

                    if (!File.Exists(flatFilePath))
                    {
                        logger.LogError("Flat file not found: {Path}", flatFilePath);
                        File.AppendAllText(logFile, $"ERROR: Flat file not found: {flatFilePath}{Environment.NewLine}");
                        return;
                    }

                    // Parse the flat file into structured data
                    var parser = new FlatFileParser();
                    var parsedFile = parser.Parse(flatFilePath);

                    // Build patient, encounter, and questionnaire bundles if applicable
                    var patientBuilder = parsedFile.Patient.Any() ? new PatientXmlBuilder() : null;
                    var encounterBuilder = parsedFile.Encounter.Any() ? new EncounterXmlBuilder() : null;
                    var questionnaireResponseBuilder = parsedFile.AssessmentSections.Any() ? new QuestionnaireResponseBuilder() : null;

                    patientBuilder?.BuildPatientBundle(parsedFile);
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

                    // ✅ CHANGE: Explicitly set XML declaration before converting to string
                    bundleResponseDoc.Declaration = new XDeclaration("1.0", "UTF-8", null);

                    // Get the XML string (will now include declaration)
                    string xmlContentForSave = bundleResponseDoc.ToString();

                    // Save final bundle to disk
                    string outputPath = Path.Combine(outputFolder, $"fhir_bundle_{timestamp}.xml");
                    SaveWithDeclaration(outputPath, xmlContentForSave);
                    logger.LogInformation("FHIR Bundle saved to: {OutputPath}", outputPath);
                    File.AppendAllText(logFile, $"FHIR Bundle saved: {outputPath}{Environment.NewLine}");

                    // Save probe copy for record
                    var probePath = Path.Combine(outputFolder, $"_probe_sent_{timestamp}.xml");
                    SaveWithDeclaration(probePath, xmlContentForSave);

                    // Submit bundle to CIHI
                    logger.LogInformation("Submitting bundle to CIHI...");
                    File.AppendAllText(logFile, $"Submitting bundle at: {DateTime.Now}{Environment.NewLine}");
                    await apiClient.SubmitXmlAsync(xmlContentForSave);

                    logger.LogInformation("Submission completed successfully.");
                    File.AppendAllText(logFile, "Submission completed successfully." + Environment.NewLine);
                    if (IRRSApiClient.clientMessage != "")
                    {
                        File.AppendAllText(logFile, IRRSApiClient.clientMessage + Environment.NewLine);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log unexpected errors
                File.AppendAllText(logFile, ex.ToString());
                logger.LogError(ex, "An error occurred during processing.");
            }
        }

        /// <summary>
        /// Saves XML to a file, ensuring that the XML declaration exists and is UTF-8 encoded.
        /// </summary>
        static void SaveWithDeclaration(string xmlPath, string xmlContent)
        {
            var doc = XDocument.Parse(xmlContent, LoadOptions.PreserveWhitespace);
            doc.Declaration ??= new XDeclaration("1.0", "UTF-8", null);

            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                Indent = true,
                OmitXmlDeclaration = false
            };

            using var writer = XmlWriter.Create(xmlPath, settings);
            doc.Save(writer);
        }

        /// <summary>
        /// Configures the application host, including DI, configuration, and logging.
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
