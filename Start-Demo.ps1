# ┌──────────────────────────────────────────────────────────────────────────────┐
# │  Dune Arrakis Dominion — Lanzar Demo Completa                                │
# │  Arranca el backend .NET 8 + abre Swagger en el navegador                    │
# └──────────────────────────────────────────────────────────────────────────────┘

param(
    [string]$Port = "5000",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$ServiceDir = Join-Path $Root "src\DuneArrakis.SimulationService"

Write-Host ""
Write-Host "  ██████╗ ██╗   ██╗███╗   ██╗███████╗" -ForegroundColor Yellow
Write-Host "  ██╔══██╗██║   ██║████╗  ██║██╔════╝" -ForegroundColor Yellow
Write-Host "  ██║  ██║██║   ██║██╔██╗ ██║█████╗  " -ForegroundColor Yellow
Write-Host "  ██║  ██║██║   ██║██║╚██╗██║██╔══╝  " -ForegroundColor Yellow
Write-Host "  ██████╔╝╚██████╔╝██║ ╚████║███████╗" -ForegroundColor Yellow
Write-Host "  ╚═════╝  ╚═════╝ ╚═╝  ╚═══╝╚══════╝" -ForegroundColor Yellow
Write-Host ""
Write-Host "  Arrakis Dominion — Multi-Agent Architecture Demo 2026" -ForegroundColor Cyan
Write-Host "  ─────────────────────────────────────────────────────" -ForegroundColor DarkGray
Write-Host ""

# ── Verificar .NET SDK ──────────────────────────────────────────────────────────
try {
    $dotnetVersion = dotnet --version
    Write-Host "  ✔ .NET SDK detectado: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "  ✖ .NET SDK no encontrado. Instala desde: https://aka.ms/dotnet-download" -ForegroundColor Red
    exit 1
}

# ── Build ──────────────────────────────────────────────────────────────────────
if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "  ⚙  Compilando SimulationService..." -ForegroundColor Cyan
    Push-Location $ServiceDir
    dotnet build -c Release --nologo -q
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ✖ Error de compilación. Revisa los errores anteriores." -ForegroundColor Red
        Pop-Location; exit 1
    }
    Pop-Location
    Write-Host "  ✔ Compilación exitosa." -ForegroundColor Green
}

# ── Arrancar Servicio ──────────────────────────────────────────────────────────
Write-Host ""
Write-Host "  🚀 Iniciando SimulationService en puerto $Port ..." -ForegroundColor Cyan
Write-Host "     Swagger UI: http://localhost:$Port/swagger" -ForegroundColor DarkCyan
Write-Host "     Health:     http://localhost:$Port/api/simulation/health" -ForegroundColor DarkCyan
Write-Host ""
Write-Host "  ─────────────────────────────────────────────────────" -ForegroundColor DarkGray
Write-Host "  Agentes registrados (MediatR TaskWhenAllPublisher):" -ForegroundColor White
Write-Host "    🧠 StrategicAdvisorAgent   → CrewAI Advisor" -ForegroundColor Yellow
Write-Host "    ⚙️  LogisticsAutomationAgent → CrewAI Decision Crew" -ForegroundColor Yellow
Write-Host "  ─────────────────────────────────────────────────────" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  Presiona Ctrl+C para detener el servidor." -ForegroundColor DarkGray
Write-Host ""

# Abrir Swagger en el navegador después de 3 segundos
Start-Job -ScriptBlock {
    Start-Sleep -Seconds 3
    Start-Process "http://localhost:$using:Port/swagger"
} | Out-Null

Push-Location $ServiceDir
$env:ASPNETCORE_URLS = "http://localhost:$Port"
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --no-build -c Release
Pop-Location
