# .NET 10 Upgrade Plan

## Overview

**Target**: Upgrade solution DistributedAttackCircuitBreaker to .NET 10 (net10.0).
**Scope**: 3 projects (src/DistributedAttackCircuitBreaker.WebApi, src/DistributedAttackCircuitBreaker, tests/DistributedAttackCircuitBreaker.Tests). Changes include package updates, target framework updates, and resolving incompatible packages.

## Tasks

### 01-update-packages: Update NuGet packages to .NET 10-compatible versions

Update top-level NuGet package references across projects to the versions recommended by the assessment and `dotnet list package --outdated` output. Prefer the latest stable versions that list .NET 10 support. For packages that are development/IDE tooling (e.g., Microsoft.VisualStudio.Azure.Containers.Tools.Targets), prefer removing or marking PrivateAssets="all" if appropriate.

**Done when**: All targeted PackageReference entries in project files are updated to the chosen versions, `dotnet restore` succeeds, and the solution builds without package-related errors introduced by package resolution.

---

### 02-update-target-frameworks: Update project TargetFramework to net10.0

Change TargetFramework elements (or TargetFrameworks) in the three projects to `net10.0`. Ensure SDK-style csproj remains valid and multi-targeting is handled if present.

**Done when**: All three projects list `net10.0` as their target, `dotnet restore` and `dotnet build` succeed.

---

### 03-fix-incompatibilities: Resolve incompatible or deprecated packages and API changes

Address incompatible packages (e.g., Microsoft.VisualStudio.Azure.Containers.Tools.Targets), replace deprecated test packages (xunit), and fix any API changes caused by package or framework upgrades.

**Done when**: Incompatible packages are removed or replaced with compatible alternatives, code compiles, and tests updated as needed.

---

### 04-validate-build-tests: Final validation

Run full solution build and all tests. Fix warnings introduced by the upgrade; warnings are treated as errors for modified projects.

**Done when**: Solution builds without errors, touched projects have no compiler warnings, and all tests pass.
