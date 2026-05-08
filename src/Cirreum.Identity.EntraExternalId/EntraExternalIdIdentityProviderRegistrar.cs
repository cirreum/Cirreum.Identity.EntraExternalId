namespace Cirreum.Identity;

using Cirreum.Identity.Configuration;
using Cirreum.Identity.EntraExternalId;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Registrar for Microsoft Entra External ID identity provider instances. Wires up a
/// custom authentication extension (onTokenIssuanceStart) endpoint per configured instance
/// that validates Entra-signed tokens and provisions users before token issuance.
/// </summary>
/// <remarks>
/// <para>
/// For each enabled instance in configuration, this registrar:
/// </para>
/// <list type="number">
///   <item><description>Registers a keyed-singleton <see cref="EntraTokenValidator"/> for the instance (services phase). The validator owns a per-instance <c>ConfigurationManager&lt;OpenIdConnectConfiguration&gt;</c> that caches signing keys from the tenant's OIDC discovery endpoint.</description></item>
///   <item><description>Maps an anonymous POST endpoint at <c>settings.Route</c> that validates the inbound JWT, deserializes the Microsoft-shaped payload, resolves the keyed <see cref="Provisioning.IUserProvisioner"/> for <c>settings.Source</c>, and translates the result into Entra's custom-claims response envelope (endpoints phase).</description></item>
/// </list>
/// <para>
/// The keyed <see cref="Provisioning.IUserProvisioner"/> that fulfils each instance is
/// registered separately by the app through the Runtime Extensions layer
/// (<c>builder.AddIdentity().AddProvisioner&lt;T&gt;("instance_key")</c>).
/// </para>
/// </remarks>
public sealed class EntraExternalIdIdentityProviderRegistrar
	: IdentityProviderRegistrar<EntraExternalIdIdentityProviderSettings, EntraExternalIdIdentityProviderInstanceSettings> {

	/// <inheritdoc/>
	public override string ProviderName => "EntraExternalId";

	/// <inheritdoc/>
	public override void ValidateSettings(EntraExternalIdIdentityProviderInstanceSettings settings) {

		if (string.IsNullOrWhiteSpace(settings.ClientId)) {
			throw new InvalidOperationException(
				$"EntraExternalId instance '{settings.Source}' requires ClientId (the Application ID of the custom claims provider app registration).");
		}

		if (string.IsNullOrWhiteSpace(settings.Issuer)) {
			throw new InvalidOperationException(
				$"EntraExternalId instance '{settings.Source}' requires Issuer. " +
				$"Use the tenant-ID subdomain format: https://<tenant-id>.ciamlogin.com/<tenant-id>/v2.0 " +
				$"(NOT the domain-name format — using the wrong format causes silent token-validation failure).");
		}

		if (string.IsNullOrWhiteSpace(settings.MetadataEndpoint)) {
			throw new InvalidOperationException(
				$"EntraExternalId instance '{settings.Source}' requires MetadataEndpoint. " +
				$"Typically https://<tenant-id>.ciamlogin.com/<tenant-id>/v2.0/.well-known/openid-configuration.");
		}

		if (string.IsNullOrWhiteSpace(settings.EntraAppId)) {
			throw new InvalidOperationException(
				$"EntraExternalId instance '{settings.Source}' requires EntraAppId. " +
				$"Default value is '99045fe1-7639-4a75-9d4a-577b6ca3810f' — the well-known Microsoft service app.");
		}

		if (settings.ClockSkewMinutes < 0) {
			throw new InvalidOperationException(
				$"EntraExternalId instance '{settings.Source}' has invalid ClockSkewMinutes. Must be >= 0.");
		}
	}

	/// <inheritdoc/>
	protected override void RegisterProvisioner(
		string key,
		EntraExternalIdIdentityProviderInstanceSettings settings,
		IServiceCollection services,
		IConfiguration configuration) {

		// Per-instance token validator — holds the OIDC discovery ConfigurationManager and
		// its cached signing keys for this tenant. Singleton scope is required so the key
		// cache persists across requests.
		services.AddKeyedSingleton(key, (sp, _) =>
			new EntraTokenValidator(
				settings,
				sp.GetRequiredService<ILogger<EntraTokenValidator>>()));
	}

	/// <inheritdoc/>
	protected override void MapProvisioner(
		string key,
		EntraExternalIdIdentityProviderInstanceSettings settings,
		IEndpointRouteBuilder endpoints) {

		endpoints.MapPost(settings.Route, async (HttpContext ctx, CancellationToken ct) => {
			var sp = ctx.RequestServices;
			var validator = sp.GetRequiredKeyedService<EntraTokenValidator>(key);
			var logger = sp.GetRequiredService<ILogger<EntraExternalIdHandler>>();
			var handler = new EntraExternalIdHandler(settings, validator, sp, logger);
			return await handler.HandleAsync(ctx.Request, ct);
		})
		.AllowAnonymous()
		.ExcludeFromDescription();
	}
}
