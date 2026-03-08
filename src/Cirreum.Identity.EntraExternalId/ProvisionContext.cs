namespace Cirreum.Identity.EntraExternalId;

/// <summary>
/// Provides context about the user and calling application during Entra token issuance.
/// Passed to <see cref="IUserProvisioner"/> during the onTokenIssuanceStart callback.
/// </summary>
public sealed class ProvisionContext {

	/// <summary>
	/// Gets the external identity provider's unique identifier for the user being authenticated.
	/// </summary>
	/// <remarks>
	/// For Entra External ID this is the Entra object ID assigned to the user.
	/// Consuming applications typically store this as <c>ExternalUserId</c> on their own user record
	/// for future lookup.
	/// </remarks>
	public required string ExternalUserId { get; init; }

	/// <summary>
	/// Gets the correlation ID from the Entra callback payload.
	/// Used for tracing the token issuance request end-to-end.
	/// </summary>
	public required string CorrelationId { get; init; }

	/// <summary>
	/// Gets the app ID of the client application that initiated the authentication request.
	/// This is the application the user is signing into, not the Claims Extension app itself.
	/// </summary>
	public required string ClientAppId { get; init; }

	/// <summary>
	/// Gets the email address of the user being authenticated, sourced from the
	/// <c>mail</c> field in the Entra payload.
	/// </summary>
	/// <remarks>
	/// May be an empty string if the user's identity provider did not supply an email address
	/// (e.g., certain social identity providers with email sharing disabled).
	/// Provisioners performing email-based invitation lookup should handle this case.
	/// </remarks>
	public required string Email { get; init; }

}
