# Dune-Arrakis-Dominion-Distributed

## Integracion CrewAI

El repositorio incluye una integracion inicial con CrewAI AMP en el Servicio de Simulacion y un script Python de orquestacion para automatizar mejoras del sistema distribuido.

### Configuracion del servicio .NET

El servicio lee estos valores desde la seccion CrewAi de appsettings o desde variables de entorno equivalentes:

- CrewAi__BaseUrl
- CrewAi__BearerToken
- CrewAi__RequestTimeoutSeconds

La URL del deployment ya viene preconfigurada. El token debe inyectarse localmente y no se almacena en el repositorio.

Endpoints nuevos del servicio de simulacion:

- GET /api/simulation/ai/health
- GET /api/simulation/ai/inputs
- POST /api/simulation/ai/kickoff
- GET /api/simulation/ai/status/{kickoffId}
- POST /api/simulation/ai/strategic-advice

### Script de orquestacion CrewAI

Se agrego el script Python en automation/crewai_dune_orchestrator.py. Define los agentes:

- Software_Architect
- Persistence_Engineer
- Simulation_Developer
- Frontend_Developer

Y los ejecuta en orden secuencial con Process.sequential para construir el simulador distribuido en C# por hitos.

Variables esperadas:

- CREWAI_API_URL
- CREWAI_BEARER_TOKEN
- CREWAI_USER_BEARER_TOKEN

Ejecucion local del script:

```bash
python -m venv .venv
source .venv/bin/activate
pip install -r automation/requirements.txt
export CREWAI_API_URL="https://dune-arrakis-dominion-distributed-developme-97e89950.crewai.com"
export CREWAI_BEARER_TOKEN="..."
export CREWAI_USER_BEARER_TOKEN="..."
python automation/crewai_dune_orchestrator.py --mode local
```

Invocacion remota contra el deployment AMP:

```bash
python automation/crewai_dune_orchestrator.py --mode remote --wait
```

### Script de automatizacion mensual de decisiones

Tambien se agrego automation/crewai_monthly_decision_automation.py para el segundo crew, orientado a ejecutar decisiones de juego por ronda con dos agentes:

- Mentat_Financiero
- Maestro_de_Bestias

El script admite un archivo JSON con el estado de la partida y genera un archivo JSON con las acciones a ejecutar.

Ejemplo local:

```bash
python automation/crewai_monthly_decision_automation.py --mode local --state-file game_state.json
```

Ejemplo remoto:

```bash
export CREWAI_API_URL="https://gaming-analytics-content-automation-v1-ef54-0203d4b9.crewai.com"
export CREWAI_BEARER_TOKEN="..."
export CREWAI_USER_BEARER_TOKEN="..."
python automation/crewai_monthly_decision_automation.py --mode remote --wait --state-file game_state.json
```

## Automatizacion integral del proyecto (enfoque agentico)

Se agrego `automation/agentic_project_automation.py` para automatizar todo el ciclo tecnico con un enfoque basado en la guia de agentes de IBM: agentes especializados, uso de herramientas, guardrails y evaluacion continua.

Agentes incluidos en el pipeline:

- PlannerAgent: define el plan de ejecucion por fases.
- QualityAgent: ejecuta restore, build y test de .NET.
- SimulationOpsAgent: dispara la automatizacion mensual de decisiones con CrewAI.
- GovernanceAgent: aplica guardrails de credenciales, evalua resultados y genera recomendaciones.

Salida del pipeline:

- `automation/output/agentic_automation_report.json`: reporte JSON con pasos, comandos, errores y recomendaciones.
- `automation/output/agentic_automation_history.json`: memoria de ejecuciones previas para ajustar el perfil adaptativo.
- `automation/output/monthly_actions.json`: acciones mensuales si se ejecuta la fase AI.

### Ejecucion local recomendada

Validacion tecnica completa sin fase AI:

```bash
python automation/agentic_project_automation.py --skip-ai
```

Modo adaptativo con memoria historica + remediacion automatica de tests:

```bash
python automation/agentic_project_automation.py --skip-ai --adaptive --auto-remediate
```

Pipeline completo con AI local:

```bash
python automation/agentic_project_automation.py --ai-mode local
```

Pipeline con AI remota (requiere tokens):

```bash
export CREWAI_API_URL="https://..."
export CREWAI_BEARER_TOKEN="..."
export CREWAI_USER_BEARER_TOKEN="..."
python automation/agentic_project_automation.py --ai-mode remote --wait-remote
```

Opciones nuevas relevantes:

- `--adaptive`: usa historial de fallos para ajustar fases del pipeline.
- `--auto-remediate`: si fallan tests, ejecuta rerun selectivo y guarda comparativa.
- `--history-file`: ruta de memoria historica (por defecto `automation/output/agentic_automation_history.json`).
- `--history-keep`: cantidad maxima de ejecuciones historicas almacenadas.
- `--test-results-file`: ruta del `.trx` para analisis y remediacion.

### Automatizacion en GitHub Actions

Se agrego el workflow `.github/workflows/agentic-automation.yml` que:

- Corre en push/PR a `main`, programado semanalmente y por ejecucion manual.
- Ejecuta el pipeline de calidad (`--skip-ai --adaptive --auto-remediate`) para garantizar estabilidad y autocorreccion basica.
- Ejecuta fase remota AI opcional si existen secretos `CREWAI_API_URL` y token.
- Publica un resumen Markdown en el `Job Summary` de GitHub Actions.
- Publica artifacts: reporte, historial y resumen.

## Instalador nativo Windows del stack Gentleman

Se agrego el script [automation/windows/install-gentleman-stack.ps1](automation/windows/install-gentleman-stack.ps1) para instalar de forma idempotente y verificable en Windows 10/11:

- OpenCode
- Engram
- gentle-ai
- Gentleman Guardian Angel (GGA)

El script implementa fases completas:

- Auditoria inicial de sistema y herramientas.
- Instalacion de dependencias (Git, Go, Scoop, Git Bash).
- Instalacion con prioridades y fallbacks oficiales.
- Integracion de Engram y gentle-ai con OpenCode.
- Instalacion de GGA usando Git Bash.
- Verificacion final y reporte con formato de operaciones.

Ejecucion recomendada en PowerShell (Windows nativo):

```powershell
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned -Force
cd <ruta-del-repo>
powershell -ExecutionPolicy Bypass -File .\automation\windows\install-gentleman-stack.ps1
```

Salida del reporte:

- Se genera un reporte en `automation/output/windows-gentleman-stack-report-YYYYMMDD-HHMMSS.txt` con:
  - resumen
  - versiones
  - rutas importantes
  - cambios de PATH
  - pendientes
  - errores o riesgos

Guia especifica del modulo Windows:

- [automation/windows/README.md](automation/windows/README.md)

Tambien se agrego una tarea de VS Code para ejecutarlo directamente:

- [.vscode/tasks.json](.vscode/tasks.json)
- Task label: `Windows: Install OpenCode + Gentleman Stack`

## Stack Gentleman local (dentro del repositorio)

Si prefieres usar todo dentro de este repo (sin instalacion global), usa este flujo local:

- Guia: [automation/local/README.md](automation/local/README.md)
- Bootstrap local: `automation/local/bootstrap-gentleman-local.sh`
- Activacion de PATH en shell actual: `automation/local/use-gentleman-tools.sh`

Comando recomendado:

```bash
bash automation/local/bootstrap-gentleman-local.sh
source automation/local/use-gentleman-tools.sh
```

Comando unico para integrarlo con el juego y CrewAI:

```bash
bash automation/local/run-game-ai-stack.sh
```

Este flujo ejecuta en cadena:

- setup de Engram para OpenCode
- preset full-gentleman de gentle-ai
- automatizacion mensual CrewAI ya implementada en `automation/crewai_monthly_decision_automation.py`
- revision GGA sobre cambios staged en `src/DuneArrakis.SimulationService` y `tests/DuneArrakis.Tests`

Tasks nuevas en VS Code para ejecucion sin friccion:

- `Local: Auto-Stage Simulation + GGA Review`
- `Local: Auto-Stage + Full Game AI Stack` (complementado con CrewAI)

Esto deja disponibles, dentro del propio proyecto:

- `automation/tools/bin/engram`
- `automation/tools/bin/gentle-ai`
- `automation/tools/bin/gga`
- `automation/tools/skills/curated`
- `automation/tools/skills/community`
