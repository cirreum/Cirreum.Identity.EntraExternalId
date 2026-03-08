namespace Cirreum.Identity.EntraExternalId;

/// <summary>
/// Modeling guide for the invitation entity used in
/// <see cref="UserProvisionerBase{TUser}.RedeemInvitationAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface on the invitation entity your application stores in its database.
/// It is not used as a generic constraint by <see cref="UserProvisionerBase{TUser}"/> —
/// the find, expiry check, and claim are performed atomically inside
/// <see cref="UserProvisionerBase{TUser}.RedeemInvitationAsync"/>, giving the
/// implementation full control over the transaction boundary.
/// </para>
/// <para>
/// Phase 1 (onTokenIssuanceStart) marks the invitation as <em>Claimed</em> and creates
/// the user record. Phase 2 (in-app onboarding) fully redeems the invitation, completes
/// the user profile, and assigns the Entra app role via Graph API.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class Invitation : IPendingInvitation {
///     public Guid Id { get; init; }
///     public string Email { get; init; } = "";
///     public string Role { get; init; } = Roles.User;
///     public DateTimeOffset ExpiresAt { get; init; }
///     public DateTimeOffset? ClaimedAt { get; set; }
///     public string? ClaimedByExternalUserId { get; set; }
///     public DateTimeOffset? RedeemedAt { get; set; }
/// }
/// </code>
/// </example>
public interface IPendingInvitation {

	/// <summary>
	/// Gets the email address the invitation was issued to.
	/// </summary>
	/// <remarks>
	/// Used to match against <see cref="ProvisionContext.Email"/> during token issuance.
	/// Lookups should be case-insensitive. If the authenticating user's email is empty
	/// (some social identity providers do not share it), the base provisioner will deny
	/// the login without attempting an invitation lookup.
	/// </remarks>
	string Email { get; }

	/// <summary>
	/// Gets the role to assign to the user upon successful invitation redemption.
	/// </summary>
	/// <remarks>
	/// Must be a non-empty string matching a role defined in your application.
	/// Use application-level constants (e.g. <c>Roles.Admin</c>) rather than magic strings.
	/// </remarks>
	string Role { get; }

	/// <summary>
	/// Gets the UTC date and time at which this invitation expires.
	/// </summary>
	DateTimeOffset ExpiresAt { get; }

	/// <summary>
	/// Gets a value indicating whether this invitation has passed its expiry date.
	/// </summary>
	/// <remarks>
	/// The default implementation compares <see cref="ExpiresAt"/> against
	/// <see cref="DateTimeOffset.UtcNow"/>. Override on your entity class if you need
	/// additional expiry conditions (e.g. already claimed, already redeemed).
	/// </remarks>
	bool IsExpired => this.ExpiresAt < DateTimeOffset.UtcNow;

}
