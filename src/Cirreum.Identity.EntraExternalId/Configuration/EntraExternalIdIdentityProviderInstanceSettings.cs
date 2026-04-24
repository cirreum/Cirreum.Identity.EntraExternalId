namespace Cirreum.Identity.Configuration;

/// <summary>
/// Settings for a single Microsoft Entra External ID identity provider instance.
/// Maps to: <c>Cirreum:Identity:Providers:EntraExternalId:Instances:{key}</c>.
/// </summary>
/// <remarks>
/// <para>
/// The Entra External ID provider implements Microsoft's custom authentication extension
/// pattern (onTokenIssuanceStart). Entra posts a signed JWT to the endpoint; this package
/// validates the token against Entra's OIDC discovery metadata, dispatches to the app's
/// <see cref="Provisioning.IUserProvisioner"/>, and returns a Microsoft-shaped response
/// envelope carrying the provisioned roles.
/// </para>
/// <para>
/// Each instance is tied to a specific Entra External ID tenant — one <c>ClientId</c>,
/// <c>Issuer</c>, and <c>MetadataEndpoint</c> triplet per instance.
/// </para>
/// </remarks>
public sealed class EntraExternalIdIdentityProviderInstanceSettings
	: IdentityProviderInstanceSettings {

	/// <summary>
	/// The Application (client) ID of the custom claims provider app registration in the
	/// Entra External ID tenant. Validated as the <c>aud</c> claim on the inbound bearer
	/// token. Required.
	/// </summary>
	public required string ClientId { get; set; }

	/// <summary>
	/// The expected token issuer URL for the Entra External ID (CIAM) tenant.
	/// Required.
	/// </summary>
	/// <remarks>
	/// <strong>Must use the tenant-ID subdomain format</strong>:
	/// <c>https://&lt;tenant-id&gt;.ciamlogin.com/&lt;tenant-id&gt;/v2.0</c>
	/// — NOT the domain-name format (e.g. <c>yourtenant.ciamlogin.com</c>). Using the
	/// wrong format causes silent token-validation failure.
	/// </remarks>
	public required string Issuer { get; set; }

	/// <summary>
	/// The OIDC discovery endpoint used to fetch token signing keys. Required.
	/// </summary>
	/// <remarks>
	/// Typically
	/// <c>https://&lt;tenant-id&gt;.ciamlogin.com/&lt;tenant-id&gt;/v2.0/.well-known/openid-configuration</c>.
	/// The package caches the retrieved configuration per-instance and auto-refreshes on
	/// unknown <c>kid</c>.
	/// </remarks>
	public required string MetadataEndpoint { get; set; }

	/// <summary>
	/// The Microsoft Entra application ID that issues the callback token. Validated against
	/// the <c>appid</c> (v1) or <c>azp</c> (v2) claim on the inbound token.
	/// </summary>
	/// <remarks>
	/// This is <c>99045fe1-7639-4a75-9d4a-577b6ca3810f</c> — the well-known Microsoft
	/// service app ID used by all Entra External ID tenants for custom authentication
	/// extension callbacks. Override only if Microsoft changes the value.
	/// </remarks>
	public string EntraAppId { get; set; } = "99045fe1-7639-4a75-9d4a-577b6ca3810f";

	/// <summary>
	/// Optional comma- or semicolon-separated list of client application IDs allowed to
	/// trigger this endpoint. If empty, client-app enforcement is disabled.
	/// </summary>
	/// <remarks>
	/// When configured, the handler rejects (403) requests whose
	/// <c>clientServicePrincipal.appId</c> from the callback payload is not in this
	/// allowlist. Strongly recommended for production — prevents other applications in the
	/// same Entra tenant from triggering provisioning in your application.
	/// </remarks>
	public string AllowedAppIds { get; set; } = "";

	/// <summary>
	/// Clock-skew tolerance in minutes applied during JWT <c>exp</c> / <c>nbf</c>
	/// validation. Defaults to <c>5</c>.
	/// </summary>
	public int ClockSkewMinutes { get; set; } = 5;

	/// <summary>
	/// Parses <see cref="AllowedAppIds"/> into a set for fast lookup.
	/// </summary>
	internal HashSet<string> GetAllowedAppIdSet() =>
		[.. this.AllowedAppIds.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

	/// <summary>
	/// Returns <see langword="true"/> if <see cref="AllowedAppIds"/> has been configured
	/// with at least one entry.
	/// </summary>
	internal bool HasAllowedAppIds() =>
		!string.IsNullOrWhiteSpace(this.AllowedAppIds);
}
