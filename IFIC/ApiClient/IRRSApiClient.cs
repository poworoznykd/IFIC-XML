/*
 * FILE          : IRRSApiClient.cs
 * PROJECT       : IFIC - IRRS/FHIR Intermediary Component
 * PROGRAMMER    : Darryl Poworoznyk
 * FIRST VERSION : 2025-08-08
 * DESCRIPTION   :
 *   This class implements the IApiClient interface and handles sending FHIR XML
 *   payloads to the CIHI IRRS endpoint using an OAuth2 access token. Logs all
 *   activity, including submission status and transaction ID if provided.
 */

using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using IFIC.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IFIC.ApiClient
{
    /// <summary>
    /// Handles authenticated submission of FHIR XML bundles to the CIHI IRRS endpoint.
    /// </summary>
    public class IRRSApiClient : IApiClient
    {
        private readonly HttpClient httpClient;
        private readonly IAuthManager authManager;
        private readonly ILogger<IRRSApiClient> logger;
        private readonly string endpointUrl;
        public static string clientMessage = "";
        /// <summary>
        /// Initializes a new instance of the IRRSApiClient class.
        /// </summary>
        /// <param name="httpClient">The HTTP client used to send requests.</param>
        /// <param name="authManager">The authentication manager that provides the access token.</param>
        /// <param name="config">Application configuration for endpoint URL.</param>
        /// <param name="logger">Logger for diagnostic and event logging.</param>
        /// <exception cref="ArgumentNullException">Thrown if a required dependency is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if endpoint URL is not found in configuration.</exception>
        public IRRSApiClient(
            HttpClient httpClient,
            IAuthManager authManager,
            IConfiguration config,
            ILogger<IRRSApiClient> logger)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            this.authManager = authManager ?? throw new ArgumentNullException(nameof(authManager));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.endpointUrl = config["Api:SubmissionEndpoint"]
                ?? throw new InvalidOperationException("Missing 'Api:SubmissionEndpoint' in configuration.");
        }

        /// <summary>
        /// Submits a FHIR XML payload to the CIHI IRRS endpoint with bearer token authentication.
        /// </summary>
        /// <param name="xmlContent">The XML content representing the FHIR bundle.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        /// <exception cref="HttpRequestException">Thrown if the submission fails.</exception>
        public async Task SubmitXmlAsync(string xmlContent)
        {
            // Ensure XML declaration is present
            if (!xmlContent.TrimStart().StartsWith("<?xml"))
            {
                xmlContent = "<?xml version='1.0' encoding='UTF-8'?>\n" + xmlContent;
            }

            // Get OAuth2 access token
            var token = await authManager.GetAccessTokenAsync();

            // Build HTTP request
            var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var trimmed = xmlContent.TrimStart();
            if (trimmed.StartsWith("\"") || trimmed.StartsWith("'"))
            {
                // If someone upstream ever serialized the XML into JSON by mistake,
                // strip a single wrapping pair of quotes (defensive fix) and log it.
                var unwrapped = trimmed.Trim();
                if ((unwrapped.StartsWith("\"") && unwrapped.EndsWith("\"")) ||
                    (unwrapped.StartsWith("'") && unwrapped.EndsWith("'")))
                {
                    logger.LogWarning("XML appeared to be wrapped in quotes; unwrapping defensively.");
                    xmlContent = unwrapped.Substring(1, unwrapped.Length - 2);
                }
            }
            // Optional: sanity log (first chars must be '<')
            if (!xmlContent.TrimStart().StartsWith("<"))
            {
                logger.LogWarning("XML does not start with '<'. First 60 chars: {Prefix}", xmlContent.Substring(0, Math.Min(60, xmlContent.Length)));
            }


            request.Content = new StringContent(xmlContent, Encoding.UTF8, "application/fhir+xml");
            // In IRRSApiClient.SubmitXmlAsync, after creating 'request'
            request.Headers.Accept.Clear();
            request.Headers.Accept.ParseAdd("application/fhir+xml");


            logger.LogInformation("Sending XML to CIHI endpoint...");
            Console.WriteLine("Sending XML to CIHI endpoint...");

            // Send the request
            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Try to get the transaction ID from headers
            string transactionId = null;
            if (response.Headers.TryGetValues("x-cihi-transaction-id", out var values))
            {
                transactionId = values.FirstOrDefault();
            }
            else
            {
                Console.WriteLine("No transaction_id header found in CIHI submission response.");
            }

            // Check for failure
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "Submission failed with status code {StatusCode}. Response: {Response} Transaction ID: {TransactionID}",
                    response.StatusCode,
                    responseContent,
                    transactionId ?? "N/A"
                );

                Console.WriteLine($"Submission failed: {response.StatusCode}");
                throw new HttpRequestException($"CIHI submission failed: {"Transaction ID: " + transactionId + " "} {response.StatusCode} - {responseContent}");
            }

            // Success
            logger.LogInformation("Submission successful. CIHI Response: {Response}", responseContent);
            Console.WriteLine("Submission successful.");
            clientMessage = $"CIHI submission: {"Transaction ID: " + transactionId + " "} {response.StatusCode} - {responseContent}";
        }

    }
}
