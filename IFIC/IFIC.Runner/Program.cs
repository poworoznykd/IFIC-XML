/*
 * FILE          : Program.cs
 * PROJECT       : IFIC - IRRS/FHIR Intermediary Component
 * PROGRAMMER    : Darryl Poworoznyk
 * FIRST VERSION : 2025-08-01
 * DESCRIPTION   :
 *   Entry point for testing XML FHIR submission to CIHI using OAuth2.
 *   Allows simulation mode with a specific file from SampleXML folder.
 */

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using IFIC.Auth;
using IFIC.ApiClient;

namespace IFIC.Runner
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            IHost host = CreateHostBuilder(args).Build();

            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            try
            {
                logger.LogInformation("===== IFIC XML Submission Test Starting =====");

                // Determine file path based on arguments
                string xmlFilePath;

                if (args.Length >= 2 && args[0].Equals("--simulate", StringComparison.OrdinalIgnoreCase))
                {
                    // User provided a file name for simulation
                    string fileName = args[1];
                    string sampleFolder = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "IFIC.Runner", "SampleXML");
                    xmlFilePath = Path.Combine(sampleFolder, fileName);

                    logger.LogInformation("Simulation mode: Using file {File}", xmlFilePath);
                }
                else
                {
                    // Default to Example-Full-Bundle.xml
                    xmlFilePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                        "IFIC.Runner", "SampleXML", "Example-Full-Bundle.xml");

                    logger.LogInformation("Default mode: Using file {File}", xmlFilePath);
                }

                if (!File.Exists(xmlFilePath))
                {
                    logger.LogError("XML file not found: {Path}", xmlFilePath);
                    return;
                }

                string xmlContent = await File.ReadAllTextAsync(xmlFilePath);

                // Resolve ApiClient and submit XML content
                var apiClient = host.Services.GetRequiredService<IApiClient>();
                await apiClient.SubmitXmlAsync(xmlContent);

                logger.LogInformation("===== XML Submission Completed Successfully =====");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during XML submission.");
            }
        }

        /*
         * FUNCTION      : CreateHostBuilder
         * DESCRIPTION   :
         *   Configures the application host, dependency injection, and logging.
         * PARAMETERS    :
         *   string[] args : Command-line arguments
         * RETURNS       :
         *   IHostBuilder : Configured host builder instance
         */
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
