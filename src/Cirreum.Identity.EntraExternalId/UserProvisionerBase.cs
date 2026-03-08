namespace Cirreum.Identity.EntraExternalId;

/// <summary>
/// Abstract base implementation of <see cref="IUserProvisioner"/> that handles the
/// standard invitation-redemption pattern used by most Cirreum applications.
/// </summary>
/// <typeparam name="TUser">
/// The application's user entity. Must implement <see cref="IProvisionedUser"/>.
/// </typeparam>
/// <remarks>
/// <para>
/// This base class encodes the two-path provisioning flow:
/// </para>
/// <list type="number">
///   <item><description>
///     <strong>Returning user:</strong> look up the user by external user ID →
///     if found, issue the stored role immediately.
///   </description></item>
///   <item><description>
///     <strong>New user via invitation:</strong> atomically find, validate, and claim a
///     pending invitation by email → issue the invitation's role.
///   </description></item>
/// </list>
/// <para>
/// Users without an existing record and without a valid invitation are denied.
/// Logins where the identity provider does not supply an email address are denied
/// if no user record is found, since invitation lookup requires an email.
/// </para>
/// <para>
/// Inherit from this class and implement the two abstract data-access methods.
/// The provisioning logic itself does not need to be overridden for the standard
/// invitation pattern, but you are free to override <see cref="ProvisionAsync"/> entirely
/// if your application requires a different flow.
/// </para>
/// <para>
/// Register as scoped so it can access database contexts and other request-scoped services:
/// <c>builder.AddEntraExternalId&lt;MyProvisioner&gt;()</c>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class AppUserProvisioner(AppDbContext db) : UserProvisionerBase&lt;AppUser&gt; {
///
///     protected override Task&lt;AppUser?&gt; FindUserAsync(
///         string externalUserId,
///         CancellationToken cancellationToken) =>
///         db.Users.FirstOrDefaultAsync(u => u.ExternalUserId == externalUserId, cancellationToken);
///
///     protected override async Task&lt;AppUser?&gt; RedeemInvitationAsync(
///         string email,
///         string externalUserId,
///         CancellationToken cancellationToken) {
///         var invitation = await db.Invitations
///             .FirstOrDefaultAsync(
///                 i => i.Email == email.ToLowerInvariant()
///                   &amp;&amp; i.ClaimedAt == null
///                   &amp;&amp; i.ExpiresAt > DateTimeOffset.UtcNow,
///                 cancellationToken);
///         if (invitation is null) {
///             return null;
///         }
///         invitation.ClaimedAt = DateTimeOffset.UtcNow;
///         invitation.ClaimedByExternalUserId = externalUserId;
///         var user = new AppUser { ExternalUserId = externalUserId, Roles = invitation.Roles };
///         db.Users.Add(user);
///         await db.SaveChangesAsync(cancellationToken);
///         return user;
///     }
/// }
/// </code>
/// </example>
public abstract class UserProvisionerBase<TUser> : IUserProvisioner
	where TUser : IProvisionedUser {

	/// <inheritdoc />
	/// <remarks>
	/// Executes the standard two-path flow: returning user lookup → invitation redemption.
	/// Override this method only if your application requires a fundamentally different
	/// provisioning strategy.
	/// </remarks>
	public virtual async Task<ProvisionResult> ProvisionAsync(
		ProvisionContext context,
		CancellationToken cancellationToken = default) {

		var user = await this.FindUserAsync(context.ExternalUserId, cancellationToken);
		if (user is not null) {
			return ProvisionResult.Allow(user.Roles);
		}

		if (string.IsNullOrWhiteSpace(context.Email)) {
			return ProvisionResult.Deny();
		}

		var newUser = await this.RedeemInvitationAsync(context.Email, context.ExternalUserId, cancellationToken);
		return newUser is not null
			? ProvisionResult.Allow(newUser.Roles)
			: ProvisionResult.Deny();
	}

	/// <summary>
	/// Looks up an existing user by their external identity provider user ID.
	/// </summary>
	/// <param name="externalUserId">
	/// The external user ID from the token issuance callback. Stored as <c>ExternalUserId</c>
	/// on the application's user record.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// The matching user, or <see langword="null"/> if no record exists for this external user ID.
	/// </returns>
	protected abstract Task<TUser?> FindUserAsync(
		string externalUserId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Atomically finds, validates, and claims a pending invitation by email address,
	/// and creates the application user record. This is Phase 1 of the two-phase onboarding flow.
	/// </summary>
	/// <param name="email">
	/// The email address from the Entra token issuance callback.
	/// Implementations should match case-insensitively.
	/// </param>
	/// <param name="externalUserId">
	/// The external user ID of the authenticating user. Store this on the new user record
	/// so <see cref="FindUserAsync"/> can retrieve them on subsequent logins.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// The newly created user record if a valid invitation was found and successfully claimed,
	/// or <see langword="null"/> if no matching invitation exists, it has expired, or it has
	/// already been claimed.
	/// The base class reads <see cref="IProvisionedUser.Roles"/> from the returned
	/// instance to populate the issued token.
	/// </returns>
	/// <remarks>
	/// <para>
	/// Implementations should perform the find, expiry check, and claim in a single
	/// database transaction to prevent concurrent logins from redeeming the same invitation twice.
	/// At minimum, the implementation should:
	/// </para>
	/// <list type="bullet">
	///   <item><description>Query for an unclaimed, non-expired invitation matching <paramref name="email"/></description></item>
	///   <item><description>Return <see langword="null"/> immediately if none is found</description></item>
	///   <item><description>Mark the invitation as Claimed (e.g. set <c>ClaimedAt</c>, <c>ClaimedByExternalUserId</c>)</description></item>
	///   <item><description>Create a new user record with <paramref name="externalUserId"/> and the invitation's role</description></item>
	///   <item><description>Persist both changes atomically</description></item>
	/// </list>
	/// <para>
	/// Phase 2 (in-app onboarding) completes the redemption: collecting any remaining profile
	/// data, calling the Graph API to assign the Entra app role, and marking the invitation
	/// as fully Redeemed.
	/// </para>
	/// </remarks>
	protected abstract Task<TUser?> RedeemInvitationAsync(
		string email,
		string externalUserId,
		CancellationToken cancellationToken);

}
