# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.1.0] - 2026-07-23

### Added

- Provisioned claims beyond roles. The `onTokenIssuanceStart` response now projects the
  application's full `IProvisionedIdentity.Claims` set â€” each `custom*` claim is emitted as a
  flat inline token claim (`customRoles`, `customName`, `customTenant`, â€¦) via a
  `[JsonExtensionData]` member bag. A single-valued claim serializes as a scalar string; roles
  (and any multi-valued claim) serialize as a string array, matching Entra's String / String[]
  supported data types.
- OpenTelemetry: the provisioner callback is now wrapped in the Core
  `Cirreum.Identity.Provisioning` telemetry scope, tagged `provider = entra`, emitting the
  provisioning span plus duration / outcome-count / minted-claim-count metrics.

### Changed

- Requires `Cirreum.IdentityProvider` `2.0.0`. The handler reads the reshaped
  `ProvisionResult.Allowed(Claims)` and projects `Claims.ToClaimMap()` onto the wire.
- An allowed result with no claims is now a valid outcome (the app admits the user but mints
  nothing beyond what the IdP itself issues) â€” it returns a 200 with only `correlationId`,
  instead of the former 500. The empty-roles guard is removed.

## [2.0.9] - 2026-07-20

### Updated

- Updated NuGet packages.

## [2.0.8] - 2026-07-19

### Updated

- Updated NuGet packages.

## [2.0.7] - 2026-07-04

### Updated

- Updated NuGet packages.

## [2.0.6] - 2026-07-04

### Updated

- Updated NuGet packages.

## [2.0.5] - 2026-05-08

### Updated

- Updated NuGet packages.

## [2.0.4] - 2026-05-07

### Updated

- Updated NuGet packages.

## [2.0.3] - 2026-05-01

### Updated

- Updated NuGet packages.
