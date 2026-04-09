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