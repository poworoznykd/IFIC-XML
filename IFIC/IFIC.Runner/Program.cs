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

namespace IFIC.Runner
{
    public class Program
    {
        /// <summary>
        /// Application entry point. Configures DI, logging, and runs the processing pipeline.
        /// </summary>
        /// <param name="args">Command-line arguments (--simulate optional)</param>
        public static async Task Main(string[] args)
        {
            Console.WriteLine("===== IFIC Runner Starting =====");

            IHost host = CreateHostBuilder(args).Build();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var apiClient = host.Services.GetRequiredService<IApiClient>();
            // Resolve paths
            string baseDir = AppContext.BaseDirectory;
            string outputFolder = Path.Combine(baseDir, "Output");
            Directory.CreateDirectory(outputFolder);

            string logFolder = Path.Combine(baseDir, "Logs");
            Directory.CreateDirectory(logFolder);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string logFile = Path.Combine(logFolder, $"runlog_{timestamp}.txt");

            try
            {
                if (args.Length >= 2 && args[0].Equals("--simulate", StringComparison.OrdinalIgnoreCase))
                {
                    // Simulation mode
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

                    string xmlContent = await File.ReadAllTextAsync(xmlFilePath);
                    await apiClient.SubmitXmlAsync(xmlContent);

                    logger.LogInformation("Simulation completed successfully.");
                    File.AppendAllText(logFile, "Simulation completed successfully." + Environment.NewLine);
                }
                else
                {
                    // Default mode: Process flat file → Build Bundle → Submit
                    string flatFilePath = Path.Combine(baseDir, "..", "..", "..", "..", "IFIC.Runner", "SimpleFlatFiles", "Simple-Bundle.dat");

                    logger.LogInformation("Processing flat file: {File}", flatFilePath);
                    File.AppendAllText(logFile, $"Processing flat file: {flatFilePath}{Environment.NewLine}");

                    if (!File.Exists(flatFilePath))
                    {
                        logger.LogError("Flat file not found: {Path}", flatFilePath);
                        File.AppendAllText(logFile, $"ERROR: Flat file not found: {flatFilePath}{Environment.NewLine}");
                        return;
                    }

                    // Parse flat file
                    var parser = new FlatFileParser();
                    var parsedFile = parser.Parse(flatFilePath);

                    var patientBuilder = new PatientXmlBuilder();
                    var encounterBuilder = new EncounterXmlBuilder();
                    var questionnaireResponseBuilder = new QuestionnaireResponseBuilder();

                    if (parsedFile.Patient.Any())
                    {
                        // Build FHIR Patient Bundle
                        var patientDoc = patientBuilder.BuildPatientBundle(parsedFile);
                    }
                    if(parsedFile.Encounter.Any())
                    {
                        // Build FHIR Encounter Bundle
                        var encounterDoc = encounterBuilder.BuildEncounterBundle(parsedFile);
                    }
                    if (parsedFile.AssessmentSections.Any())
                    {
                        // Build FHIR QuestionnaireResponse Bundle
                        var questionnaireResponseDoc = questionnaireResponseBuilder.BuildQuestionnaireResponseBundle(parsedFile);
                    }
                    if (!parsedFile.Patient.Any())
                    {
                        patientBuilder = null;
                    }
                    if (!parsedFile.Encounter.Any())
                    {
                        encounterBuilder = null;
                    }
                    if (!parsedFile.AssessmentSections.Any())
                    {
                        questionnaireResponseBuilder = null;
                    }

                    var bundleBuilder = new BundleXmlBuilder();
                    var bundleResponseDoc = bundleBuilder.BuildFullBundle(
                        parsedFile,
                        patientBuilder,
                        encounterBuilder,
                        questionnaireResponseBuilder);

                    // Save XML locally
                    string outputPath = Path.Combine(outputFolder, $"fhir_bundle_{timestamp}.xml");
                    await File.WriteAllTextAsync(outputPath, bundleResponseDoc.ToString());
                    logger.LogInformation("FHIR Bundle saved to: {OutputPath}", outputPath);
                    File.AppendAllText(logFile, $"FHIR Bundle saved: {outputPath}{Environment.NewLine}");

                    // Submit to CIHI
                    string xmlContentForSubmit = bundleResponseDoc.ToString();
                    logger.LogInformation("Submitting bundle to CIHI...");
                    File.AppendAllText(logFile, $"Submitting bundle at: {DateTime.Now}{Environment.NewLine}");
                    await apiClient.SubmitXmlAsync(xmlContentForSubmit);

                    logger.LogInformation("Submission completed successfully.");
                    File.AppendAllText(logFile, "Submission completed successfully." + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(logFile, ex.ToString());
                logger.LogError(ex, "An error occurred during processing.");
            }
        }

        /// <summary>
        /// Configures the host builder for dependency injection and logging.
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
