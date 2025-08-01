/*
 * FILE          : IRRSApiClient.cs
 * PROJECT       : IFIC - IRRS/FHIR Intermediary Component
 * PROGRAMMER    : Darryl Poworoznyk
 * FIRST VERSION : 2025-08-01
 * DESCRIPTION   :
 *   Handles submission of XML FHIR Bundles to CIHI IRRS API using OAuth2
 *   authentication and Bearer token authorization.
 */

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using IFIC.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IFIC.ApiClient
{
    public class IRRSApiClient : IApiClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuthManager _authManager;
        private readonly ILogger<IRRSApiClient> _logger;
        private readonly IConfiguration _config;

        /*
         * FUNCTION      : IRRSApiClient
         * DESCRIPTION   :
         *   Constructor initializes dependencies for XML submission.
         */
        public IRRSApiClient(
            IHttpClientFactory httpClientFactory,
            IAuthManager authManager,
            ILogger<IRRSApiClient> logger,
            IConfiguration config)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _authManager = authManager ?? throw new ArgumentNullException(nameof(authManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /*
         * FUNCTION      : SubmitXmlAsync
         * DESCRIPTION   :
         *   Submits an XML FHIR bundle to CIHI IRRS endpoint using a Bearer token.
         * PARAMETERS    :
         *   string xmlContent : XML payload string to submit
         * RETURNS       :
         *   Task : Asynchronous operation for submission
         */
        public async Task SubmitXmlAsync(string xmlContent)
        {
            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                throw new ArgumentException("XML content cannot be null or empty.", nameof(xmlContent));
            }

            string endpoint = _config["Api:SubmissionEndpoint"];
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                _logger.LogError("Submission endpoint is missing in configuration.");
                throw new InvalidOperationException("Submission endpoint is not configured.");
            }

            _logger.LogInformation("Submitting XML payload to CIHI endpoint: {Endpoint}", endpoint);

            // Get OAuth2 token
            string token = await _authManager.GetAccessTokenAsync();

            // Build HTTP request
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(xmlContent, Encoding.UTF8, "application/fhir+xml")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var client = _httpClientFactory.CreateClient();

            // Send request
            _logger.LogInformation("Sending FHIR XML bundle...");
            HttpResponseMessage response = await client.SendAsync(request);

            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("CIHI submission failed. Status: {Status}, Response: {Response}", response.StatusCode, responseContent);
                throw new Exception($"Submission failed with status {response.StatusCode}");
            }

            _logger.LogInformation("Submission successful. CIHI Response: {Response}", responseContent);
        }
    }
}
