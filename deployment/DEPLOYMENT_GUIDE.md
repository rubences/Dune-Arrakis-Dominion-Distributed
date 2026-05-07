# Despliegue Unificado: Web (Vercel) + Servicios .NET + CrewAI + Unity

## 1) Stack local en 1 comando

```bash
bash deployment/deploy-full-stack.sh up
```

Servicios:
- Frontend Next.js: http://localhost:3000
- SimulationService (.NET): http://localhost:8080
- PersistenceService (.NET): http://localhost:8081

Para apagar:

```bash
bash deployment/deploy-full-stack.sh down
```

## 2) Variables de entorno CrewAI (SimulationService)

Definir antes de levantar:

```bash
export CREWAI_BASE_URL="https://dune-arrakis-dominion-distributed-developme-97e89950.crewai.com"
export CREWAI_BEARER_TOKEN="..."
export DECISION_CREWAI_BASE_URL="https://gaming-analytics-content-automation-v1-ef54-0203d4b9.crewai.com"
export DECISION_CREWAI_BEARER_TOKEN="..."
```

## 3) Despliegue web en Vercel

1. Importa el proyecto `frontend` en Vercel.
2. Configura variables:
   - `NEXT_PUBLIC_SIMULATION_API_URL` = URL pública de SimulationService
   - `NEXT_PUBLIC_PERSISTENCE_API_URL` = URL pública de PersistenceService
3. Ejecuta deploy (Preview/Production).

## 4) Unity conectado al backend

En Unity (`BackendManager`), define `BaseUrl` al endpoint de SimulationService desplegado.
- Local: `http://localhost:8080`
- Cloud: `https://<tu-simulation-service>/`

Con esto, Unity invoca los endpoints del servidor autoritario y el servidor decide cuándo consultar CrewAI.

## 5) Flujo end-to-end recomendado

1. Levantar servicios .NET (local o cloud).
2. Configurar tokens CrewAI en SimulationService.
3. Levantar/desplegar frontend (Vercel).
4. Abrir Unity y apuntar `BaseUrl` al SimulationService.
5. Ejecutar un ciclo y validar:
   - `/api/simulation/ai/health`
   - `/api/simulation/ai/strategic-advice`
   - `/api/simulation/month/process`
