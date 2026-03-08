# Cirreum.Identity.EntraExternalId

## What This Library Does

Handles the Microsoft Entra External ID `onTokenIssuanceStart` custom authentication extension callback. When a user signs in via Entra External ID, this library:

1. Validates the bearer token sent by Entra
2. Provisions the user (find existing or redeem invitation)
3. Returns custom claims (roles) to embed in the issued ID token

This is a **server-side endpoint** that supports the **client's sign-in flow**. It can run in any ASP.NET host: the main API, an Azure Function, or a dedicated service.

## Architecture

- **Framework-independent** — does NOT depend on Cirreum.Core or any Cirreum framework libraries
- Lives in `C:\Cirreum\Common` (not Infrastructure or Runtime)
- Single NuGet package: `Cirreum.Identity.EntraExternalId`

## Key Types

### Public (contracts)
- `IUserProvisioner` — implement to control user access and provisioning
- `IProvisionedUser` — implement on your user entity (exposes `ExternalUserId` + `Roles`)
- `IPendingInvitation` — modeling guide for invitation entities
- `ProvisionResult` — discriminated union: `Allow(roles)` or `Deny()`
- `ProvisionContext` — context passed to provisioner (`ExternalUserId`, `Email`, `CorrelationId`, `ClientAppId`)
- `UserProvisionerBase<TUser>` — standard find-user/redeem-invitation base class
- `EntraExternalIdOptions` — configuration options
- `EntraExternalIdExtensions` — DI registration + endpoint mapping

### Internal
- `EntraTokenValidator` — OIDC + Entra appid/azp validation
- `EntraExternalIdHandler` — request orchestration
- `EntraExternalIdJsonContext` — AOT-compatible source-gen JSON
- `Models/EntraClaimsRequest` — Entra callback request DTOs
- `Models/EntraClaimsResponse` — Entra callback response DTOs

## Commands

```powershell
# Build
dotnet build Cirreum.Identity.EntraExternalId.slnx

# Pack
dotnet pack Cirreum.Identity.EntraExternalId.slnx -c Release -o ./artifacts
```

## Configuration Section

Default: `Cirreum:Identity:EntraExternalId`

## Testing

There are no test projects in this repository. The library is tested via integration with consuming applications.

## Security Design

- The callback endpoint is `AllowAnonymous` — authentication is performed internally by validating the Entra bearer token
- Token validation uses OIDC discovery for signing key rotation
- The `appid`/`azp` claim is validated against the configured `EntraAppId`
- Calling applications are validated against an `AllowedAppIds` allowlist
