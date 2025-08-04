/************************************************************************************
* FILE          : IRRSApiClient.cs
* PROJECT       : IFIC - IRRS/FHIR Intermediary Component
* PROGRAMMER    : Darryl Poworoznyk
* FIRST VERSION : 2025-08-02
* DESCRIPTION   :
*   Handles HTTP POST submissions of FHIR XML bundles to CIHI IRRS API.
*   Implements IApiClient interface.
************************************************************************************/

using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IFIC.ApiClient
{
    public class IRRSApiClient : IApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ILogger<IRRSApiClient> _logger;

        public IRRSApiClient(HttpClient httpClient, IConfiguration config, ILogger<IRRSApiClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Submits XML payload to CIHI API endpoint asynchronously.
        /// </summary>
        /// <param name="xmlContent">FHIR XML string to submit</param>
        /// <returns>Response content as string</returns>
        public async Task<string> SubmitXmlAsync(string xmlContent)
        {
            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                throw new ArgumentException("XML content cannot be null or empty.", nameof(xmlContent));
            }

            var submissionUrl = _config["Api:SubmissionEndpoint"];
            if (string.IsNullOrEmpty(submissionUrl))
            {
                throw new InvalidOperationException("Submission endpoint is not configured.");
            }

            _logger.LogInformation("Submitting XML to CIHI API: {Url}", submissionUrl);

            var content = new StringContent(xmlContent, Encoding.UTF8, "application/fhir+xml");
            var response = await _httpClient.PostAsync(submissionUrl, content);

            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Submission failed with status {StatusCode}. Response: {Response}", response.StatusCode, responseString);
                throw new HttpRequestException($"Submission failed: {response.StatusCode} - {responseString}");
            }

            _logger.LogInformation("Submission successful. Response length: {Length}", responseString.Length);
            return responseString;
        }
    }
}
