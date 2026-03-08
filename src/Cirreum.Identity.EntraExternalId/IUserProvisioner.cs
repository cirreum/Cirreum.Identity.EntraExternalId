namespace Cirreum.Identity.EntraExternalId;

/// <summary>
/// Provisions and validates a user account during Entra External ID token issuance.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface to hook into the Entra onTokenIssuanceStart callback and
/// perform application-specific provisioning and access control logic before the token
/// is issued. This is the earliest possible hook in the authentication flow — the token
/// has not yet been issued when this is called.
/// </para>
/// <para>
/// The provisioner is the sole authority on whether a user is allowed into your application.
/// Authenticating successfully against Entra does not grant access — your provisioner
/// decides. Common implementations will:
/// </para>
/// <list type="bullet">
///   <item><description>Validate the user account exists in the application database; deny if not</description></item>
///   <item><description>Create the account and assign a role on first sign-up if applicable</description></item>
///   <item><description>Redeem a pending invitation and assign the invited role</description></item>
///   <item><description>Look up and return the user's current role(s)</description></item>
/// </list>
/// <para>
/// Registration is required. Call <c>services.AddEntraExternalId&lt;TProvisioner&gt;()</c>
/// during app startup. An <see cref="InvalidOperationException"/> will be thrown at startup
/// if no provisioner has been registered.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class MyAppUserProvisioner(
///     IUserRepository users,
///     IInvitationService invitations
/// ) : IUserProvisioner {
///
///     public async Task&lt;ProvisionResult&gt; ProvisionAsync(
///         ProvisionContext context,
///         CancellationToken cancellationToken = default) {
///
///         var invitation = await invitations.RedeemAsync(context.ExternalUserId, cancellationToken);
///         if (invitation is not null) {
///             await users.CreateAsync(context.ExternalUserId, invitation.Role, cancellationToken);
///             return ProvisionResult.Allow(invitation.Role);
///         }
///
///         var existing = await users.FindAsync(context.ExternalUserId, cancellationToken);
///         if (existing is not null) {
///             return ProvisionResult.Allow(existing.Role);
///         }
///
///         return ProvisionResult.Deny();
///     }
/// }
/// </code>
/// </example>
public interface IUserProvisioner {

	/// <summary>
	/// Provisions the user and returns whether they are allowed into the application,
	/// along with the roles to embed in the issued token.
	/// </summary>
	/// <param name="context">
	/// Context about the user and calling application during token issuance.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// <see cref="ProvisionResult.Allow(string[])"/> with at least one role to permit the login,
	/// or <see cref="ProvisionResult.Deny()"/> to block token issuance.
	/// </returns>
	Task<ProvisionResult> ProvisionAsync(
		ProvisionContext context,
		CancellationToken cancellationToken = default);

}
