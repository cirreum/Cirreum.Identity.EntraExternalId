# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is **Cirreum.Identity.EntraExternalId**, the Infrastructure-layer implementation of the Cirreum Identity provider pattern for Microsoft Entra External ID (CIAM). It registers the custom authentication extension (`onTokenIssuanceStart`) endpoint: validates the inbound Microsoft-signed JWT, deserializes Microsoft's fixed request envelope, dispatches to the app's `IUserProvisioner`, and returns custom claims in Microsoft's fixed response envelope.

## Build Commands

```bash
# Build the solution
dotnet build Cirreum.Identity.EntraExternalId.slnx

# Build the project
dotnet build src/Cirreum.Identity.EntraExternalId/Cirreum.Identity.EntraExternalId.csproj

# Pack for local release (uses version 1.0.100-rc)
dotnet pack --configuration Release
```

## Architecture

### Core responsibility

For each enabled instance in `Cirreum:Identity:Providers:EntraExternalId:Instances:*`, this package:

1. **Services phase** — registers a per-instance keyed-singleton `EntraTokenValidator`. Each validator owns its own `ConfigurationManager<OpenIdConnectConfiguration>` so signing-key caches are isolated per Entra tenant.
2. **Endpoints phase** — maps an anonymous `MapPost` at `settings.Route` that runs the `EntraExternalIdHandler` flow.

### Request flow (`EntraExternalIdHandler.HandleAsync`)

1. Read `Authorization` header → extract bearer token (401 on missing).
2. Validate JWT via `EntraTokenValidator.ValidateAsync` — issuer, audience, lifetime, signing key, `appid`/`azp` (401 on any failure).
3. Deserialize Microsoft-shaped payload (400 on bad body).
4. Require `correlationId` and `user.id` (400 if missing).
5. Check `clientServicePrincipal.appId` against `AllowedAppIds` when the allowlist is configured (403 on mismatch).
6. Resolve `IUserProvisioner` keyed by `settings.Source` (= instance key).
7. Invoke `ProvisionAsync` with the built `ProvisionContext`.
8. Map `ProvisionResult`:
   - `Allowed { Roles: [...] }` → 200 with Microsoft-shaped response envelope (customRoles + correlationId embedded)
   - `Allowed { Roles: [] }` → 500 (provisioner bug — allow-no-roles)
   - `Denied` → **403 Forbidden** (no body — Entra treats any non-200 as failure)
   - Exception → 500

### Key types

| Type | Namespace | Visibility | Purpose |
|---|---|---|---|
| `EntraExternalIdIdentityProviderRegistrar` | `Cirreum.Identity` | public | Registers services + maps endpoints per instance |
| `EntraExternalIdIdentityProviderSettings` | `Cirreum.Identity.Configuration` | public | Provider settings container |
| `EntraExternalIdIdentityProviderInstanceSettings` | `Cirreum.Identity.Configuration` | public | Per-instance settings |
| `EntraExternalIdHandler` | `Cirreum.Identity.EntraExternalId` | internal | HTTP handler |
| `EntraTokenValidator` | `Cirreum.Identity.EntraExternalId` | internal | OIDC-discovery JWT validation |
| `EntraExternalIdJsonContext` | `Cirreum.Identity.EntraExternalId` | internal | Source-gen JSON context |
| `EntraClaimsRequest` / `EntraClaimsResponse` + nested records | `Cirreum.Identity.EntraExternalId.Models` | internal | Microsoft wire DTOs |

### RootNamespace

The csproj sets `<RootNamespace>Cirreum.Identity</RootNamespace>` so folder conventions map to the intended sub-namespaces:

- `src/Cirreum.Identity.EntraExternalId/` → `Cirreum.Identity` (registrar)
- `src/Cirreum.Identity.EntraExternalId/EntraExternalId/` → `Cirreum.Identity.EntraExternalId` (impl)
- `src/Cirreum.Identity.EntraExternalId/EntraExternalId/Models/` → `Cirreum.Identity.EntraExternalId.Models` (wire DTOs)
- `src/Cirreum.Identity.EntraExternalId/Configuration/` → `Cirreum.Identity.Configuration` (settings)

## Important quirks (from past session memory)

- **`Issuer` MUST use the tenant-ID subdomain format** (`https://<tenant-id>.ciamlogin.com/<tenant-id>/v2.0`), NOT the domain-name format. Wrong format causes silent token-validation failure.
- **`acceptMappedClaims`** must be set to `true` in the app manifest's `api` section — no Azure Portal UI toggle exists.
- **`customRoles` is the custom claim name** issued here; clients remap it to `roles` before `ClaimsPrincipal` construction (e.g. via an `IClaimsExtender`).
- **`EntraAppId` default (`99045fe1-7639-4a75-9d4a-577b6ca3810f`)** is Microsoft's service-app ID for all Entra External ID tenants — override only if Microsoft changes it.

## Dependencies

- **Cirreum.IdentityProvider** (v1.0.1+) — base registrar, provisioning contracts, settings base types
- **Microsoft.IdentityModel.Protocols.OpenIdConnect** — OIDC discovery / JWKS fetch
- **System.IdentityModel.Tokens.Jwt** — JWT validation
- **Microsoft.AspNetCore.App** — `IEndpointRouteBuilder`, `HttpRequest`, `Results`, etc.

## What's not here

- **App-facing extensions** (`AddEntraExternalIdIdentity`, `MapEntraExternalIdIdentity`) — those live in the Runtime Extensions package `Cirreum.Runtime.Identity.EntraExternalId`. The app never touches `EntraExternalIdIdentityProviderRegistrar` directly.
- **The app's `IUserProvisioner`** — registered by the app via the Runtime Extensions layer as a keyed scoped service, resolved here at callback time.

## Development Notes

- Uses .NET 10.0 with latest C# language version
- Nullable reference types enabled
- Source-generated JSON serialization (`JsonSerializerContext`) for AOT-friendly, low-allocation body handling
- Per-instance token validator keeps per-tenant OIDC config + signing keys cached independently
- Handler is stateless — constructed per request from keyed validator, per-instance settings, IServiceProvider, and logger
- File-scoped namespaces throughout
- K&R braces, tabs for indentation (matches repo `.editorconfig`)
