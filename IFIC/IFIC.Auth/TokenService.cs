/*
 * FILE          : TokenService.cs
 * PROJECT       : IFIC - IRRS/FHIR Intermediary Component
 * PROGRAMMER    : Darryl Poworoznyk
 * FIRST VERSION : 2025-08-02
 * DESCRIPTION   :
 *   Caches OAuth2 tokens in memory and handles token expiry to prevent
 *   redundant token requests.
 */

using System;

namespace IFIC.Auth
{
    public class TokenService
    {
        private string? cachedToken;
        private DateTime tokenExpiry;

        /*
         * FUNCTION      : StoreToken
         * DESCRIPTION   :
         *   Stores an access token and its expiration time in memory.
         * PARAMETERS    :
         *   string token       : OAuth2 access token
         *   TimeSpan lifetime  : Token lifetime
         */
        public void StoreToken(string token, TimeSpan lifetime)
        {
            cachedToken = token;
            tokenExpiry = DateTime.UtcNow.Add(lifetime);
        }

        /*
         * FUNCTION      : GetToken
         * DESCRIPTION   :
         *   Retrieves a valid token from memory if not expired.
         * PARAMETERS    :
         *   (none)
         * RETURNS       :
         *   string? : The token if valid, otherwise null
         */
        public string? GetToken()
        {
            if (!string.IsNullOrEmpty(cachedToken) && DateTime.UtcNow < tokenExpiry)
            {
                return cachedToken;
            }
            return null;
        }
    }
}
