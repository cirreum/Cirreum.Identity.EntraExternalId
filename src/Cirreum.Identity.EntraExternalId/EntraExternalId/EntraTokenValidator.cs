namespace Cirreum.Identity.EntraExternalId;

using Cirreum.Identity.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

/// <summary>
/// Validates the bearer token posted by Entra External ID during the
/// <c>onTokenIssuanceStart</c> custom authentication extension callback. Uses OIDC
/// discovery (per-instance) to fetch and cache Microsoft's signing keys.
/// </summary>
/// <remarks>
/// <para>
/// One instance of this validator is registered per configured Entra tenant (keyed by the
/// provisioning instance name), so each tenant keeps its own cached signing-key set and
/// discovery configuration.
/// </para>
/// <para>
/// The validator checks, in order: token signature (against keys from the tenant's OIDC
/// metadata), issuer (<see cref="EntraExternalIdIdentityProviderInstanceSettings.Issuer"/>),
/// audience (<see cref="EntraExternalIdIdentityProviderInstanceSettings.ClientId"/>),
/// lifetime (with configured clock skew), and the Entra service app identity
/// (<c>appid</c> v1 / <c>azp</c> v2 matching
/// <see cref="EntraExternalIdIdentityProviderInstanceSettings.EntraAppId"/>).
/// </para>
/// </remarks>
internal sealed class EntraTokenValidator(
	EntraExternalIdIdentityProviderInstanceSettings settings,
	ILogger<EntraTokenValidator> logger) {

	private readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
			settings.MetadataEndpoint,
			new OpenIdConnectConfigurationRetriever(),
			new HttpDocumentRetriever());

	public async Task<bool> ValidateAsync(string token, CancellationToken cancellationToken = default) {
		try {
			var config = await this._configManager.GetConfigurationAsync(cancellationToken);

			var validationParameters = new TokenValidationParameters {
				ValidateIssuer = true,
				ValidIssuer = settings.Issuer,
				ValidateAudience = true,
				ValidAudience = settings.ClientId,
				ValidateIssuerSigningKey = true,
				IssuerSigningKeys = config.SigningKeys,
				ValidateLifetime = true,
				ClockSkew = TimeSpan.FromMinutes(settings.ClockSkewMinutes),
			};

			var handler = new JwtSecurityTokenHandler();
			handler.ValidateToken(token, validationParameters, out var validatedToken);

			if (validatedToken is not JwtSecurityToken jwt) {
				logger.LogWarning(
					"Token is not a valid JWT for instance '{Source}'.",
					settings.Source);
				return false;
			}

			// Validate appid (v1) or azp (v2) matches the Entra service app ID.
			var appId = jwt.Claims.FirstOrDefault(c => c.Type is "appid")?.Value
				?? jwt.Claims.FirstOrDefault(c => c.Type is "azp")?.Value;

			if (appId != settings.EntraAppId) {
				logger.LogWarning(
					"Token appid/azp '{AppId}' does not match expected '{Expected}' for instance '{Source}'.",
					appId, settings.EntraAppId, settings.Source);
				return false;
			}

			return true;

		} catch (SecurityTokenValidationException ex) {
			logger.LogWarning(
				ex,
				"Token validation failed for instance '{Source}': {Message}",
				settings.Source, ex.Message);
			return false;
		} catch (Exception ex) {
			logger.LogError(
				ex,
				"Unexpected error during token validation for instance '{Source}': {Message}",
				settings.Source, ex.Message);
			return false;
		}
	}
}
