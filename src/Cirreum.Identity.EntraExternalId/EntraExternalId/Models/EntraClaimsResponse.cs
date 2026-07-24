namespace Cirreum.Identity.EntraExternalId.Models;

using System.Text.Json;
using System.Text.Json.Serialization;
using Cirreum.Identity.Provisioning;

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
	public EntraClaimsPayload Claims { get; init; } = new();
}

/// <summary>
/// The claims Entra embeds in the issued token. <c>correlationId</c> is a fixed member;
/// every provisioned <c>custom*</c> claim is emitted as a flat inline sibling via
/// <c>[JsonExtensionData]</c>. Entra claim values are String or String[] only, so a
/// single-valued claim serializes as a scalar and a multi-valued (or roles) claim as an array.
/// </summary>
internal sealed class EntraClaimsPayload {

	[JsonPropertyName("correlationId")]
	public string CorrelationId { get; set; } = "";

	// Emits each entry as a flat sibling member: "customRoles": [...], "customName": "Jane Smith".
	[JsonExtensionData]
	public Dictionary<string, JsonElement> Claims { get; set; } = [];

	/// <summary>
	/// Projects a canonical claim map to Entra's flat inline shape. A single-valued claim
	/// becomes a scalar string; a multi-valued claim — and roles, always — becomes a string array.
	/// </summary>
	internal static EntraClaimsPayload From(string correlationId, IReadOnlyDictionary<string, string[]> map) {
		var claims = new Dictionary<string, JsonElement>(map.Count, StringComparer.Ordinal);
		foreach (var (type, values) in map) {
			var scalar = values.Length == 1 && type != CustomClaimNames.Roles;
			claims[type] = scalar
				? JsonSerializer.SerializeToElement(values[0], EntraExternalIdJsonContext.Default.String)
				: JsonSerializer.SerializeToElement(values, EntraExternalIdJsonContext.Default.StringArray);
		}
		return new EntraClaimsPayload { CorrelationId = correlationId, Claims = claims };
	}
}
