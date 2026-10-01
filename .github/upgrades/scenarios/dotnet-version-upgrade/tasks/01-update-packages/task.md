# 01-update-packages: Update NuGet packages to .NET 10-compatible versions

Update top-level NuGet package references across projects to the versions recommended by the assessment and `dotnet list package --outdated` output. Prefer the latest stable versions that list .NET 10 support. For packages that are development/IDE tooling (e.g., Microsoft.VisualStudio.Azure.Containers.Tools.Targets), prefer removing or marking PrivateAssets="all" if appropriate.

**Done when**: All targeted PackageReference entries in project files are updated to the chosen versions, `dotnet restore` succeeds, and the solution builds without package-related errors introduced by package resolution.
