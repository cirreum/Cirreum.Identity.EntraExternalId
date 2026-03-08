namespace Cirreum.Identity.EntraExternalId;

/// <summary>
/// Configuration options for the Entra External ID custom authentication extension endpoint.
/// Bind from appsettings.json under the section name specified during registration.
/// </summary>
/// <remarks>
/// See SETUP.md for full configuration instructions, Azure Portal setup steps,
/// and troubleshooting guidance.
/// </remarks>
public sealed class EntraExternalIdOptions {

	/// <summary>
	/// The endpoint route path. Defaults to "/auth/entra/claims".
	/// Must match the Target URL configured on the Custom Authentication Extension in the Azure Portal.
	/// </summary>
	public string Route { get; set; } = "/auth/entra/claims";

	/// <summary>
	/// The Application (client) ID of your custom claims provider app registration.
	/// Validated as the <c>aud</c> claim on the incoming bearer token.
	/// </summary>
	public required string ClientId { get; set; }

	/// <summary>
	/// The expected token issuer URL for your Entra External ID (CIAM) tenant.
	/// Use the tenant ID subdomain format: <c>https://&lt;tenant-id&gt;.ciamlogin.com/&lt;tenant-id&gt;/v2.0</c>
	/// </summary>
	/// <remarks>
	/// Do not use the domain name format (e.g. <c>correxternaldev.ciamlogin.com</c>).
	/// Using the wrong format causes silent token validation failure.
	/// </remarks>
	public required string Issuer { get; set; }

	/// <summary>
	/// The Microsoft Entra application ID that issues the callback token.
	/// Validated against the <c>appid</c> (v1) or <c>azp</c> (v2) claim.
	/// This is <c>99045fe1-7639-4a75-9d4a-577b6ca3810f</c> for all Entra External ID tenants.
	/// </summary>
	public required string EntraAppId { get; set; }

	/// <summary>
	/// The OIDC discovery endpoint used to fetch token signing keys.
	/// Example: <c>https://&lt;tenant-id&gt;.ciamlogin.com/&lt;tenant-id&gt;/v2.0/.well-known/openid-configuration</c>
	/// </summary>
	public required string MetadataEndpoint { get; set; }

	/// <summary>
	/// Comma or semicolon-separated list of allowed application IDs
	/// that can trigger this claims endpoint.
	/// </summary>
	public required string AllowedAppIds { get; set; }

	/// <summary>
	/// Parses AllowedAppIds into a set for fast lookup.
	/// </summary>
	internal HashSet<string> GetAllowedAppIdSet() =>
	  [.. this.AllowedAppIds.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

}
