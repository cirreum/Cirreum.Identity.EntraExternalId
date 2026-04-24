# Cirreum Identity EntraExternalId

[![NuGet Version](https://img.shields.io/nuget/v/Cirreum.Identity.EntraExternalId.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Identity.EntraExternalId/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Cirreum.Identity.EntraExternalId.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Identity.EntraExternalId/)
[![GitHub Release](https://img.shields.io/github/v/release/cirreum/Cirreum.Identity.EntraExternalId?style=flat-square&labelColor=1F1F1F&color=FF3B2E)](https://github.com/cirreum/Cirreum.Identity.EntraExternalId/releases)
[![License](https://img.shields.io/github/license/cirreum/Cirreum.Identity.EntraExternalId?style=flat-square&labelColor=1F1F1F&color=F2F2F2)](https://github.com/cirreum/Cirreum.Identity.EntraExternalId/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-003D8F?style=flat-square&labelColor=1F1F1F)](https://dotnet.microsoft.com/)

**Microsoft Entra External ID integration for Cirreum — the Infrastructure-layer library that implements the custom authentication extension (`onTokenIssuanceStart`) callback: validates Entra-signed tokens, provisions users, and returns custom claims for the issued token.**

## Overview

`Cirreum.Identity.EntraExternalId` is the Infrastructure-layer implementation of the Cirreum Identity provider pattern for Microsoft Entra External ID (CIAM). For each configured instance it registers an HTTP endpoint that Entra calls during sign-in to:

1. Authenticate itself with a Microsoft-signed bearer token (validated against Entra's OIDC discovery metadata).
2. Supply the authenticating user's context (external ID, email, correlation ID, calling service principal).
3. Receive custom claims — the provisioner's role list — to be embedded in the issued token.

## Installation

Apps do **not** reference this package directly. Install `Cirreum.Runtime.Identity.EntraExternalId` (or the umbrella `Cirreum.Runtime.Identity`), which brings this package in transitively and exposes the app-facing `builder.AddIdentity()` / `app.MapIdentity()` extensions.

## Wire contract (Microsoft-defined)

The request and response shapes are fixed by Microsoft's [custom claims provider](https://learn.microsoft.com/en-us/entra/identity-platform/custom-claims-provider-overview) contract. This package handles both sides — apps only supply an `IUserProvisioner` that returns `ProvisionResult.Allow(roles)` or `Deny()`.

### Request

Entra posts a JWT-authenticated payload describing the authentication context (calling app, user, correlation ID, etc.). The package validates and unpacks it internally.

### Response on Allow

```json
{
  "data": {
    "@odata.type": "microsoft.graph.onTokenIssuanceStartResponseData",
    "actions": [
      {
        "@odata.type": "microsoft.graph.tokenIssuanceStart.provideClaimsForToken",
        "claims": {
          "correlationId": "<echoed>",
          "customRoles": ["role1", "role2"]
        }
      }
    ]
  }
}
```

### Response on Deny or error

| Status | Trigger |
|---|---|
| 401 Unauthorized | Missing / invalid bearer token |
| 400 Bad Request  | Malformed JSON, missing `correlationId`, or missing user ID |
| 403 Forbidden    | `clientServicePrincipal.appId` not in allowlist, OR provisioner returned `Deny` |
| 500 Internal Server Error | Provisioner threw, or returned `Allow` with zero roles |

## Configuration

```json
{
  "Cirreum": {
    "Identity": {
      "Providers": {
        "EntraExternalId": {
          "Instances": {
            "primary": {
              "Enabled": true,
              "Route": "/auth/entra/provision",
              "ClientId": "<app-registration-client-id>",
              "Issuer": "https://<tenant-id>.ciamlogin.com/<tenant-id>/v2.0",
              "MetadataEndpoint": "https://<tenant-id>.ciamlogin.com/<tenant-id>/v2.0/.well-known/openid-configuration",
              "AllowedAppIds": "<allowed-client-app-guid>,<another-allowed-client-app-guid>"
            }
          }
        }
      }
    }
  }
}
```

### Per-instance settings

| Key | Default | Notes |
|---|---|---|
| `Enabled` | `false` | Instance is skipped during registration when `false`. |
| `Route` | — | Required. The HTTP route Entra will POST to (match the Target URL on the Custom Authentication Extension in the Azure Portal). |
| `ClientId` | — | Required. Application (client) ID of the custom claims provider app registration in the Entra External ID tenant. Validated as the `aud` claim on inbound tokens. |
| `Issuer` | — | Required. **Must use tenant-ID subdomain format**: `https://<tenant-id>.ciamlogin.com/<tenant-id>/v2.0`. Do NOT use the domain-name format. |
| `MetadataEndpoint` | — | Required. OIDC discovery URL for the tenant. |
| `EntraAppId` | `99045fe1-7639-4a75-9d4a-577b6ca3810f` | The Microsoft service app issuing callback tokens. Validated against `appid`/`azp`. Default is correct for all Entra External ID tenants — override only if Microsoft changes it. |
| `AllowedAppIds` | `""` | Comma/semicolon-separated list of permitted client-app GUIDs. Empty = allowlist disabled. |
| `ClockSkewMinutes` | `5` | Tolerance for JWT `exp` / `nbf`. |

### Instance key = Source name

The instance key (e.g. `primary`) is auto-populated into `ProvisionContext.Source` and is also the keyed-DI key under which the app registers its `IUserProvisioner` (via `AddProvisioner<T>(key)` in the Runtime Extensions layer). Do **not** set `Source` in configuration — it will fail loudly on mismatch.

### Multi-instance

Configure multiple entries under `Instances:` to serve multiple Entra External ID tenants from one API, each with its own client ID, issuer, and provisioner class. Per-instance `EntraTokenValidator` keeps tenant-specific signing keys cached independently.

## Azure Portal setup (summary)

1. Register an application (the "custom claims provider app registration") in your Entra External ID tenant. Note its Application (client) ID — this becomes `ClientId`.
2. In the app's manifest, set `acceptMappedClaims` to `true` in the `api` section. (There is no Azure Portal UI toggle for this — manifest edit only.)
3. Create a Custom Authentication Extension of type **onTokenIssuanceStart**, targeting the `Route` URL from your configuration.
4. Attach the extension to the relevant user flow(s) and select the claims you want to issue.
5. On the client app registration(s) consuming the flow, add the custom `customRoles` claim to the ID token claim configuration (and remap to `roles` on the client — see `Cirreum.Components.WebAssembly.Authentication`).

For the full setup walkthrough, see Microsoft's docs:
<https://learn.microsoft.com/en-us/entra/identity-platform/custom-claims-provider-configure-custom-extension>

## Security notes

- **Token validation** uses Microsoft's signing keys fetched via OIDC discovery, cached per-instance and auto-refreshed on unknown `kid`.
- **The token's `appid` / `azp` claim is verified** against `EntraAppId` to ensure the callback was triggered by Microsoft's Entra service (not by a forged JWT using any other valid Microsoft-signed token).
- **`AllowedAppIds` is strongly recommended** in production — it prevents other applications within the same Entra tenant from triggering provisioning in your application.
- **Body integrity is not additionally verified** beyond the bearer-token signature envelope. Microsoft's token validation is the authenticity boundary.

## What's not in this package

- **The app's `IUserProvisioner` implementation** — apps register theirs via `builder.AddIdentity().AddProvisioner<TProvisioner>("instance_key")` in the Runtime Extensions layer. This package only resolves the keyed service at callback time.
- **App-facing `AddEntraExternalIdIdentity()` / `MapEntraExternalIdIdentity()` extensions** — those live in `Cirreum.Runtime.Identity.EntraExternalId`.
- **OIDC webhook-style IdPs** (Descope, Auth0) — those use a different protocol and live in the sibling `Cirreum.Identity.Oidc` package.

## License

MIT — see [LICENSE](LICENSE).

---

**Cirreum Foundation Framework**  
*Layered simplicity for modern .NET*
