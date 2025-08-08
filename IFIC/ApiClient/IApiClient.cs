/************************************************************************************
* FILE          : IApiClient.cs
* PROJECT       : IFIC - IRRS/FHIR Intermediary Component
* PROGRAMMER    : Darryl Poworoznyk
* FIRST VERSION : 2025-08-02
* DESCRIPTION   : Defines an abstraction for submitting XML payloads to the CIHI API.
************************************************************************************/

using System.Threading.Tasks;

namespace IFIC.ApiClient
{
    public interface IApiClient
    {
        /// <summary>
        /// Submits an XML payload to the configured CIHI endpoint.
        /// </summary>
        /// <param name="xmlContent">FHIR-compliant XML string to submit</param>
        /// <returns>A string containing the response from the CIHI API</returns>
        Task SubmitXmlAsync(string xmlContent);

    }
}
