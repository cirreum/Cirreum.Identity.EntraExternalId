# Setup Guide — Cirreum.Identity.EntraExternalId

This guide walks through the Azure Portal configuration and application setup required to use the `onTokenIssuanceStart` custom authentication extension with Entra External ID.

## Prerequisites

- An **Azure subscription** with an **Entra External ID** (CIAM) tenant
- A **client application** registered in the External ID tenant (the app users sign into)
- An **ASP.NET host** to run the callback endpoint (your API, an Azure Function, or a dedicated service)

## 1. Register the Custom Claims Provider App

This is a separate app registration that represents the callback endpoint itself — not the app your users sign into.

1. In the Azure Portal, go to **Microsoft Entra ID** > **App registrations** > **New registration**
2. Set the name to something like `Claims Provider - {YourApp}`
3. Set **Supported account types** to **Accounts in this organizational directory only**
4. No redirect URI is needed
5. Click **Register**

After registration:

6. Go to **Expose an API**
7. Click **Add** next to **Application ID URI** — accept the default (`api://{client-id}`) or set a custom one
8. Click **Add a scope**:
   - **Scope name:** `CustomAuthenticationExtensions.Receive.Payload`
   - **Who can consent:** Admins only
   - **Admin consent display name:** Receive custom authentication extension payloads
   - Click **Add scope**

> **Save the Application (client) ID** — this is the `ClientId` in your configuration.

## 2. Create the Custom Authentication Extension

1. Go to **Microsoft Entra ID** > **Enterprise applications** > **Custom authentication extensions**
2. Click **Create a custom extension**
3. Select **onTokenIssuanceStart** and click **Next**
4. Configure the endpoint:
   - **Name:** e.g. `Claims Provider - {YourApp}`
   - **Target URL:** Your endpoint URL (must match the `Route` in config, default: `/auth/entra/claims`)
   - **Timeout:** 500ms (default) or up to 2000ms
5. Click **Next**
6. Under **API Authentication**, select the claims provider app registration from Step 1
7. Click **Create**

> After creation, Azure will prompt you to grant admin consent for the `CustomAuthenticationExtensions.Receive.Payload` permission. **Grant it** — without consent, the callback will not receive a valid token.

## 3. Configure the Custom Claims

1. Open the custom authentication extension you just created
2. Go to the **Claims** tab
3. Add the custom claim your provisioner returns:

| Claim name | Source |
|---|---|
| `customRoles` | Custom |

> **Note:** The `correlationId` is included in the response payload so Entra accepts the return, but it does not need to be configured as a claim here — only `customRoles` needs to be mapped.

4. **Save**

## 4. Assign the Extension to Your User Flow

1. Go to **Microsoft Entra ID** > **External Identities** > **User flows**
2. Open the sign-up/sign-in user flow for your client application
3. Go to **Custom authentication extensions**
4. Under **Before including claims in token (preview)**, select the extension from Step 2
5. Check the claim to include: `customRoles`
6. **Save**

## 5. Enable Mapped Claims on the Client App Registration

The client app registration (the app users sign into) must accept mapped claims for the custom authentication extension claims to appear in the issued token.

1. Go to **Microsoft Entra ID** > **App registrations** > open your **client app**
2. Go to **Manifest**
3. Find the `api` section and set `acceptMappedClaims` to `true`:

```json
{
  "api": {
    "acceptMappedClaims": true,
    "knownClientApplications": [],
    "requestedAccessTokenVersion": 2,
    "oauth2PermissionScopes": [],
    "preAuthorizedApplications": []
  }
}
```

4. **Save** the manifest

> **Important:** There is no UI toggle for this setting — it must be set directly in the manifest JSON. Without `acceptMappedClaims: true`, the custom claims from your authentication extension will be silently dropped from the issued token.
>
> You do **not** need to configure optional claims for this to work. The `customRoles` claim flows through via the custom authentication extension (Step 3) and user flow assignment (Step 4) alone.

## 6. Application Configuration

Add the following to your `appsettings.json`:

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

### Configuration reference

| Property | Required | Description |
|---|---|---|
| `Route` | No | Endpoint path. Defaults to `/auth/entra/claims`. Must match the Target URL in the Azure Portal extension. |
| `ClientId` | Yes | Application (client) ID of the claims provider app registration (Step 1). Validated as the `aud` claim on the incoming bearer token. |
| `Issuer` | Yes | Token issuer URL. **Must use the tenant ID format** — see warning below. |
| `EntraAppId` | Yes | The Microsoft service app ID that issues the callback token. Always `99045fe1-7639-4a75-9d4a-577b6ca3810f` for Entra External ID. |
| `MetadataEndpoint` | Yes | OIDC discovery URL for fetching token signing keys. |
| `AllowedAppIds` | Yes | Comma or semicolon-separated list of client app IDs allowed to trigger this endpoint. |

> **Warning — Issuer format:**
> Always use the **tenant ID subdomain** format for the issuer:
> ```
> https://<tenant-id>.ciamlogin.com/<tenant-id>/v2.0
> ```
> Do **not** use the domain name format (e.g., `https://myapp.ciamlogin.com/...`).
> Using the wrong format causes silent token validation failure with no clear error message.

### Where to find your tenant ID

1. Go to **Microsoft Entra ID** > **Overview**
2. Copy the **Tenant ID** (a GUID like `a1b2c3d4-e5f6-7890-abcd-ef1234567890`)

### Multiple allowed client apps

If multiple client applications share the same claims endpoint, list all their app IDs:

```json
"AllowedAppIds": "app-id-1,app-id-2,app-id-3"
```

## 7. Application Code

### Register services and map the endpoint

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddEntraExternalId<AppUserProvisioner>();

var app = builder.Build();

app.MapEntraExternalId();

app.Run();
```

### Implement the provisioner

Inherit from `UserProvisionerBase<TUser>` for the standard invitation-redemption flow:

```csharp
public sealed class AppUser : IProvisionedUser {
    public string ExternalUserId { get; init; } = "";
    public string Email { get; init; } = "";
    public IReadOnlyList<string> Roles { get; init; } = [];
}
```

> **Cirreum framework users:** If your application uses the full Cirreum runtime (`Cirreum.Runtime.Wasm`, etc.), also implement `IApplicationUser` on your user class so it participates in the Cirreum user-state pipeline (`IUserState.ApplicationUser`, `IApplicationUserLoader<T>`, and the Phase 2 `ApplicationUserProcessor<T>`):
>
> ```csharp
> public sealed class AppUser : IProvisionedUser, IApplicationUser { ... }
> ```

```csharp

public sealed class AppUserProvisioner(AppDbContext db)
    : UserProvisionerBase<AppUser> {

    protected override Task<AppUser?> FindUserAsync(
        string externalUserId, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.ExternalUserId == externalUserId, ct);

    protected override async Task<AppUser?> RedeemInvitationAsync(
        string email, string externalUserId, CancellationToken ct) {
        var invitation = await db.Invitations
            .FirstOrDefaultAsync(
                i => i.Email == email.ToLowerInvariant()
                  && i.ClaimedAt == null
                  && i.ExpiresAt > DateTimeOffset.UtcNow,
                ct);
        if (invitation is null) return null;

        invitation.ClaimedAt = DateTimeOffset.UtcNow;
        invitation.ClaimedByExternalUserId = externalUserId;
        var user = new AppUser {
            ExternalUserId = externalUserId,
            Email = email,
            Roles = [invitation.Role]
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user;
    }
}
```

Or implement `IUserProvisioner` directly for custom provisioning logic:

```csharp
public sealed class CustomProvisioner(AppDbContext db) : IUserProvisioner {

    public async Task<ProvisionResult> ProvisionAsync(
        ProvisionContext context, CancellationToken ct) {

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.ExternalUserId == context.ExternalUserId, ct);

        if (user is not null) {
            return ProvisionResult.Allow(user.Roles.ToArray());
        }

        // Auto-provision on first sign-in (no invitation required)
        var newUser = new AppUser {
            ExternalUserId = context.ExternalUserId,
            Email = context.Email,
            Roles = ["app:user"]
        };
        db.Users.Add(newUser);
        await db.SaveChangesAsync(ct);
        return ProvisionResult.Allow("app:user");
    }
}
```

## 8. Client-Side Claim Mapping

The provisioner embeds roles in the token as `customRoles` — this is a custom claim name, not the standard `roles` claim that ASP.NET Core authorization checks. On the Blazor WASM client, you need to map `customRoles` → `roles` before the `ClaimsPrincipal` is constructed.

### Using Cirreum Runtime

Implement `IClaimsExtender` (from `Cirreum.Runtime.Wasm`) to remap the claim during authentication. The extender runs before the `ClaimsPrincipal` is built, so `[Authorize(Roles = "...")]` works as expected.

```csharp
public sealed partial class CustomRoleClaimsExtender(
    ILogger<CustomRoleClaimsExtender> logger
) : IClaimsExtender {

    private const string CustomRolesType = "customRoles";
    private const string RolesType = "roles";

    public int Order => int.MinValue; // Run first

    public ValueTask ExtendClaimsAsync<TAccount>(
        ClaimsIdentity identity,
        TAccount account,
        IAccessTokenProvider accessTokenProvider)
        where TAccount : RemoteUserAccount {

        if (account.AdditionalProperties is null
            || !account.AdditionalProperties.TryGetValue(CustomRolesType, out var customRolesObj)) {
            return default;
        }

        // The claim value may arrive as a string or a JsonElement
        var raw = customRolesObj switch {
            string s => s,
            JsonElement json => json.ToString(),
            _ => customRolesObj?.ToString() ?? ""
        };

        // Parse comma/semicolon-delimited roles and add as standard role claims
        var roles = raw.Split([',', ';'],
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var role in roles) {
            if (!identity.HasClaim(RolesType, role)) {
                identity.AddClaim(new Claim(RolesType, role));
            }
        }

        // Remove the original customRoles claim
        var originalClaim = identity.FindFirst(CustomRolesType);
        if (originalClaim is not null) {
            identity.RemoveClaim(originalClaim);
        }

        return default;
    }
}
```

Register it during authentication setup:

```csharp
builder.AddAuthentication(...)
    .AddClaimsExtender<CustomRoleClaimsExtender>();
```

### Without Cirreum Runtime

If you're not using the Cirreum runtime, implement the same remapping in your own `AccountClaimsPrincipalFactory<RemoteUserAccount>` override by reading `customRoles` from the account's additional properties and adding them as `roles` claims on the identity.

> **Why `customRoles` instead of `roles`?**
> Entra External ID custom authentication extensions cannot override built-in claim names like `roles`. The extension must use a custom claim name, which is why the provisioner returns `customRoles` and the client maps it to the standard `roles` claim.

## 9. Local Development

### Expose your local endpoint

Entra External ID calls your endpoint over HTTPS from Azure. For local development, use a tunnel:

**Using Visual Studio Dev Tunnels:**
1. In Visual Studio, go to **Tools** > **Options** > **Dev Tunnels** > **Enable Dev Tunnels**
2. Create a persistent tunnel via **View** > **Other Windows** > **Dev Tunnels**
3. Set the tunnel URL as the **Target URL** in the custom authentication extension

**Using ngrok:**
```bash
ngrok http https://localhost:5001
```
Use the HTTPS forwarding URL as the Target URL in the Azure Portal.

### Update the Target URL

After creating your tunnel, update the custom authentication extension's **Target URL** in the Azure Portal to point to your tunnel URL + the route:

```
https://{tunnel-id}.devtunnels.ms/auth/entra/claims
```

> **Tip:** Create a separate custom authentication extension for development so you don't break production configuration.

### Testing the endpoint directly

You can test the endpoint by sending a POST request that mimics the Entra callback. However, the bearer token must be a valid JWT signed by Entra — you cannot use a static test token.

For integration testing, use the `IServiceCollection` overload to wire up the services with a test configuration and mock provisioner:

```csharp
services.AddEntraExternalId<TestProvisioner>(configuration);
```

## Troubleshooting

### Token validation fails silently

- **Check the Issuer format.** The most common cause is using the domain name (`myapp.ciamlogin.com`) instead of the tenant ID format in the `Issuer` configuration. See the warning in the configuration reference.
- **Check admin consent.** The `CustomAuthenticationExtensions.Receive.Payload` permission must have admin consent granted.
- **Check the MetadataEndpoint.** Verify it returns a valid OIDC configuration document by opening it in a browser.

### 403 Forbidden — App not allowed

The `AllowedAppIds` list does not include the client application's app ID. Check:
1. The **Application (client) ID** of the app the user is signing into (not the claims provider app)
2. That it's listed in `AllowedAppIds` in your configuration

### 401 Unauthorized — Missing or invalid token

- Entra sends a bearer token to your callback. If it's missing, the request is not coming from Entra.
- If the token is present but validation fails, check:
  - `ClientId` matches the claims provider app registration
  - `EntraAppId` is `99045fe1-7639-4a75-9d4a-577b6ca3810f`
  - `MetadataEndpoint` is reachable from your host

### Custom claims not appearing in the ID token

1. Verify `"acceptMappedClaims": true` is set in the client app's manifest `api` section — this is the most common cause and must be set manually in the manifest JSON
2. Verify the `customRoles` claim is added in the custom authentication extension's **Claims** tab
3. Verify the claim is selected in the user flow's **Custom authentication extensions** section
4. Clear the browser cache / use an incognito window — cached tokens won't have the new claims

### Provisioner returns Denied but user should be allowed

- Check your database — does the user record exist with the correct `ExternalUserId`?
- If using invitations, verify the invitation is not expired and has not already been claimed
- Check the `Email` field — if the identity provider doesn't supply an email, invitation redemption is skipped and the user is denied (when using `UserProvisionerBase`)
