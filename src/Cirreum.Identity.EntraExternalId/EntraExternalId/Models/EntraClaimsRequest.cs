namespace Cirreum.Identity.EntraExternalId.Models;

using System.Text.Json.Serialization;

/// <summary>
/// The request payload posted by Entra External ID during the
/// <c>onTokenIssuanceStart</c> custom authentication extension callback.
/// </summary>
/// <remarks>
/// See Microsoft's documentation:
/// <see href="https://learn.microsoft.com/en-us/entra/identity-platform/custom-claims-provider-overview"/>.
/// </remarks>
internal sealed record EntraClaimsRequest {

	[JsonPropertyName("data")]
	public EntraRequestData Data { get; init; } = new();

	[JsonPropertyName("source")]
	public string Source { get; init; } = "";

	[JsonPropertyName("type")]
	public string Type { get; init; } = "";
}

internal sealed record EntraRequestData {

	[JsonPropertyName("@odata.type")]
	public string ODataType { get; init; } = "";

	[JsonPropertyName("authenticationContext")]
	public EntraAuthenticationContext AuthenticationContext { get; init; } = new();

	[JsonPropertyName("authenticationEventListenerId")]
	public string AuthenticationEventListenerId { get; init; } = "";

	[JsonPropertyName("customAuthenticationExtensionId")]
	public string CustomAuthenticationExtensionId { get; init; } = "";

	[JsonPropertyName("tenantId")]
	public string TenantId { get; init; } = "";
}

internal sealed record EntraAuthenticationContext {

	[JsonPropertyName("client")]
	public EntraClient Client { get; init; } = new();

	[JsonPropertyName("clientServicePrincipal")]
	public EntraServicePrincipal ClientServicePrincipal { get; init; } = new();

	[JsonPropertyName("correlationId")]
	public string CorrelationId { get; init; } = "";

	[JsonPropertyName("protocol")]
	public string Protocol { get; init; } = "";

	[JsonPropertyName("resourceServicePrincipal")]
	public EntraServicePrincipal ResourceServicePrincipal { get; init; } = new();

	[JsonPropertyName("user")]
	public EntraUser User { get; init; } = new();
}

internal sealed record EntraClient {

	[JsonPropertyName("ip")]
	public string Ip { get; init; } = "";

	[JsonPropertyName("locale")]
	public string Locale { get; init; } = "";

	[JsonPropertyName("market")]
	public string Market { get; init; } = "";
}

internal sealed record EntraServicePrincipal {

	[JsonPropertyName("appDisplayName")]
	public string AppDisplayName { get; init; } = "";

	[JsonPropertyName("appId")]
	public string AppId { get; init; } = "";

	[JsonPropertyName("displayName")]
	public string DisplayName { get; init; } = "";

	[JsonPropertyName("id")]
	public string Id { get; init; } = "";
}

internal sealed record EntraUser {

	[JsonPropertyName("companyName")]
	public string CompanyName { get; init; } = "";

	[JsonPropertyName("createdDateTime")]
	public DateTimeOffset CreatedDateTime { get; init; }

	[JsonPropertyName("displayName")]
	public string DisplayName { get; init; } = "";

	[JsonPropertyName("givenName")]
	public string GivenName { get; init; } = "";

	[JsonPropertyName("id")]
	public string Id { get; init; } = "";

	[JsonPropertyName("mail")]
	public string Mail { get; init; } = "";

	[JsonPropertyName("surname")]
	public string Surname { get; init; } = "";

	[JsonPropertyName("userPrincipalName")]
	public string UserPrincipalName { get; init; } = "";

	[JsonPropertyName("userType")]
	public string UserType { get; init; } = "";
}
