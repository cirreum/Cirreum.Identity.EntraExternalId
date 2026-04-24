namespace Cirreum.Identity.Configuration;

/// <summary>
/// Settings container for Microsoft Entra External ID identity provider instances.
/// Maps to: <c>Cirreum:Identity:Providers:EntraExternalId</c>.
/// </summary>
public sealed class EntraExternalIdIdentityProviderSettings
	: IdentityProviderSettings<EntraExternalIdIdentityProviderInstanceSettings>;
