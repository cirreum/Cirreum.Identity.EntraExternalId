# Cirreum.Identity.EntraExternalId

[![NuGet Version](https://img.shields.io/nuget/v/Cirreum.Identity.EntraExternalId.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Identity.EntraExternalId/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Cirreum.Identity.EntraExternalId.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Identity.EntraExternalId/)
[![GitHub Release](https://img.shields.io/github/v/release/cirreum/Cirreum.Identity.EntraExternalId?style=flat-square&labelColor=1F1F1F&color=FF3B2E)](https://github.com/cirreum/Cirreum.Identity.EntraExternalId/releases)
[![License](https://img.shields.io/github/license/cirreum/Cirreum.Identity.EntraExternalId?style=flat-square&labelColor=1F1F1F&color=F2F2F2)](https://github.com/cirreum/Cirreum.Identity.EntraExternalId/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-003D8F?style=flat-square&labelColor=1F1F1F)](https://dotnet.microsoft.com/)

**User provisioning for Microsoft Entra External ID token issuance callbacks.**

## Overview

**Cirreum.Identity.EntraExternalId** handles the `onTokenIssuanceStart` custom authentication extension in Microsoft Entra External ID. When a user signs in, Entra calls your endpoint before issuing the ID token — this library validates the callback, provisions the user, and returns custom claims (roles) for Entra to embed in the token.

The library is framework-independent and can run in any ASP.NET host: your main API, an Azure Function, or a dedicated service.

### How it works

1. **Validates** the callback bearer token (OIDC signature + Entra app ID)
2. **Provisions** the user via your `IUserProvisioner` implementation
3. **Returns** custom claims that Entra embeds in the issued ID token

### Installation

```
dotnet add package Cirreum.Identity.EntraExternalId
```

### Quick start

```csharp
// Program.cs
builder.AddEntraExternalId<AppUserProvisioner>();

var app = builder.Build();
app.MapEntraExternalId();
```

### Implement the provisioner

```csharp
public sealed class AppUserProvisioner(AppDbContext db)
    : UserProvisionerBase<AppUser> {

    protected override Task<AppUser?> FindUserAsync(
        string externalUserId, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.ExternalUserId == externalUserId, ct);

    protected override async Task<AppUser?> RedeemInvitationAsync(
        string email, string externalUserId, CancellationToken ct) {
        var invitation = await db.Invitations
            .FirstOrDefaultAsync(i => i.Email == email && !i.IsRedeemed, ct);
        if (invitation is null || invitation.IsExpired) return null;

        var user = new AppUser {
            ExternalUserId = externalUserId,
            Email = email,
            Roles = [invitation.Role]
        };
        db.Users.Add(user);
        invitation.IsRedeemed = true;
        await db.SaveChangesAsync(ct);
        return user;
    }
}
```

> **Cirreum framework users:** If your app uses the full Cirreum runtime, also implement `IApplicationUser` on your user class so it integrates with `IUserState` and the authentication post-processor pipeline. See [SETUP.md](SETUP.md) for details.

### Configuration

```json
{
  "Cirreum": {
    "Identity": {
      "EntraExternalId": {
        "Route": "/auth/entra/claims",
        "ClientId": "<claims-provider-app-client-id>",
        "Issuer": "https://<tenant-id>.ciamlogin.com/<tenant-id>/v2.0",
        "EntraAppId": "99045fe1-7639-4a75-9d4a-577b6ca3810f",
        "MetadataEndpoint": "https://<tenant-id>.ciamlogin.com/<tenant-id>/v2.0/.well-known/openid-configuration",
        "AllowedAppIds": "<client-app-id>"
      }
    }
  }
}
```

### Client-side claim mapping

Roles arrive in the ID token as `customRoles` (Entra doesn't allow overriding the built-in `roles` claim name). On the Blazor WASM client, map them to `roles` before the `ClaimsPrincipal` is built using `IClaimsExtender` from `Cirreum.Runtime.Wasm`. See [SETUP.md](SETUP.md#8-client-side-claim-mapping) for the full implementation.

## Documentation

See [SETUP.md](SETUP.md) for Azure Portal configuration, client-side claim mapping, and local development instructions.

## Contribution Guidelines

1. **Be conservative with new abstractions**
   The API surface must remain stable and meaningful.

2. **Limit dependency expansion**
   Only add foundational, version-stable dependencies.

3. **Favor additive, non-breaking changes**
   Breaking changes ripple through the entire ecosystem.

4. **Include thorough unit tests**
   All primitives and patterns should be independently testable.

5. **Document architectural decisions**
   Context and reasoning should be clear for future maintainers.

6. **Follow .NET conventions**
   Use established patterns from Microsoft.Extensions.* libraries.

## Versioning

Cirreum.Identity.EntraExternalId follows [Semantic Versioning](https://semver.org/):

- **Major** - Breaking API changes
- **Minor** - New features, backward compatible
- **Patch** - Bug fixes, backward compatible

Given its foundational role, major version bumps are rare and carefully considered.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

**Cirreum Foundation Framework**  
*Layered simplicity for modern .NET*
