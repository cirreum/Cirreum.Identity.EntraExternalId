namespace Cirreum.Identity.EntraExternalId;

using Cirreum.Identity.EntraExternalId.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Handles the Entra External ID onTokenIssuanceStart callback.
/// Validates the bearer token, verifies the calling app is allowed,
/// provisions the user via <see cref="IUserProvisioner"/>,
/// and returns initial role claims for the issued token.
/// </summary>
internal sealed partial class EntraExternalIdHandler(
	EntraTokenValidator tokenValidator,
	IOptions<EntraExternalIdOptions> options,
	IServiceProvider services,
	ILogger<EntraExternalIdHandler> logger
) {

	public async Task<IResult> HandleAsync(HttpRequest request, CancellationToken cancellationToken = default) {

		// -------------------------------------------------------------------------
		// 1. Validate bearer token
		// -------------------------------------------------------------------------

		if (!request.Headers.TryGetValue("Authorization", out var authHeader)
			|| string.IsNullOrWhiteSpace(authHeader)) {
			Log.MissingAuthorizationHeader(logger);
			return Results.Unauthorized();
		}

		var token = authHeader.ToString().Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
		if (!await tokenValidator.ValidateAsync(token, cancellationToken)) {
			Log.InvalidToken(logger);
			return Results.Unauthorized();
		}

		// -------------------------------------------------------------------------
		// 2. Deserialize payload
		// -------------------------------------------------------------------------

		var payload = await request.ReadFromJsonAsync<EntraClaimsRequest>(cancellationToken);
		if (payload is null) {
			Log.DeserializationFailed(logger);
			return Results.BadRequest("Invalid request body");
		}

		var context = payload.Data.AuthenticationContext;

		// -------------------------------------------------------------------------
		// 3. Validate required fields
		// -------------------------------------------------------------------------

		if (string.IsNullOrWhiteSpace(context.CorrelationId)) {
			Log.MissingCorrelationId(logger);
			return Results.BadRequest("Missing CorrelationId");
		}

		if (string.IsNullOrWhiteSpace(context.User.Id)) {
			Log.MissingUserId(logger);
			return Results.BadRequest("Missing User Id");
		}

		// -------------------------------------------------------------------------
		// 4. Validate calling app is on the allowlist
		// -------------------------------------------------------------------------

		var config = options.Value;
		var allowedApps = config.GetAllowedAppIdSet();
		if (!allowedApps.Contains(context.ClientServicePrincipal.AppId)) {
			Log.AppNotAllowed(logger, context.ClientServicePrincipal.AppId);
			return Results.Forbid();
		}

		// -------------------------------------------------------------------------
		// 5. Provision user
		// -------------------------------------------------------------------------

		var provisionContext = new ProvisionContext {
			ExternalUserId = context.User.Id,
			CorrelationId = context.CorrelationId,
			ClientAppId = context.ClientServicePrincipal.AppId,
			Email = context.User.Mail
		};

		var provisioner = services.GetRequiredService<IUserProvisioner>();
		ProvisionResult provisionResult;
		try {
			provisionResult = await provisioner.ProvisionAsync(provisionContext, cancellationToken);
		} catch (Exception ex) {
			Log.ProvisionerFailed(logger, ex, provisionContext.ExternalUserId);
			return Results.Problem("User provisioning failed.", statusCode: 500);
		}

		// -------------------------------------------------------------------------
		// 6. Map provision result to roles
		// -------------------------------------------------------------------------

		if (provisionResult is ProvisionResult.Denied) {
			Log.UserDenied(logger, provisionContext.ExternalUserId);
			return Results.Forbid();
		}

		if (provisionResult is not ProvisionResult.Allowed { Roles: { Count: > 0 } roles }) {
			if (provisionResult is ProvisionResult.Allowed) {
				Log.ProvisionerAllowedWithNoRoles(logger, provisionContext.ExternalUserId);
			} else {
				Log.ProvisionerFailed(logger, null, provisionContext.ExternalUserId);
			}
			return Results.Problem("User provisioning failed.", statusCode: 500);
		}

		var rolesString = string.Join(", ", roles);
		Log.IssuingRoles(logger, rolesString, context.User.Id, context.CorrelationId);

		// -------------------------------------------------------------------------
		// 7. Build and return response
		// -------------------------------------------------------------------------

		var response = new EntraClaimsResponse {
			Data = new EntraResponseData {
				Actions = [
					new EntraTokenAction {
						Claims = new EntraCustomClaims {
							CorrelationId = context.CorrelationId,
							CustomRoles = [.. roles]
						}
					}
				]
			}
		};

		var json = System.Text.Json.JsonSerializer.Serialize(
			response,
			EntraExternalIdJsonContext.Default.EntraClaimsResponse);

		return Results.Content(json, "application/json");
	}

	// -------------------------------------------------------------------------
	// Logging
	// -------------------------------------------------------------------------

	private static partial class Log {
		[LoggerMessage(Level = LogLevel.Warning, Message = "Missing Authorization header.")]
		internal static partial void MissingAuthorizationHeader(ILogger logger);

		[LoggerMessage(Level = LogLevel.Warning, Message = "Invalid Authorization token.")]
		internal static partial void InvalidToken(ILogger logger);

		[LoggerMessage(Level = LogLevel.Warning, Message = "Failed to deserialize request body.")]
		internal static partial void DeserializationFailed(ILogger logger);

		[LoggerMessage(Level = LogLevel.Warning, Message = "Missing CorrelationId in request.")]
		internal static partial void MissingCorrelationId(ILogger logger);

		[LoggerMessage(Level = LogLevel.Warning, Message = "Missing User Id in request.")]
		internal static partial void MissingUserId(ILogger logger);

		[LoggerMessage(Level = LogLevel.Warning, Message = "App '{AppId}' is not in the allowed list.")]
		internal static partial void AppNotAllowed(ILogger logger, string appId);

		[LoggerMessage(Level = LogLevel.Information, Message = "User '{UserId}' was denied by provisioner. Blocking token issuance.")]
		internal static partial void UserDenied(ILogger logger, string userId);

		[LoggerMessage(Level = LogLevel.Warning, Message = "Provisioner returned Allowed with no roles for user '{UserId}'. Blocking token issuance.")]
		internal static partial void ProvisionerAllowedWithNoRoles(ILogger logger, string userId);

		[LoggerMessage(Level = LogLevel.Error, Message = "Provisioner failed for user '{UserId}'. Blocking token issuance.")]
		internal static partial void ProvisionerFailed(ILogger logger, Exception? ex, string userId);

		[LoggerMessage(Level = LogLevel.Information, Message = "Issuing roles '{Roles}' for user '{UserId}' (correlation: {CorrelationId}).")]
		internal static partial void IssuingRoles(ILogger logger, string roles, string userId, string correlationId);
	}

}
