/*
 * FILE          : IAuthManager.cs
 * PROJECT       : IFIC - IRRS/FHIR Intermediary Component
 * PROGRAMMER    : Darryl Poworoznyk
 * FIRST VERSION : 2025-08-01
 * DESCRIPTION   :
 *   Defines an interface for OAuth2 authentication manager, responsible
 *   for obtaining access tokens for CIHI IRRS submissions.
 */

using System.Threading.Tasks;

namespace IFIC.Auth
{
    public interface IAuthManager
    {
        /*
         * FUNCTION      : GetAccessTokenAsync
         * DESCRIPTION   :
         *   Retrieves an OAuth2 access token using the JWT Bearer Client
         *   Credentials flow from CIHI authentication service.
         * PARAMETERS    :
         *   (none)
         * RETURNS       :
         *   Task<string> : Asynchronous operation returning the access token
         */
        Task<string> GetAccessTokenAsync();
    }
}
