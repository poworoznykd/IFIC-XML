/*
 * FILE          : IApiClient.cs
 * PROJECT       : IFIC - IRRS/FHIR Intermediary Component
 * PROGRAMMER    : Darryl Poworoznyk
 * FIRST VERSION : 2025-08-01
 * DESCRIPTION   :
 *   Defines an interface for submitting XML FHIR Bundles to CIHI IRRS API.
 */

using System.Threading.Tasks;

namespace IFIC.ApiClient
{
    public interface IApiClient
    {
        /*
         * FUNCTION      : SubmitXmlAsync
         * DESCRIPTION   :
         *   Submits the given XML content to CIHI IRRS service.
         * PARAMETERS    :
         *   string xmlContent : XML payload string to submit
         * RETURNS       :
         *   Task : Represents an asynchronous operation
         */
        Task SubmitXmlAsync(string xmlContent);
    }
}
