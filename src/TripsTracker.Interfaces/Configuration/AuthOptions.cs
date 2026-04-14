using System.ComponentModel.DataAnnotations;

namespace TripsTracker.Interfaces.Configuration;

/// <summary>
/// Configuration for JWT Bearer token validation.
/// Bound from the "Auth" section in application settings.
/// </summary>
public class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// OIDC authority URL including /v2.0 suffix.
    /// Example: https://login.microsoftonline.com/common/v2.0
    /// </summary>
    [Required]
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Application ID URI of this backend (api://{clientId}).
    /// Access tokens must be issued for this audience.
    /// </summary>
    [Required]
    public string Audience { get; set; } = string.Empty;
}
