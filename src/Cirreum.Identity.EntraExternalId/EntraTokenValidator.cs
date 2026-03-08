namespace Cirreum.Identity.EntraExternalId;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

/// <summary>
/// Validates the bearer token sent by Entra External ID during the
/// onTokenIssuanceStart custom authentication extension callback.
/// Uses OIDC discovery to fetch and cache Microsoft's signing keys.
/// </summary>
internal sealed class EntraTokenValidator(
	IOptions<EntraExternalIdOptions> options,
	ILogger<EntraTokenValidator> logger) {

	private readonly EntraExternalIdOptions _options = options.Value;
	private readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager = new(
		options.Value.MetadataEndpoint,
		new OpenIdConnectConfigurationRetriever(),
		new HttpDocumentRetriever());

	public async Task<bool> ValidateAsync(string token, CancellationToken cancellationToken = default) {
		try {
			var config = await _configManager.GetConfigurationAsync(cancellationToken);

			var validationParameters = new TokenValidationParameters {
				ValidateIssuer = true,
				ValidIssuer = _options.Issuer,
				ValidateAudience = true,
				ValidAudience = _options.ClientId,
				ValidateIssuerSigningKey = true,
				IssuerSigningKeys = config.SigningKeys,
				ValidateLifetime = true,
				ClockSkew = TimeSpan.FromMinutes(5)
			};

			var handler = new JwtSecurityTokenHandler();
			handler.ValidateToken(token, validationParameters, out var validatedToken);

			if (validatedToken is not JwtSecurityToken jwt) {
				logger.LogWarning("Token is not a valid JWT");
				return false;
			}

			// Validate appid (v1) or azp (v2) matches the Entra service app ID
			var appId = jwt.Claims.FirstOrDefault(c => c.Type is "appid")?.Value
				?? jwt.Claims.FirstOrDefault(c => c.Type is "azp")?.Value;

			if (appId != _options.EntraAppId) {
				logger.LogWarning("Token appid/azp '{AppId}' does not match expected '{Expected}'", appId, _options.EntraAppId);
				return false;
			}

			return true;

		} catch (SecurityTokenValidationException ex) {
			logger.LogWarning(ex, "Token validation failed: {Message}", ex.Message);
			return false;
		} catch (Exception ex) {
			logger.LogError(ex, "Unexpected error during token validation: {Message}", ex.Message);
			return false;
		}
	}

}
