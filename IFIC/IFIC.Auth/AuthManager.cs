/*
 * FILE          : AuthManager.cs
 * PROJECT       : IFIC - IRRS/FHIR Intermediary Component
 * PROGRAMMER    : Darryl Poworoznyk
 * FIRST VERSION : 2025-08-02
 * DESCRIPTION   :
 *   Handles OAuth2 authentication by creating a JWT, signing it with
 *   a private key, and exchanging it for an access token. Implements
 *   in-memory caching to avoid redundant token requests.
 */

using System;
using System.IO;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Jose;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

namespace IFIC.Auth
{
    public class AuthManager : IAuthManager
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AuthManager> _logger;
        private readonly TokenService _tokenService;

        /*
         * FUNCTION      : AuthManager
         * DESCRIPTION   :
         *   Constructor that injects configuration, HTTP client factory, and logger.
         * PARAMETERS    :
         *   IConfiguration config               : Application settings
         *   IHttpClientFactory httpClientFactory : For creating HTTP clients
         *   ILogger<AuthManager> logger          : Logging interface
         *   TokenService tokenService            : Token cache
         */
        public AuthManager(
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            ILogger<AuthManager> logger,
            TokenService tokenService)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        }

        /*
         * FUNCTION      : GetAccessTokenAsync
         * DESCRIPTION   :
         *   Generates a signed JWT and exchanges it for an OAuth2 token
         *   using CIHI's token endpoint.
         * RETURNS       :
         *   string : OAuth2 bearer token
         */
        public async Task<string> GetAccessTokenAsync()
        {
            // Load settings from appsettings.json
            string privateKeyPath = _config["Authentication:PrivateKeyPath"]
                ?? throw new InvalidOperationException("Missing 'PrivateKeyPath' in appsettings.json.");
            string systemId = _config["Authentication:SystemIdentifier"]
                ?? throw new InvalidOperationException("Missing 'SystemIdentifier' in appsettings.json.");
            string audience = _config["Authentication:Audience"]
                ?? throw new InvalidOperationException("Missing 'Audience' in appsettings.json.");
            string scope = _config["Authentication:Scope"]
                ?? throw new InvalidOperationException("Missing 'Scope' in appsettings.json.");
            string tokenEndpoint = _config["Authentication:TokenEndpoint"]
                ?? throw new InvalidOperationException("Missing 'TokenEndpoint' in appsettings.json.");

            // Load and parse private key
            var rsaPrivateKey = LoadPrivateKey(privateKeyPath);

            // Build JWT payload
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var payload = new
            {
                iss = systemId,
                sub = "AccessRequest" + systemId,
                scope = scope,
                aud = audience,
                iat = now,
                exp = now + 300
            };

            // Sign JWT
            var jwt = JWT.Encode(payload, rsaPrivateKey, JwsAlgorithm.RS256);

            // Prepare request to token endpoint
            using var client = new HttpClient();
            var content = new StringContent(
                $"grant_type=client_credentials&assertion={jwt}",
                Encoding.UTF8,
                "application/x-www-form-urlencoded");

            var response = await client.PostAsync(tokenEndpoint, content);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"CIHI token request failed: {response.StatusCode} - {error}");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var token = doc.RootElement.GetProperty("access_token").GetString();

            return token;
        }

        /*
         * FUNCTION      : LoadPrivateKey
         * DESCRIPTION   :
         *   Loads RSA private key from PEM file.
         * PARAMETERS    :
         *   string privateKeyPath : Path to private key file
         * RETURNS       :
         *   RSA : RSA object
         */
        private static System.Security.Cryptography.RSA LoadPrivateKey(string privateKeyPath)
        {
            if (!File.Exists(privateKeyPath))
            {
                throw new FileNotFoundException("Private key file not found.", privateKeyPath);
            }

            using var reader = File.OpenText(privateKeyPath);
            var pemReader = new PemReader(reader);
            var keyPair = pemReader.ReadObject() as RsaPrivateCrtKeyParameters;

            if (keyPair == null)
            {
                throw new Exception("Invalid RSA private key format. Ensure PEM format.");
            }

            return DotNetUtilities.ToRSA(keyPair);
        }
    }
}
