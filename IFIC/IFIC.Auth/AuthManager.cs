/************************************************************************************
* FILE          : AuthManager.cs
* PROJECT       : IFIC - IRRS/FHIR Intermediary Component
* PROGRAMMER    : Darryl Poworoznyk
* FIRST VERSION : 2025-08-02
* DESCRIPTION   :
*   Handles OAuth2 authentication by creating a JWT, signing it with
*   a private key, and exchanging it for an access token. Implements
*   in-memory caching to avoid redundant token requests.
************************************************************************************/

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
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
        private readonly IConfiguration config;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly ILogger<AuthManager> logger;
        private readonly TokenService tokenService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthManager"/> class.
        /// </summary>
        /// <param name="config">Application settings</param>
        /// <param name="httpClientFactory">For creating HTTP clients</param>
        /// <param name="logger">Logging interface</param>
        /// <param name="tokenService">Token cache</param>
        public AuthManager(
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            ILogger<AuthManager> logger,
            TokenService tokenService)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        }

        /// <summary>
        /// Generates a signed JWT and exchanges it for an OAuth2 token using CIHI's token endpoint.
        /// </summary>
        /// <returns>OAuth2 bearer token string</returns>
        /// <exception cref="InvalidOperationException">Thrown if a required config setting is missing</exception>
        /// <exception cref="Exception">Thrown if token request fails or token cannot be parsed</exception>
        public async Task<string> GetAccessTokenAsync()
        {
            string privateKeyPath = config["Authentication:PrivateKeyPath"]
                ?? throw new InvalidOperationException("Missing 'PrivateKeyPath' in appsettings.json.");
            string systemId = config["Authentication:SystemIdentifier"]
                ?? throw new InvalidOperationException("Missing 'SystemIdentifier' in appsettings.json.");
            string audience = config["Authentication:Audience"]
                ?? throw new InvalidOperationException("Missing 'Audience' in appsettings.json.");
            string scope = config["Authentication:Scope"]
                ?? throw new InvalidOperationException("Missing 'Scope' in appsettings.json.");
            string tokenEndpoint = config["Authentication:TokenEndpoint"]
                ?? throw new InvalidOperationException("Missing 'TokenEndpoint' in appsettings.json.");

            logger.LogInformation("Requesting access token from CIHI...");
            Console.WriteLine("Requesting access token from CIHI...");

            var rsaPrivateKey = LoadPrivateKey(privateKeyPath);

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

            var jwt = JWT.Encode(payload, rsaPrivateKey, JwsAlgorithm.RS256);

            using var client = new HttpClient();

            var content = new StringContent(
                $"grant_type=client_credentials&assertion={jwt}",
                Encoding.UTF8,
                "application/x-www-form-urlencoded");

            var response = await client.PostAsync(tokenEndpoint, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.Headers.TryGetValues("transaction_id", out var transactionIdValues))
            {
                var transactionId = transactionIdValues.FirstOrDefault();
                logger.LogInformation("CIHI token response includes transaction_id: {TransactionId}", transactionId);
                Console.WriteLine("CIHI token transaction ID: " + transactionId);
            }
            else
            {
                logger.LogInformation("No transaction_id header found in CIHI token response.");
                Console.WriteLine("No transaction_id found in token response.");
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("CIHI token request failed: {StatusCode} - {Content}", response.StatusCode, responseContent);
                Console.WriteLine($"Token request failed: {response.StatusCode} - {responseContent}");
                throw new Exception($"CIHI token request failed: {response.StatusCode} - {responseContent}");
            }

            logger.LogInformation("CIHI token request succeeded: {StatusCode}", response.StatusCode);
            Console.WriteLine("Token request successful.");

            using var doc = JsonDocument.Parse(responseContent);
            var token = doc.RootElement.GetProperty("access_token").GetString();

            //To get token for use in powershell
            logger.LogInformation("Access token successfully extracted from response.");
            Console.WriteLine("Access token successfully received and ready to use.");

            // DEBUG: log full token (remove in production!)
            Console.WriteLine("DEBUG: Access Token = " + token);
            logger.LogInformation("DEBUG: Access Token = {Token}", token);


            logger.LogInformation("Access token successfully extracted from response.");
            Console.WriteLine("Access token successfully received and ready to use.");

            return token;
        }

        /// <summary>
        /// Loads an RSA private key from a PEM file.
        /// </summary>
        /// <param name="privateKeyPath">Path to private key file</param>
        /// <returns>RSA key object</returns>
        /// <exception cref="FileNotFoundException">If the key file is not found</exception>
        /// <exception cref="Exception">If the key is not a valid RSA PEM format</exception>
        private static System.Security.Cryptography.RSA LoadPrivateKey(string privateKeyPath)
        {
            if (!File.Exists(privateKeyPath))
            {
                Console.WriteLine($"ERROR: Private key file not found at {privateKeyPath}");
                throw new FileNotFoundException("Private key file not found.", privateKeyPath);
            }

            using var reader = File.OpenText(privateKeyPath);
            var pemReader = new PemReader(reader);
            var keyPair = pemReader.ReadObject() as RsaPrivateCrtKeyParameters;

            if (keyPair == null)
            {
                Console.WriteLine("ERROR: Invalid RSA private key format. Ensure PEM format.");
                throw new Exception("Invalid RSA private key format. Ensure PEM format.");
            }

            return DotNetUtilities.ToRSA(keyPair);
        }
    }
}
