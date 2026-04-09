Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

param(
    [string]$WorkspaceRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),
    [string]$SourceRoot = "$env:USERPROFILE\src",
    [switch]$SkipOpenCodeConfig,
    [switch]$SkipGentleAiConfig
)

$script:State = [ordered]@{
    Installed = New-Object System.Collections.Generic.List[string]
    AlreadyInstalled = New-Object System.Collections.Generic.List[string]
    Methods = [ordered]@{}
    Versions = [ordered]@{}
    Paths = [ordered]@{}
    PathChanges = New-Object System.Collections.Generic.List[string]
    ConfigFiles = New-Object System.Collections.Generic.List[string]
    Pending = New-Object System.Collections.Generic.List[string]
    Risks = New-Object System.Collections.Generic.List[string]
    Errors = New-Object System.Collections.Generic.List[string]
}

function Write-Phase {
    param([string]$Title)
    Write-Host ""
    Write-Host "=== $Title ===" -ForegroundColor Cyan
}

function Add-ItemUnique {
    param(
        [System.Collections.Generic.List[string]]$List,
        [string]$Value
    )
    if (-not [string]::IsNullOrWhiteSpace($Value) -and -not $List.Contains($Value)) {
        [void]$List.Add($Value)
    }
}

function Get-CommandPath {
    param([string]$Name)
    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $cmd) { return $null }
    return $cmd.Source
}

function Test-CommandAvailable {
    param([string]$Name)
    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Get-CommandOutput {
    param(
        [string]$Name,
        [string[]]$Args = @("--version")
    )
    try {
        $path = Get-CommandPath -Name $Name
        if (-not $path) { return "NOT INSTALLED" }
        $output = & $path @Args 2>&1
        if ($LASTEXITCODE -ne 0) {
            return "ERROR ($LASTEXITCODE): $($output | Out-String)"
        }
        return (($output | Select-Object -First 1) -as [string]).Trim()
    }
    catch {
        return "ERROR: $($_.Exception.Message)"
    }
}

function Ensure-Directory {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Add-UserPathEntry {
    param([string]$Entry)

    if ([string]::IsNullOrWhiteSpace($Entry)) { return }
    if (-not (Test-Path -LiteralPath $Entry)) { return }

    $sep = ';'
    $currentUserPath = [Environment]::GetEnvironmentVariable("Path", "User")
    if ([string]::IsNullOrWhiteSpace($currentUserPath)) {
        [Environment]::SetEnvironmentVariable("Path", $Entry, "User")
        $env:Path = "$Entry$sep$env:Path"
        Add-ItemUnique -List $script:State.PathChanges -Value "User PATH + $Entry"
        return
    }

    $entries = $currentUserPath.Split($sep, [System.StringSplitOptions]::RemoveEmptyEntries)
    $exists = $false
    foreach ($existing in $entries) {
        if ($existing.TrimEnd('\\').ToLowerInvariant() -eq $Entry.TrimEnd('\\').ToLowerInvariant()) {
            $exists = $true
            break
        }
    }

    if (-not $exists) {
        $newPath = "$currentUserPath$sep$Entry"
        [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
        $env:Path = "$Entry$sep$env:Path"
        Add-ItemUnique -List $script:State.PathChanges -Value "User PATH + $Entry"
    }
}

function Find-GitBash {
    $candidates = @(
        "$env:ProgramFiles\Git\bin\bash.exe",
        "$env:ProgramFiles\Git\usr\bin\bash.exe",
        "$env:ProgramFiles(x86)\Git\bin\bash.exe",
        "$env:ProgramFiles(x86)\Git\usr\bin\bash.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    $bashFromCmd = Get-CommandPath -Name "bash"
    if ($bashFromCmd) { return $bashFromCmd }
    return $null
}

function Invoke-External {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$Arguments = @(),
        [switch]$AllowFailure
    )

    $output = & $FilePath @Arguments 2>&1
    $exit = $LASTEXITCODE

    if ($exit -ne 0 -and -not $AllowFailure) {
        throw "Command failed: $FilePath $($Arguments -join ' ')`n$($output | Out-String)"
    }

    return [pscustomobject]@{
        ExitCode = $exit
        Output = $output
    }
}

function Install-Git {
    if (Test-CommandAvailable "git") {
        Add-ItemUnique -List $script:State.AlreadyInstalled -Value "Git"
        $script:State.Methods["Git"] = "Reused existing installation"
        return
    }

    Write-Host "Installing Git from official git-for-windows releases..."
    $api = "https://api.github.com/repos/git-for-windows/git/releases/latest"
    $release = Invoke-RestMethod -Uri $api -UseBasicParsing

    $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
    $asset = $null

    if ($arch -like "*arm64*") {
        $asset = $release.assets | Where-Object { $_.name -match "Git-.*-arm64\.exe$" } | Select-Object -First 1
    }
    else {
        $asset = $release.assets | Where-Object { $_.name -match "Git-.*-64-bit\.exe$" } | Select-Object -First 1
    }

    if (-not $asset) {
        throw "Could not resolve official Git installer asset from git-for-windows releases."
    }

    $tmpExe = Join-Path $env:TEMP $asset.name
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $tmpExe -UseBasicParsing

    Invoke-External -FilePath $tmpExe -Arguments @("/VERYSILENT", "/NORESTART") | Out-Null

    Remove-Item -LiteralPath $tmpExe -Force -ErrorAction SilentlyContinue

    if (-not (Test-CommandAvailable "git")) {
        Add-UserPathEntry -Entry "$env:ProgramFiles\Git\cmd"
    }

    if (-not (Test-CommandAvailable "git")) {
        throw "Git installation completed but git is still not available in PATH."
    }

    Add-ItemUnique -List $script:State.Installed -Value "Git"
    $script:State.Methods["Git"] = "Official git-for-windows release installer"
}

function Install-Go {
    $goOk = $false
    if (Test-CommandAvailable "go") {
        $versionLine = Get-CommandOutput -Name "go" -Args @("version")
        if ($versionLine -match "go version go(\d+)\.(\d+)") {
            $major = [int]$Matches[1]
            $minor = [int]$Matches[2]
            if ($major -gt 1 -or ($major -eq 1 -and $minor -ge 22)) {
                $goOk = $true
            }
        }

        if ($goOk) {
            Add-ItemUnique -List $script:State.AlreadyInstalled -Value "Go"
            $script:State.Methods["Go"] = "Reused existing installation"
            return
        }
    }

    Write-Host "Installing Go from go.dev official downloads..."
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
    $meta = Invoke-RestMethod -Uri "https://go.dev/dl/?mode=json" -UseBasicParsing
    $stable = $meta | Select-Object -First 1

    $fileName = if ($arch -like "*arm64*") {
        ($stable.files | Where-Object { $_.os -eq "windows" -and $_.arch -eq "arm64" -and $_.kind -eq "installer" } | Select-Object -First 1).filename
    }
    else {
        ($stable.files | Where-Object { $_.os -eq "windows" -and $_.arch -eq "amd64" -and $_.kind -eq "installer" } | Select-Object -First 1).filename
    }

    if (-not $fileName) {
        throw "Could not resolve official Go MSI installer for this architecture."
    }

    $url = "https://go.dev/dl/$fileName"
    $tmpMsi = Join-Path $env:TEMP $fileName
    Invoke-WebRequest -Uri $url -OutFile $tmpMsi -UseBasicParsing

    Invoke-External -FilePath "msiexec.exe" -Arguments @("/i", $tmpMsi, "/qn", "/norestart") | Out-Null
    Remove-Item -LiteralPath $tmpMsi -Force -ErrorAction SilentlyContinue

    Add-UserPathEntry -Entry "C:\Go\bin"

    if (-not (Test-CommandAvailable "go")) {
        throw "Go installation completed but go is still not available in PATH."
    }

    Add-ItemUnique -List $script:State.Installed -Value "Go"
    $script:State.Methods["Go"] = "Official go.dev MSI installer"
}

function Install-Scoop {
    if (Test-CommandAvailable "scoop") {
        Add-ItemUnique -List $script:State.AlreadyInstalled -Value "Scoop"
        $script:State.Methods["Scoop"] = "Reused existing installation"
        return
    }

    Write-Host "Installing Scoop from official installer script..."
    try {
        Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser -Force
    }
    catch {
        Add-ItemUnique -List $script:State.Risks -Value "Could not set execution policy to RemoteSigned for CurrentUser."
    }

    Invoke-Expression (New-Object System.Net.WebClient).DownloadString('https://get.scoop.sh')

    if (-not (Test-CommandAvailable "scoop")) {
        Add-UserPathEntry -Entry "$env:USERPROFILE\scoop\shims"
    }

    if (-not (Test-CommandAvailable "scoop")) {
        throw "Scoop installation completed but scoop is still not available in PATH."
    }

    Add-ItemUnique -List $script:State.Installed -Value "Scoop"
    $script:State.Methods["Scoop"] = "Official scoop installer script"
}

function Install-OpenCode {
    if (Test-CommandAvailable "opencode") {
        Add-ItemUnique -List $script:State.AlreadyInstalled -Value "OpenCode"
        $script:State.Methods["OpenCode"] = "Reused existing installation"
        return
    }

    $installed = $false

    if (Test-CommandAvailable "scoop") {
        Write-Host "Installing OpenCode via Scoop..."
        try {
            Invoke-External -FilePath (Get-CommandPath "scoop") -Arguments @("install", "opencode") | Out-Null
            if (Test-CommandAvailable "opencode") {
                $installed = $true
                $script:State.Methods["OpenCode"] = "Scoop install opencode"
            }
        }
        catch {
            Add-ItemUnique -List $script:State.Risks -Value "OpenCode via Scoop failed: $($_.Exception.Message)"
        }
    }

    if (-not $installed -and (Test-CommandAvailable "npm")) {
        Write-Host "Installing OpenCode via npm fallback..."
        try {
            Invoke-External -FilePath (Get-CommandPath "npm") -Arguments @("install", "-g", "opencode-ai") | Out-Null
            if (Test-CommandAvailable "opencode") {
                $installed = $true
                $script:State.Methods["OpenCode"] = "npm install -g opencode-ai"
            }
        }
        catch {
            Add-ItemUnique -List $script:State.Risks -Value "OpenCode via npm failed: $($_.Exception.Message)"
        }
    }

    if (-not $installed) {
        Write-Host "Installing OpenCode from official GitHub release fallback..."
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/opencode-ai/opencode/releases/latest" -UseBasicParsing
        $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
        $asset = if ($arch -like "*arm64*") {
            $release.assets | Where-Object { $_.name -match "windows.*arm64.*(zip|exe)$" } | Select-Object -First 1
        }
        else {
            $release.assets | Where-Object { $_.name -match "windows.*(amd64|x64).*(zip|exe)$" } | Select-Object -First 1
        }

        if (-not $asset) {
            throw "Could not resolve official OpenCode Windows release asset."
        }

        $tmpAsset = Join-Path $env:TEMP $asset.name
        Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $tmpAsset -UseBasicParsing

        $targetDir = "$env:LOCALAPPDATA\Programs\OpenCode"
        Ensure-Directory -Path $targetDir

        if ($asset.name.ToLowerInvariant().EndsWith(".zip")) {
            Expand-Archive -Path $tmpAsset -DestinationPath $targetDir -Force
        }
        else {
            Copy-Item -LiteralPath $tmpAsset -Destination (Join-Path $targetDir "opencode.exe") -Force
        }

        Remove-Item -LiteralPath $tmpAsset -Force -ErrorAction SilentlyContinue
        Add-UserPathEntry -Entry $targetDir

        if (Test-CommandAvailable "opencode") {
            $installed = $true
            $script:State.Methods["OpenCode"] = "Official opencode-ai release binary"
        }
    }

    if (-not (Test-CommandAvailable "opencode")) {
        throw "OpenCode could not be installed with official methods."
    }

    Add-ItemUnique -List $script:State.Installed -Value "OpenCode"
}

function Install-Engram {
    if (-not (Test-CommandAvailable "go")) {
        throw "Go is required to install Engram from source."
    }

    Ensure-Directory -Path $SourceRoot
    $engramDir = Join-Path $SourceRoot "engram"

    if (-not (Test-Path -LiteralPath $engramDir)) {
        Invoke-External -FilePath (Get-CommandPath "git") -Arguments @("clone", "https://github.com/Gentleman-Programming/engram.git", $engramDir) | Out-Null
        Add-ItemUnique -List $script:State.Installed -Value "Engram source"
    }
    else {
        Invoke-External -FilePath (Get-CommandPath "git") -Arguments @("-C", $engramDir, "pull", "--ff-only") -AllowFailure | Out-Null
        Add-ItemUnique -List $script:State.AlreadyInstalled -Value "Engram source"
    }

    Push-Location $engramDir
    try {
        Invoke-External -FilePath (Get-CommandPath "go") -Arguments @("install", "./cmd/engram") | Out-Null

        $gopath = (& (Get-CommandPath "go") env GOPATH).Trim()
        if ([string]::IsNullOrWhiteSpace($gopath)) {
            $gopath = "$env:USERPROFILE\go"
        }

        $goBin = Join-Path $gopath "bin"
        Ensure-Directory -Path $goBin
        Add-UserPathEntry -Entry $goBin

        $ver = (& (Get-CommandPath "git") describe --tags --always 2>$null)
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($ver)) {
            $outExe = Join-Path $engramDir "engram.exe"
            Invoke-External -FilePath (Get-CommandPath "go") -Arguments @("build", "-ldflags=-X main.version=local-$ver", "-o", $outExe, "./cmd/engram") | Out-Null
            Copy-Item -LiteralPath $outExe -Destination (Join-Path $goBin "engram.exe") -Force
        }
    }
    finally {
        Pop-Location
    }

    if (-not (Test-CommandAvailable "engram")) {
        throw "Engram install finished but engram command is not available in PATH."
    }

    Add-ItemUnique -List $script:State.Installed -Value "Engram"
    $script:State.Methods["Engram"] = "Go install from official source repo"
}

function Setup-EngramOpenCode {
    if (-not (Test-CommandAvailable "engram")) {
        throw "engram command not available for OpenCode setup."
    }

    try {
        Invoke-External -FilePath (Get-CommandPath "engram") -Arguments @("setup", "opencode") -AllowFailure | Out-Null
    }
    catch {
        Add-ItemUnique -List $script:State.Risks -Value "engram setup opencode failed: $($_.Exception.Message)"
    }

    $possibleFiles = @(
        "$env:APPDATA\OpenCode\config.json",
        "$env:APPDATA\OpenCode\mcp.json",
        "$env:USERPROFILE\.config\opencode\config.json",
        "$env:USERPROFILE\.config\opencode\mcp.json"
    )

    foreach ($file in $possibleFiles) {
        if (Test-Path -LiteralPath $file) {
            Add-ItemUnique -List $script:State.ConfigFiles -Value $file
        }
    }

    if ($script:State.ConfigFiles.Count -eq 0) {
        Add-ItemUnique -List $script:State.Pending -Value "Could not locate OpenCode config file generated by engram setup opencode."
    }
}

function Install-GentleAi {
    if (Test-CommandAvailable "gentle-ai") {
        Add-ItemUnique -List $script:State.AlreadyInstalled -Value "gentle-ai"
        $script:State.Methods["gentle-ai"] = "Reused existing installation"
        return
    }

    $installed = $false

    if (Test-CommandAvailable "scoop") {
        Write-Host "Installing gentle-ai via Scoop gentleman bucket..."
        try {
            Invoke-External -FilePath (Get-CommandPath "scoop") -Arguments @("bucket", "add", "gentleman", "https://github.com/Gentleman-Programming/scoop-bucket") -AllowFailure | Out-Null
            Invoke-External -FilePath (Get-CommandPath "scoop") -Arguments @("install", "gentle-ai") | Out-Null
            if (Test-CommandAvailable "gentle-ai") {
                $installed = $true
                $script:State.Methods["gentle-ai"] = "Scoop bucket gentleman"
            }
        }
        catch {
            Add-ItemUnique -List $script:State.Risks -Value "gentle-ai via Scoop failed: $($_.Exception.Message)"
        }
    }

    if (-not $installed) {
        Write-Host "Installing gentle-ai via official PowerShell script fallback..."
        try {
            irm https://raw.githubusercontent.com/Gentleman-Programming/gentle-ai/main/scripts/install.ps1 | iex
            if (Test-CommandAvailable "gentle-ai") {
                $installed = $true
                $script:State.Methods["gentle-ai"] = "Official install.ps1 script"
            }
        }
        catch {
            Add-ItemUnique -List $script:State.Risks -Value "gentle-ai official install script failed: $($_.Exception.Message)"
        }
    }

    if (-not $installed -and (Test-CommandAvailable "go")) {
        Write-Host "Installing gentle-ai via go install fallback..."
        Invoke-External -FilePath (Get-CommandPath "go") -Arguments @("install", "github.com/gentleman-programming/gentle-ai/cmd/gentle-ai@latest") | Out-Null
        if (Test-CommandAvailable "gentle-ai") {
            $installed = $true
            $script:State.Methods["gentle-ai"] = "go install fallback"
        }
    }

    if (-not $installed) {
        throw "gentle-ai could not be installed with official methods."
    }

    Add-ItemUnique -List $script:State.Installed -Value "gentle-ai"
}

function Setup-GentleAiOpenCode {
    if ($SkipGentleAiConfig) {
        Add-ItemUnique -List $script:State.Pending -Value "gentle-ai install --agent opencode was skipped by parameter."
        return
    }

    if (-not (Test-CommandAvailable "gentle-ai")) {
        Add-ItemUnique -List $script:State.Pending -Value "gentle-ai not available for OpenCode integration step."
        return
    }

    try {
        Invoke-External -FilePath (Get-CommandPath "gentle-ai") -Arguments @("install", "--agent", "opencode", "--preset", "full-gentleman") -AllowFailure | Out-Null
    }
    catch {
        Add-ItemUnique -List $script:State.Risks -Value "gentle-ai OpenCode install command failed: $($_.Exception.Message)"
    }

    $possibleFiles = @(
        "$env:APPDATA\OpenCode\config.json",
        "$env:USERPROFILE\.config\opencode\config.json",
        "$env:APPDATA\gentle-ai\config.yaml",
        "$env:USERPROFILE\.gentle-ai\config.yaml"
    )

    foreach ($file in $possibleFiles) {
        if (Test-Path -LiteralPath $file) {
            Add-ItemUnique -List $script:State.ConfigFiles -Value $file
        }
    }
}

function Install-Gga {
    $bash = Find-GitBash
    if (-not $bash) {
        throw "Git Bash (bash.exe) not found. Install Git for Windows and retry."
    }

    Ensure-Directory -Path $SourceRoot
    $ggaDir = Join-Path $SourceRoot "gentleman-guardian-angel"

    if (-not (Test-Path -LiteralPath $ggaDir)) {
        Invoke-External -FilePath (Get-CommandPath "git") -Arguments @("clone", "https://github.com/Gentleman-Programming/gentleman-guardian-angel.git", $ggaDir) | Out-Null
        Add-ItemUnique -List $script:State.Installed -Value "GGA source"
    }
    else {
        Invoke-External -FilePath (Get-CommandPath "git") -Arguments @("-C", $ggaDir, "pull", "--ff-only") -AllowFailure | Out-Null
        Add-ItemUnique -List $script:State.AlreadyInstalled -Value "GGA source"
    }

    $ggaPosix = $ggaDir -replace '\\','/'
    $installCmd = "cd '$ggaPosix' && bash install.sh"
    Invoke-External -FilePath $bash -Arguments @("-lc", $installCmd) -AllowFailure | Out-Null

    Add-UserPathEntry -Entry "$env:USERPROFILE\bin"

    if (-not (Test-CommandAvailable "gga")) {
        Add-ItemUnique -List $script:State.Pending -Value "gga command not yet on PATH in this session; restart shell or ensure installer target path is exported."
    }
    else {
        Add-ItemUnique -List $script:State.Installed -Value "GGA"
        $script:State.Methods["GGA"] = "Official install.sh via Git Bash"
    }

    $script:State.Paths["git_bash"] = $bash
}

function Capture-Paths {
    $script:State.Paths["git"] = Get-CommandPath -Name "git"
    $script:State.Paths["go"] = Get-CommandPath -Name "go"
    $script:State.Paths["scoop"] = Get-CommandPath -Name "scoop"
    $script:State.Paths["opencode"] = Get-CommandPath -Name "opencode"
    $script:State.Paths["engram"] = Get-CommandPath -Name "engram"
    $script:State.Paths["gentle_ai"] = Get-CommandPath -Name "gentle-ai"
    $script:State.Paths["gga"] = Get-CommandPath -Name "gga"
}

function Capture-Versions {
    $script:State.Versions["git --version"] = Get-CommandOutput -Name "git" -Args @("--version")
    $script:State.Versions["go version"] = Get-CommandOutput -Name "go" -Args @("version")
    $script:State.Versions["scoop --version"] = Get-CommandOutput -Name "scoop" -Args @("--version")
    $script:State.Versions["opencode --version"] = Get-CommandOutput -Name "opencode" -Args @("--version")
    $script:State.Versions["engram version"] = Get-CommandOutput -Name "engram" -Args @("version")
    $script:State.Versions["gentle-ai --version"] = Get-CommandOutput -Name "gentle-ai" -Args @("--version")
    $script:State.Versions["gga version"] = Get-CommandOutput -Name "gga" -Args @("version")
}

function Write-InitialAudit {
    Write-Phase "FASE 1 - AUDITORIA INICIAL"

    if (-not $IsWindows) {
        throw "This installer supports Windows native only."
    }

    $os = Get-CimInstance Win32_OperatingSystem
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
    $psVersion = $PSVersionTable.PSVersion.ToString()

    $audit = [ordered]@{
        Windows = "$($os.Caption) ($($os.Version))"
        Architecture = "$arch"
        PowerShell = $psVersion
        Git = if (Test-CommandAvailable "git") { "Present" } else { "Missing" }
        Go = if (Test-CommandAvailable "go") { "Present" } else { "Missing" }
        Scoop = if (Test-CommandAvailable "scoop") { "Present" } else { "Missing" }
        Node = if (Test-CommandAvailable "node") { "Present" } else { "Missing" }
        Npm = if (Test-CommandAvailable "npm") { "Present" } else { "Missing" }
        GitBash = if (Find-GitBash) { "Present" } else { "Missing" }
    }

    $script:State.Paths["initial_path_user"] = [Environment]::GetEnvironmentVariable("Path", "User")
    $script:State.Paths["initial_path_machine"] = [Environment]::GetEnvironmentVariable("Path", "Machine")

    Write-Host "Mini resumen de estado inicial:"
    $audit.GetEnumerator() | ForEach-Object {
        Write-Host (" - {0}: {1}" -f $_.Key, $_.Value)
    }
}

function Write-FinalReport {
    Capture-Paths
    Capture-Versions

    Write-Phase "REPORTE FINAL"

    Write-Host "1. RESUMEN"
    Write-Host " - Instalado: $($script:State.Installed -join ', ')"
    Write-Host " - Ya estaba: $($script:State.AlreadyInstalled -join ', ')"
    Write-Host " - Metodos:"
    foreach ($k in $script:State.Methods.Keys) {
        Write-Host ("   - {0}: {1}" -f $k, $script:State.Methods[$k])
    }

    Write-Host ""
    Write-Host "2. VERSIONES"
    foreach ($k in $script:State.Versions.Keys) {
        Write-Host (" - {0}: {1}" -f $k, $script:State.Versions[$k])
    }

    Write-Host ""
    Write-Host "3. RUTAS IMPORTANTES"
    foreach ($k in $script:State.Paths.Keys) {
        if ($k -like "initial_path_*") { continue }
        Write-Host (" - {0}: {1}" -f $k, $script:State.Paths[$k])
    }
    if ($script:State.ConfigFiles.Count -gt 0) {
        Write-Host " - Configs modificadas/encontradas:"
        foreach ($f in $script:State.ConfigFiles) {
            Write-Host ("   - {0}" -f $f)
        }
    }

    Write-Host ""
    Write-Host "4. CAMBIOS EN PATH"
    if ($script:State.PathChanges.Count -eq 0) {
        Write-Host " - Sin cambios en PATH"
    }
    else {
        foreach ($c in $script:State.PathChanges) {
            Write-Host (" - {0}" -f $c)
        }
    }

    Write-Host ""
    Write-Host "5. PENDIENTES"
    if ($script:State.Pending.Count -eq 0) {
        Write-Host " - Ninguno"
    }
    else {
        foreach ($p in $script:State.Pending) {
            Write-Host (" - {0}" -f $p)
        }
    }

    Write-Host ""
    Write-Host "6. ERRORES O RIESGOS"
    if ($script:State.Risks.Count -eq 0 -and $script:State.Errors.Count -eq 0) {
        Write-Host " - Ninguno"
    }
    else {
        foreach ($r in $script:State.Risks) {
            Write-Host (" - Riesgo: {0}" -f $r)
        }
        foreach ($e in $script:State.Errors) {
            Write-Host (" - Error: {0}" -f $e)
        }
    }

    $reportDir = Join-Path $WorkspaceRoot "automation\output"
    Ensure-Directory -Path $reportDir

    $ts = Get-Date -Format "yyyyMMdd-HHmmss"
    $reportFile = Join-Path $reportDir "windows-gentleman-stack-report-$ts.txt"

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("1. RESUMEN")
    $lines.Add("Instalado: $($script:State.Installed -join ', ')")
    $lines.Add("Ya estaba: $($script:State.AlreadyInstalled -join ', ')")
    foreach ($k in $script:State.Methods.Keys) { $lines.Add("Metodo $k: $($script:State.Methods[$k])") }
    $lines.Add("")
    $lines.Add("2. VERSIONES")
    foreach ($k in $script:State.Versions.Keys) { $lines.Add("$k => $($script:State.Versions[$k])") }
    $lines.Add("")
    $lines.Add("3. RUTAS IMPORTANTES")
    foreach ($k in $script:State.Paths.Keys) {
        if ($k -like "initial_path_*") { continue }
        $lines.Add("$k => $($script:State.Paths[$k])")
    }
    foreach ($f in $script:State.ConfigFiles) { $lines.Add("config => $f") }
    $lines.Add("")
    $lines.Add("4. CAMBIOS EN PATH")
    foreach ($c in $script:State.PathChanges) { $lines.Add($c) }
    $lines.Add("")
    $lines.Add("5. PENDIENTES")
    foreach ($p in $script:State.Pending) { $lines.Add($p) }
    $lines.Add("")
    $lines.Add("6. ERRORES O RIESGOS")
    foreach ($r in $script:State.Risks) { $lines.Add("Riesgo: $r") }
    foreach ($e in $script:State.Errors) { $lines.Add("Error: $e") }

    Set-Content -LiteralPath $reportFile -Value $lines -Encoding UTF8
    Write-Host ""
    Write-Host "Reporte guardado en: $reportFile"
}

try {
    Write-InitialAudit

    Write-Phase "FASE 2 - PREPARAR DEPENDENCIAS"
    Install-Git
    Install-Go
    Install-Scoop

    $bashPath = Find-GitBash
    if (-not $bashPath) {
        throw "Git Bash not found after dependency setup."
    }
    $script:State.Paths["git_bash"] = $bashPath

    Write-Phase "FASE 3 - INSTALAR OPENCODE"
    Install-OpenCode

    Write-Phase "FASE 4 - INSTALAR ENGRAM DESDE FUENTE"
    Install-Engram

    Write-Phase "FASE 5 - INTEGRAR ENGRAM CON OPENCODE"
    if (-not $SkipOpenCodeConfig) {
        Setup-EngramOpenCode
    }

    Write-Phase "FASE 6 - INSTALAR GENTLE-AI"
    Install-GentleAi
    Setup-GentleAiOpenCode

    Write-Phase "FASE 7 - INSTALAR GGA"
    Install-Gga

    Write-Phase "FASE 8 - VERIFICACIONES FINALES"
    Capture-Versions

    if ($script:State.Versions["opencode --version"] -eq "NOT INSTALLED") {
        Add-ItemUnique -List $script:State.Errors -Value "OpenCode still not installed after all fallbacks."
    }
    if ($script:State.Versions["engram version"] -eq "NOT INSTALLED") {
        Add-ItemUnique -List $script:State.Errors -Value "Engram still not installed after source build."
    }
    if ($script:State.Versions["gentle-ai --version"] -eq "NOT INSTALLED") {
        Add-ItemUnique -List $script:State.Errors -Value "gentle-ai still not installed after all fallbacks."
    }

    Add-ItemUnique -List $script:State.Pending -Value "If OpenCode provider/model credentials are missing, complete provider login and model setup to fully use gentle-ai presets."
}
catch {
    Add-ItemUnique -List $script:State.Errors -Value $_.Exception.Message
}
finally {
    Write-FinalReport

    if ($script:State.Errors.Count -gt 0) {
        Write-Host ""
        Write-Host "Completed with blocking errors." -ForegroundColor Red
        exit 1
    }

    Write-Host ""
    Write-Host "Completed successfully." -ForegroundColor Green
    exit 0
}
