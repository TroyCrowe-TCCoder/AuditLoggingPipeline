<#
.SYNOPSIS
	Manually runs the installed ValidationEngine tool against this repository.

.DESCRIPTION
	Thin wrapper only — do not add validation logic here. Extend
	ValidationEngine/*.cs (in the ValidationEngine repository) instead, so all governed
	repositories share identical enforcement behavior.

	Requires the ValidationEngine local dotnet tool to be restored:
		dotnet tool restore
	No sibling repository checkout is required — see
	GlobalDeveloperOnboardingStandards.md Section 4.1.

.EXAMPLE
	pwsh ./Validate.ps1
#>

$repoRoot = git rev-parse --show-toplevel

Write-Host "Running validation (validation-engine)..."

dotnet tool run validation-engine -- -RepositoryRoot $repoRoot -Mode Manual
exit $LASTEXITCODE
