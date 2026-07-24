# Cirreum.Identity.EntraExternalId 2.1.0 — provisioned claims beyond roles

## Why this release exists

The Entra adapter could inject exactly one thing into the issued token: **roles**. That was
enough when Entra federated a full profile for free, but under a pure IdP-as-a-service backing —
where the application is the authority for the user's attributes — the app had no channel to
project its own claims (display name, tenant, entitlements) into the token. This release widens
that channel to the application's full claim set, tracking the `Cirreum.IdentityProvider 2.0.0`
contract.

## What's new

The `onTokenIssuanceStart` response now projects the whole `IProvisionedIdentity.Claims` set.
Each provisioned claim lives in a `custom*` namespace and is emitted as a **flat inline token
claim** so it stays natively consumable — no bundle to unpack client-side:

```jsonc
// claims object in the response envelope
{
  "correlationId": "…",
  "customRoles": ["admin", "subscriber"],   // roles are always an array
  "customName": "Jane Smith",               // single-valued claim → scalar string
  "customTenant": "acme"
}
```

Entra claim values are String or String[] only (`JSON: False`), so the projection emits a
single-valued claim as a scalar and a multi-valued claim (and roles, always) as an array. The
inline members ride on a `[JsonExtensionData]` bag, so there are no fixed per-claim C# properties
to maintain — an app adds a claim by adding an `IdentityClaim`, nothing in this package changes.

**Allowed-with-no-claims is now valid.** A provisioner that admits a user but mints nothing
returns a 200 carrying only `correlationId`. The former "allowed with no roles → 500" guard is
gone — a roleless identity is a deliberate application choice (ABAC / ownership models), expressed
by the shape of the app's own provisioned-identity type.

**Observability.** The provisioner call is wrapped in the Core `Cirreum.Identity.Provisioning`
telemetry scope (tagged `provider = entra`): one span per callback plus duration, outcome-count,
and minted-claim-count metrics. No user identifier or email is tagged.

## Per-claim IdP declaration

Each `custom*` member is a distinct token claim the operator declares once, Entra-side: one
Attributes-tab row per claim (`customRoles`, `customName`, …), with the app token configuration
set to utilize it. The `custom*` name is kept end-to-end; the client canonicalizes
`customRoles → roles` during `ClaimsPrincipal` construction (or the operator canonicalizes at the
IdP and skips the client extender).

## Compatibility

- Requires `Cirreum.IdentityProvider 2.0.0`.
- The package's public .NET surface (registrar, settings) is unchanged — this is a minor bump.
- The **wire contract** changed: `customRoles` is no longer the only claim, and a claim can now
  be a scalar. An operator adopting this must declare the additional `custom*` claims their
  provisioner mints. A provisioner that still mints only roles produces the same `customRoles`
  array as before.

## See also

- `Cirreum.IdentityProvider 2.0.0` — the reshaped provisioning contract this adapter projects.
- `Cirreum.Identity.Oidc 1.1.0` — the sibling adapter, same claim set over a `customClaims` object.
