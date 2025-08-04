/************************************************************************************
* FILE          : FhirPipeline.cs
* PROJECT       : IFIC-XML
* PROGRAMMER    : Darryl Poworoznyk
* FIRST VERSION : 2025-08-02
* DESCRIPTION   : Orchestrates parsing flat files, building FHIR XML Bundles, saving locally, and submitting to CIHI.
************************************************************************************/

using System;
using System.IO;
using System.Threading.Tasks;
using IFIC.FileIngestor.Builders;
using IFIC.FileIngestor.Parsers;
using Microsoft.Extensions.Logging;
using IFIC.ApiClient;

namespace IFIC.FileIngestor
{
    public class FhirPipeline
    {
        private readonly IApiClient _apiClient;
        private readonly ILogger<FhirPipeline> _logger;

        public FhirPipeline(IApiClient apiClient, ILogger<FhirPipeline> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        /// <summary>
        /// Processes a flat file into a FHIR XML Bundle and submits it to CIHI.
        /// </summary>
        /// <param name="flatFilePath">Path of the flat file.</param>
        /// <param name="outputFolder">Folder where the XML will be saved.</param>
        public async Task ProcessFlatFileAsync(string flatFilePath, string outputFolder)
        {
            _logger.LogInformation("Starting processing of flat file: {File}", flatFilePath);

            var parser = new FlatFileParser();
            var parsed = parser.Parse(flatFilePath);

            var builder = new FhirXmlBuilder();
            var bundle = builder.BuildPatientBundle(parsed);

            Directory.CreateDirectory(outputFolder);
            var filePath = Path.Combine(outputFolder, $"fhir_patient_bundle_{DateTime.Now:yyyyMMdd_HHmmss}.xml");
            await File.WriteAllTextAsync(filePath, bundle.ToString());

            _logger.LogInformation("FHIR Bundle saved to {Path}", filePath);

            await _apiClient.SubmitXmlAsync(bundle.ToString());

            _logger.LogInformation("FHIR Bundle submitted successfully.");
        }
    }
}
