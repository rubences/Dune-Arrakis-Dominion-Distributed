Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

param(
    [string]$WorkspaceRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),
    [switch]$RunInstaller,
    [switch]$SkipOpenCodeConfig,
    [switch]$SkipGentleAiConfig
)

$repoRoot = Join-Path $WorkspaceRoot "automation\external"
$repos = @(
    @{ Name = "engram"; Url = "https://github.com/Gentleman-Programming/engram" },
    @{ Name = "gentle-ai"; Url = "https://github.com/Gentleman-Programming/gentle-ai" },
    @{ Name = "gentleman-guardian-angel"; Url = "https://github.com/Gentleman-Programming/gentleman-guardian-angel" },
    @{ Name = "Gentleman-Skills"; Url = "https://github.com/Gentleman-Programming/Gentleman-Skills" }
)

if (-not (Test-Path -LiteralPath $repoRoot)) {
    New-Item -ItemType Directory -Path $repoRoot -Force | Out-Null
}

foreach ($repo in $repos) {
    $target = Join-Path $repoRoot $repo.Name
    if (Test-Path -LiteralPath (Join-Path $target ".git")) {
        Write-Host "Updating $($repo.Name)..."
        git -C $target pull --ff-only
    }
    else {
        Write-Host "Cloning $($repo.Name)..."
        git clone $repo.Url $target
    }

    $origin = git -C $target remote get-url origin
    if ($origin.Trim() -ne $repo.Url) {
        throw "Repository origin mismatch for $($repo.Name). Expected $($repo.Url), got $origin"
    }
}

Write-Host "All Gentleman repositories are synced under: $repoRoot"

if ($RunInstaller) {
    $installer = Join-Path $WorkspaceRoot "automation\windows\install-gentleman-stack.ps1"
    if (-not (Test-Path -LiteralPath $installer)) {
        throw "Installer script not found: $installer"
    }

    $args = @("-ExecutionPolicy", "Bypass", "-File", $installer)
    if ($SkipOpenCodeConfig) { $args += "-SkipOpenCodeConfig" }
    if ($SkipGentleAiConfig) { $args += "-SkipGentleAiConfig" }

    Write-Host "Running Windows installer..."
    powershell @args
}
