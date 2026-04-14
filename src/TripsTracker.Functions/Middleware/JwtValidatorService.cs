using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using TripsTracker.Interfaces.Configuration;

namespace TripsTracker.Functions.Middleware;

/// <summary>
/// Singleton service that validates JWT Bearer tokens against the Microsoft identity platform.
/// Uses OIDC discovery to fetch and rotate signing keys automatically.
///
/// Issuer validation is disabled: the /common endpoint's discovery document returns a literal
/// {tenantid} placeholder in the issuer field — actual issuers vary by account type.
///
/// Audience validation is enabled: tokens must be issued for this application's
/// Application ID URI (api://{clientId}), proving they were not issued for another service.
/// </summary>
public class JwtValidatorService
{
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager;
    private readonly string[] _validAudiences;
    private readonly string _discoveryUrl;
    private readonly JsonWebTokenHandler _tokenHandler = new();
    private readonly ILogger<JwtValidatorService> _logger;

    public JwtValidatorService(IOptions<AuthOptions> authOptions, ILogger<JwtValidatorService> logger)
    {
        var opts = authOptions.Value;
        _logger = logger;

        // Accept both Application ID URI (api://clientId) and raw client ID as audience.
        // v2.0 tokens use the URI, v1.0 tokens use the raw GUID — both are valid.
        var audienceUri = opts.Audience; // e.g. api://05aa01d6-...
        var clientId = audienceUri.StartsWith("api://", StringComparison.OrdinalIgnoreCase)
            ? audienceUri[6..]
            : audienceUri;
        _validAudiences = [audienceUri, clientId];

        _discoveryUrl = opts.Authority.TrimEnd('/') + "/.well-known/openid-configuration";
        _logger.LogInformation("JwtValidatorService initialized — discovery: {DiscoveryUrl}, audiences: {Audiences}", _discoveryUrl, string.Join(", ", _validAudiences));

        _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            _discoveryUrl,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = true });
    }

    /// <summary>
    /// Validates a raw Bearer token string.
    /// Returns the claims principal on success, null on failure.
    /// </summary>
    public async Task<ClaimsPrincipal?> ValidateAsync(string token, CancellationToken ct = default)
    {
        try
        {
            var config = await _configManager.GetConfigurationAsync(ct);
            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = true,
                ValidAudiences = _validAudiences,
                ValidateLifetime = true,
                IssuerSigningKeys = config.SigningKeys,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            var result = await _tokenHandler.ValidateTokenAsync(token, validationParams);
            if (!result.IsValid)
            {
                _logger.LogWarning("Token validation failed: {Error}", result.Exception?.Message ?? "unknown");
            }
            return result.IsValid ? new ClaimsPrincipal(result.ClaimsIdentity) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token validation threw exception");
            return null;
        }
    }
}
