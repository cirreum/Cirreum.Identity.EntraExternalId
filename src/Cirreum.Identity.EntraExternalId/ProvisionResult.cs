namespace Cirreum.Identity.EntraExternalId;

/// <summary>
/// Represents the outcome of a user provisioning attempt during Entra token issuance.
/// </summary>
/// <remarks>
/// Use the static factory methods to construct a result:
/// <list type="bullet">
///   <item><description><see cref="Allow(string[])"/> — grant access with the specified roles</description></item>
///   <item><description><see cref="Deny()"/> — block token issuance for this user</description></item>
/// </list>
/// </remarks>
public abstract record ProvisionResult {

	// Private constructor prevents subclassing outside this type.
	// Nested records have access to private members of the enclosing type.
	private ProvisionResult() { }

	/// <summary>
	/// The user is allowed; the specified roles will be embedded in the issued token.
	/// </summary>
	public sealed record Allowed(IReadOnlyList<string> Roles) : ProvisionResult;

	/// <summary>
	/// The user is denied; token issuance will be blocked.
	/// </summary>
	public sealed record Denied : ProvisionResult;

	/// <summary>
	/// Grants the user access and embeds the specified roles in the issued token.
	/// </summary>
	/// <param name="roles">One or more role values to embed as custom claims.</param>
	public static ProvisionResult Allow(params string[] roles) => new Allowed(roles);

	/// <summary>
	/// Grants the user access and embeds the specified roles in the issued token.
	/// </summary>
	/// <param name="roles">The role values to embed as custom claims.</param>
	public static ProvisionResult Allow(IReadOnlyList<string> roles) => new Allowed(roles);

	/// <summary>
	/// Blocks token issuance for this user. The login will be rejected by Entra.
	/// </summary>
	public static ProvisionResult Deny() => new Denied();

}
