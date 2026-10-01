# 02-update-target-frameworks: Update project TargetFramework to net10.0

Change TargetFramework elements (or TargetFrameworks) in the three projects to `net10.0`. Ensure SDK-style csproj remains valid and multi-targeting is handled if present.

**Done when**: All three projects list `net10.0` as their target, `dotnet restore` and `dotnet build` succeed.
