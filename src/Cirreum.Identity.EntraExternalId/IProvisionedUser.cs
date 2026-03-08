namespace Cirreum.Identity.EntraExternalId;

/// <summary>
/// Constrains the user entity returned by <see cref="UserProvisionerBase{TUser}"/>
/// to expose the fields required for token issuance.
/// </summary>
/// <remarks>
/// Implement this interface on whatever user entity your application stores in its database.
/// The base provisioner only reads <see cref="ExternalUserId"/> for lookup and <see cref="Roles"/>
/// for the issued token — all other fields on your type are invisible to this library.
/// </remarks>
/// <example>
/// <code>
/// public sealed class AppUser : IProvisionedUser {
///     public Guid Id { get; init; }
///     public string ExternalUserId { get; init; } = "";
///     public string Email { get; init; } = "";
///     public IReadOnlyList&lt;string&gt; Roles { get; init; } = [AppRoles.User];
///     // ... other application fields
/// }
/// </code>
/// </example>
public interface IProvisionedUser {

	/// <summary>
	/// Gets the external identity provider's unique identifier for this user, as stored
	/// in the application database.
	/// </summary>
	/// <remarks>
	/// This must match the value of <see cref="ProvisionContext.ExternalUserId"/> from the
	/// token issuance callback. Consuming apps typically store this as <c>ExternalUserId</c>
	/// on their user record.
	/// </remarks>
	string ExternalUserId { get; }

	/// <summary>
	/// Gets the roles to embed in the issued token for this user.
	/// </summary>
	/// <remarks>
	/// Each value must be a non-empty string matching a role defined in your application.
	/// Use application-level constants (e.g. <c>AppRoles.Admin</c>) rather than magic strings.
	/// Most users have a single role; return a list with one entry for the common case.
	/// </remarks>
	IReadOnlyList<string> Roles { get; }

}
