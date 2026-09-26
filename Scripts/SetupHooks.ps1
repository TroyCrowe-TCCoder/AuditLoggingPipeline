<#
.SYNOPSIS
Enables the repository's version-controlled Git hooks.

.DESCRIPTION
Sets the local repository's core.hooksPath to .githooks so that the
pre-commit validation hook checked into source control becomes active.
This must be run once after cloning, per GlobalDeveloperOnboardingStandards.md
Section 4, Step 2 (Enable Local Standards Validation Enforcement). The hook
invokes the ValidationEngine dotnet tool restored via .config/dotnet-tools.json
and requires no sibling-repository checkout, per GlobalDeveloperOnboardingStandards.md Section 4.1.

.EXAMPLE
pwsh ./Scripts/SetupHooks.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repoRoot = (git rev-parse --show-toplevel 2>$null)
if (-not $repoRoot) {
	Write-Error 'This script must be run from within a git repository.'
	exit 1
}

Push-Location $repoRoot
try {
	git config core.hooksPath .githooks

	$hookPath = Join-Path $repoRoot '.githooks/pre-commit'
	if (Test-Path $hookPath) {
		git update-index --chmod=+x -- .githooks/pre-commit 2>$null | Out-Null
	}

	dotnet tool restore

	Write-Host "Git hooks enabled. core.hooksPath set to .githooks in $repoRoot" -ForegroundColor Green
}
finally {
	Pop-Location
}
