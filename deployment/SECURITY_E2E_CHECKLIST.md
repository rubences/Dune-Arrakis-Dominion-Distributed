# Checklist E2E + Revisión de Vulnerabilidades

## 1) Checklist endpoint a endpoint (punta a punta)

> Base URLs:
- SimulationService: `http://localhost:5200`
- PersistenceService: `http://localhost:5100`

### SimulationService

1. `POST /api/simulation/new-game`
2. `POST /api/simulation/process-month`
3. `POST /api/simulation/purchase-creature`
4. `POST /api/simulation/buy-creature`
5. `POST /api/simulation/transfer-creature`
6. `POST /api/simulation/build-facility`
7. `POST /api/simulation/feed-creature`
8. `GET /api/simulation/health`
9. `GET /api/simulation/ai/health`
10. `GET /api/simulation/ai/inputs`
11. `POST /api/simulation/ai/kickoff`
12. `GET /api/simulation/ai/status/{kickoffId}`
13. `POST /api/simulation/ai/strategic-advice`
14. `POST /api/simulation/ai/monthly-automation`
15. `POST /api/simulation/ai/webhooks/{source}`

### PersistenceService

1. `POST /api/gamestate/save`
2. `GET /api/gamestate/load/{saveName}`
3. `GET /api/gamestate/list`
4. `GET /api/gamestate/health`

---

## 2) Hallazgos de seguridad/robustez

### Alto

- **Webhook de CrewAI sin verificación de firma/HMAC**: el endpoint `POST /api/simulation/ai/webhooks/{source}` acepta payloads sin validación criptográfica de origen. Recomendado: `X-Signature` + HMAC SHA256 + timestamp + replay protection.  
- **Sin autenticación/autorización visible en endpoints de mutación** (`new-game`, `process-month`, `transfer-creature`, etc.). Recomendado: JWT/API key por rol y políticas por endpoint.

### Medio

- **Uso extensivo de HTTP local/documentación** (`http://localhost`) correcto para dev pero riesgo en despliegue si se replica. Recomendado: forzar HTTPS en producción y HSTS.
- **Secrets por variables de entorno sí, pero sin política de rotación** documentada para `CrewAi__BearerToken` y `DecisionCrewAi__BearerToken`. Recomendado: Secret Manager + rotación periódica.
- **Falta de lockfile auditable para `npm audit`** en entorno actual detectado (comando falla por lock ausente). Recomendado: versionar lockfile y ejecutar audit en CI.

### Bajo

- **Mensajes de error descriptivos** en respuestas de IA pueden filtrar detalles operativos (estado de configuración). Recomendado: mensajes genéricos hacia cliente y detalle en logs internos.

---

## 3) Plan de hardening mínimo (acción inmediata)

1. Añadir auth por API Key o JWT en endpoints críticos.
2. Implementar firma HMAC para webhooks CrewAI.
3. Añadir rate limiting global y por endpoint.
4. Activar validación estricta de DTOs (`[Required]`, tamaños máximos, enums).
5. Integrar SAST/SCA en CI:
   - `dotnet list package --vulnerable`
   - `npm audit --production`
6. Añadir pruebas de seguridad automáticas para:
   - payload malformado,
   - replay de webhook,
   - acceso no autenticado.
