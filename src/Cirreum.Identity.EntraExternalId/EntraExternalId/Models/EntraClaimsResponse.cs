namespace Cirreum.Identity.EntraExternalId.Models;

using System.Text.Json.Serialization;

/// <summary>
/// The response payload expected by Entra External ID for the
/// <c>onTokenIssuanceStart</c> custom authentication extension. Returns custom claims to
/// be added to the issued token.
/// </summary>
/// <remarks>
/// The shape is fixed by Microsoft — including the <c>@odata.type</c> discriminators on
/// the response envelope and each action. See
/// <see href="https://learn.microsoft.com/en-us/graph/api/resources/onTokenIssuanceStartResponseData"/>.
/// </remarks>
internal sealed record EntraClaimsResponse {

	[JsonPropertyName("data")]
	public EntraResponseData Data { get; init; } = new();
}

internal sealed record EntraResponseData {

	[JsonPropertyName("@odata.type")]
	public string ODataType { get; init; } = "microsoft.graph.onTokenIssuanceStartResponseData";

	[JsonPropertyName("actions")]
	public List<EntraTokenAction> Actions { get; init; } = [new()];
}

internal sealed record EntraTokenAction {

	[JsonPropertyName("@odata.type")]
	public string ODataType { get; init; } = "microsoft.graph.tokenIssuanceStart.provideClaimsForToken";

	[JsonPropertyName("claims")]
	public EntraCustomClaims Claims { get; init; } = new();
}

internal sealed record EntraCustomClaims {

	[JsonPropertyName("correlationId")]
	public string CorrelationId { get; init; } = "";

	[JsonPropertyName("customRoles")]
	public List<string> CustomRoles { get; init; } = [];
}
