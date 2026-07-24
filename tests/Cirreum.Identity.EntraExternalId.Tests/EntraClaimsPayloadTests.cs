namespace Cirreum.Identity.EntraExternalId.Tests;

using System.Text.Json;
using Cirreum.Identity.EntraExternalId;
using Cirreum.Identity.EntraExternalId.Models;

public class EntraClaimsPayloadTests {

	private static IReadOnlyDictionary<string, string[]> MapOf(params IdentityClaim[] claims) =>
		((IReadOnlyList<IdentityClaim>)claims).ToClaimMap();

	[Fact]
	public void CorrelationId_is_a_fixed_member() {
		var payload = EntraClaimsPayload.From("corr-1", MapOf(IdentityClaim.Name("Jane Smith")));

		payload.CorrelationId.Should().Be("corr-1");
	}

	[Fact]
	public void A_single_valued_non_roles_claim_projects_as_a_scalar_string() {
		var payload = EntraClaimsPayload.From("c", MapOf(IdentityClaim.Name("Jane Smith")));

		payload.Claims[CustomClaimNames.Name].ValueKind.Should().Be(JsonValueKind.String);
		payload.Claims[CustomClaimNames.Name].GetString().Should().Be("Jane Smith");
	}

	[Fact]
	public void Roles_project_as_an_array_even_when_single_valued() {
		var payload = EntraClaimsPayload.From("c", MapOf(IdentityClaim.Roles("admin")));

		payload.Claims[CustomClaimNames.Roles].ValueKind.Should().Be(JsonValueKind.Array);
		payload.Claims[CustomClaimNames.Roles].EnumerateArray().Select(e => e.GetString())
			.Should().ContainSingle().Which.Should().Be("admin");
	}

	[Fact]
	public void A_multi_valued_claim_projects_as_an_array() {
		var payload = EntraClaimsPayload.From("c", MapOf(IdentityClaim.Roles("admin", "subscriber")));

		payload.Claims[CustomClaimNames.Roles].ValueKind.Should().Be(JsonValueKind.Array);
		payload.Claims[CustomClaimNames.Roles].EnumerateArray().Select(e => e.GetString())
			.Should().BeEquivalentTo("admin", "subscriber");
	}

	[Fact]
	public void An_empty_map_projects_to_a_payload_with_only_a_correlation_id() {
		var payload = EntraClaimsPayload.From("c", MapOf());

		payload.Claims.Should().BeEmpty();
		payload.CorrelationId.Should().Be("c");
	}

	[Fact]
	public void Claims_serialize_as_flat_inline_siblings_of_correlation_id() {
		var response = new EntraClaimsResponse {
			Data = new EntraResponseData {
				Actions = [
					new EntraTokenAction {
						Claims = EntraClaimsPayload.From("corr-9", MapOf(
							IdentityClaim.Roles("admin", "subscriber"),
							IdentityClaim.Name("Jane Smith"),
							IdentityClaim.Of("tenant", "acme")))
					}
				]
			}
		};

		var json = JsonSerializer.Serialize(response, EntraExternalIdJsonContext.Default.EntraClaimsResponse);

		using var doc = JsonDocument.Parse(json);
		var claims = doc.RootElement
			.GetProperty("data")
			.GetProperty("actions")[0]
			.GetProperty("claims");

		// correlationId + each custom* claim are flat siblings — no nesting.
		claims.GetProperty("correlationId").GetString().Should().Be("corr-9");
		claims.GetProperty("customRoles").ValueKind.Should().Be(JsonValueKind.Array);
		claims.GetProperty("customRoles").EnumerateArray().Select(e => e.GetString())
			.Should().BeEquivalentTo("admin", "subscriber");
		claims.GetProperty("customName").GetString().Should().Be("Jane Smith");
		claims.GetProperty("customTenant").GetString().Should().Be("acme");
	}

	[Fact]
	public void The_fixed_odata_type_discriminators_are_preserved() {
		var response = new EntraClaimsResponse {
			Data = new EntraResponseData {
				Actions = [new EntraTokenAction { Claims = EntraClaimsPayload.From("c", MapOf(IdentityClaim.Roles("admin"))) }]
			}
		};

		var json = JsonSerializer.Serialize(response, EntraExternalIdJsonContext.Default.EntraClaimsResponse);

		using var doc = JsonDocument.Parse(json);
		doc.RootElement.GetProperty("data").GetProperty("@odata.type").GetString()
			.Should().Be("microsoft.graph.onTokenIssuanceStartResponseData");
		doc.RootElement.GetProperty("data").GetProperty("actions")[0].GetProperty("@odata.type").GetString()
			.Should().Be("microsoft.graph.tokenIssuanceStart.provideClaimsForToken");
	}
}
