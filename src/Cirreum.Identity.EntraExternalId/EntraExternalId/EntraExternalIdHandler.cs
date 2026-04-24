namespace Cirreum.Identity.EntraExternalId;

using Cirreum.Identity.Configuration;
using Cirreum.Identity.EntraExternalId.Models;
using Cirreum.Identity.Provisioning;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles the Entra External ID <c>onTokenIssuanceStart</c> callback for a single
/// configured instance. Validates the bearer token, checks the calling app against any
/// configured allowlist, dispatches to the app's <see cref="IUserProvisioner"/>, and maps
/// the result to Microsoft's custom-claims response envelope.
/// </summary>
internal sealed partial class EntraExternalIdHandler(
	EntraExternalIdIdentityProviderInstanceSettings settings,
	EntraTokenValidator tokenValidator,
	IServiceProvider services,
	ILogger<EntraExternalIdHandler> logger) {

	public async Task<IResult> HandleAsync(HttpRequest request, CancellationToken cancellationToken = default) {

		// 1. Validate bearer token
		if (!request.Headers.TryGetValue("Authorization", out var authHeader)
			|| string.IsNullOrWhiteSpace(authHeader)) {
			Log.MissingAuthorizationHeader(logger, settings.Source);
			return Results.Unauthorized();
		}

		var token = authHeader.ToString().Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
		if (!await tokenValidator.ValidateAsync(token, cancellationToken)) {
			Log.InvalidToken(logger, settings.Source);
			return Results.Unauthorized();
		}

		// 2. Deserialize payload
		EntraClaimsRequest? payload;
		try {
			payload = await request.ReadFromJsonAsync(
				EntraExternalIdJsonContext.Default.EntraClaimsRequest,
				cancellationToken);
		} catch (Exception ex) {
			Log.DeserializationFailed(logger, ex, settings.Source);
			return Results.BadRequest("Invalid request body");
		}

		if (payload is null) {
			Log.DeserializationFailed(logger, null, settings.Source);
			return Results.BadRequest("Invalid request body");
		}

		var context = payload.Data.AuthenticationContext;

		// 3. Validate required fields
		if (string.IsNullOrWhiteSpace(context.CorrelationId)) {
			Log.MissingCorrelationId(logger, settings.Source);
			return Results.BadRequest("Missing CorrelationId");
		}

		if (string.IsNullOrWhiteSpace(context.User.Id)) {
			Log.MissingUserId(logger, settings.Source);
			return Results.BadRequest("Missing User Id");
		}

		// 4. Validate calling app (when an allowlist is configured)
		if (settings.HasAllowedAppIds()) {
			var allowedApps = settings.GetAllowedAppIdSet();
			if (!allowedApps.Contains(context.ClientServicePrincipal.AppId)) {
				Log.AppNotAllowed(logger, context.ClientServicePrincipal.AppId, settings.Source);
				return Results.Forbid();
			}
		}

		// 5. Provision user
		var provisionContext = new ProvisionContext {
			Source = settings.Source,
			ExternalUserId = context.User.Id,
			CorrelationId = context.CorrelationId,
			ClientAppId = context.ClientServicePrincipal.AppId,
			Email = context.User.Mail
		};

		var provisioner = services.GetRequiredKeyedService<IUserProvisioner>(settings.Source);
		ProvisionResult provisionResult;
		try {
			provisionResult = await provisioner.ProvisionAsync(provisionContext, cancellationToken);
		} catch (Exception ex) {
			Log.ProvisionerFailed(logger, ex, provisionContext.ExternalUserId, settings.Source);
			return Results.Problem("User provisioning failed.", statusCode: 500);
		}

		// 6. Map provision result to response
		if (provisionResult is ProvisionResult.Denied) {
			Log.UserDenied(logger, provisionContext.ExternalUserId, settings.Source);
			return Results.Forbid();
		}

		if (provisionResult is not ProvisionResult.Allowed { Roles: { Count: > 0 } roles }) {
			if (provisionResult is ProvisionResult.Allowed) {
				Log.ProvisionerAllowedWithNoRoles(logger, provisionContext.ExternalUserId, settings.Source);
			} else {
				Log.ProvisionerFailed(logger, null, provisionContext.ExternalUserId, settings.Source);
			}
			return Results.Problem("User provisioning failed.", statusCode: 500);
		}

		var rolesStr = string.Join(", ", roles);
		Log.IssuingRoles(logger, rolesStr, provisionContext.ExternalUserId, context.CorrelationId, settings.Source);

		// 7. Build and return response
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

		return Results.Json(response, EntraExternalIdJsonContext.Default.EntraClaimsResponse);
	}

	private static partial class Log {
		[LoggerMessage(Level = LogLevel.Warning, Message = "Missing Authorization header on EntraExternalId request for instance '{Source}'.")]
		internal static partial void MissingAuthorizationHeader(ILogger logger, string source);

		[LoggerMessage(Level = LogLevel.Warning, Message = "Invalid Authorization token on EntraExternalId request for instance '{Source}'.")]
		internal static partial void InvalidToken(ILogger logger, string source);

		[LoggerMessage(Level = LogLevel.Warning, Message = "Failed to deserialize EntraExternalId request body for instance '{Source}'.")]
		internal static partial void DeserializationFailed(ILogger logger, Exception? ex, string source);

		[LoggerMessage(Level = LogLevel.Warning, Message = "Missing CorrelationId in EntraExternalId request for instance '{Source}'.")]
		internal static partial void MissingCorrelationId(ILogger logger, string source);

		[LoggerMessage(Level = LogLevel.Warning, Message = "Missing User Id in EntraExternalId request for instance '{Source}'.")]
		internal static partial void MissingUserId(ILogger logger, string source);

		[LoggerMessage(Level = LogLevel.Warning, Message = "App '{AppId}' is not in the allowed list for instance '{Source}'.")]
		internal static partial void AppNotAllowed(ILogger logger, string appId, string source);

		[LoggerMessage(Level = LogLevel.Information, Message = "User '{UserId}' was denied by provisioner for instance '{Source}'. Blocking token issuance.")]
		internal static partial void UserDenied(ILogger logger, string userId, string source);

		[LoggerMessage(Level = LogLevel.Warning, Message = "Provisioner returned Allowed with no roles for user '{UserId}' on instance '{Source}'. Blocking token issuance.")]
		internal static partial void ProvisionerAllowedWithNoRoles(ILogger logger, string userId, string source);

		[LoggerMessage(Level = LogLevel.Error, Message = "Provisioner failed for user '{UserId}' on instance '{Source}'. Blocking token issuance.")]
		internal static partial void ProvisionerFailed(ILogger logger, Exception? ex, string userId, string source);

		[LoggerMessage(Level = LogLevel.Information, Message = "Issuing roles '{Roles}' for user '{UserId}' (correlation: {CorrelationId}, instance: {Source}).")]
		internal static partial void IssuingRoles(ILogger logger, string roles, string userId, string correlationId, string source);
	}
}
