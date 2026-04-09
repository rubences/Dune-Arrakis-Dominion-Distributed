// ============================================================
// DuneArrakis Dominion - BackendManager (Reescrito para demo funcional)
// Comunicación HTTP REST con el SimulationService (.NET 8).
// Todos los endpoints coinciden exactamente con el controlador.
// Soporta fallback offline (modo simulación local).
// ============================================================

using System;
using System.Collections;
using System.Text;
using DuneArrakis.Unity.Data;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace DuneArrakis.Unity.Network
{
    public class BackendManager : MonoBehaviour
    {
        // ── Inspector Settings ─────────────────────────────────────────────────
        [Header("Backend Connection")]
        [Tooltip("URL base del SimulationService. Ej: http://localhost:5000")]
        public string BaseUrl = "http://localhost:5000";

        [Header("Offline / Demo Mode")]
        [Tooltip("Si true, genera datos sintéticos sin necesitar el backend .NET")]
        public bool OfflineMode = false;

        [Header("Events")]
        public UnityEvent<string>           OnError        = new();
        public UnityEvent<GameState>        OnStateLoaded  = new();
        public UnityEvent<SimulationResult> OnMonthResult  = new();
        public UnityEvent                   OnRequestStart = new();
        public UnityEvent                   OnRequestEnd   = new();

        // ── Singleton ──────────────────────────────────────────────────────────
        public static BackendManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // PUBLIC API — Endpoints exactos del SimulationController
        // ═══════════════════════════════════════════════════════════════════════

        /// POST /api/simulation/new-game?scenarioType={t}&saveName={n}
        public void NewGame(int scenarioType, string saveName, Action<GameState> onSuccess = null)
        {
            if (OfflineMode)
            {
                StartCoroutine(DelayedCallback(() => onSuccess?.Invoke(LocalGameFactory.CreateNewGame(scenarioType, saveName))));
                return;
            }
            var url = $"{BaseUrl}/api/simulation/new-game?scenarioType={scenarioType}&saveName={UnityWebRequest.EscapeURL(saveName)}";
            StartCoroutine(SendPost<GameState>(url, null, gs => { OnStateLoaded.Invoke(gs); onSuccess?.Invoke(gs); }));
        }

        /// POST /api/simulation/process-month
        public void ProcessMonth(GameState gameState, Action<SimulationResult> onSuccess = null)
        {
            if (OfflineMode)
            {
                StartCoroutine(DelayedCallback(() =>
                {
                    var r = LocalGameFactory.SimulateMonth(gameState);
                    OnMonthResult.Invoke(r);
                    onSuccess?.Invoke(r);
                }, 2f));
                return;
            }
            StartCoroutine(SendPost<SimulationResult>(
                $"{BaseUrl}/api/simulation/process-month",
                JsonUtility.ToJson(gameState),
                r => { OnMonthResult.Invoke(r); onSuccess?.Invoke(r); }));
        }

        /// POST /api/simulation/purchase-creature
        public void PurchaseCreature(GameState gs, string enclaveId, int creatureType, Action<GameState> onSuccess = null)
        {
            if (OfflineMode) { StartCoroutine(DelayedCallback(() => onSuccess?.Invoke(gs))); return; }
            var body = BuildJson(
                ("gameState", JsonUtility.ToJson(gs)),
                ("enclaveId", $"\"{enclaveId}\""),
                ("creatureType", $"{creatureType}"));
            StartCoroutine(SendPost<GameState>($"{BaseUrl}/api/simulation/purchase-creature", body, onSuccess));
        }

        /// POST /api/simulation/feed-creature
        public void FeedCreature(GameState gs, string creatureId, int foodAmount, Action<GameState> onSuccess = null)
        {
            if (OfflineMode) { StartCoroutine(DelayedCallback(() => onSuccess?.Invoke(gs))); return; }
            var body = BuildJson(
                ("gameState", JsonUtility.ToJson(gs)),
                ("creatureId", $"\"{creatureId}\""),
                ("foodAmount", $"{foodAmount}"));
            StartCoroutine(SendPost<GameState>($"{BaseUrl}/api/simulation/feed-creature", body, onSuccess));
        }

        /// POST /api/simulation/transfer-creature
        public void TransferCreature(GameState gs, string srcId, string dstId, string creatureId, Action<GameState> onSuccess = null)
        {
            if (OfflineMode) { StartCoroutine(DelayedCallback(() => onSuccess?.Invoke(gs))); return; }
            var body = BuildJson(
                ("gameState",         JsonUtility.ToJson(gs)),
                ("sourceEnclaveId",   $"\"{srcId}\""),
                ("targetEnclaveId",   $"\"{dstId}\""),
                ("creatureId",        $"\"{creatureId}\""));
            StartCoroutine(SendPost<GameState>($"{BaseUrl}/api/simulation/transfer-creature", body, onSuccess));
        }

        /// POST /api/simulation/ai/strategic-advice
        public void GetStrategicAdvice(GameState gs, string prompt, Action<string> onAdvice)
        {
            if (OfflineMode)
            {
                StartCoroutine(DelayedCallback(() =>
                    onAdvice?.Invoke("🧠 [Modo Offline] Recomendación: Aumentar la alimentación de las criaturas en el enclave de aclimatación antes de transferirlas. Prioriza las criaturas con más de 80 de salud para maximizar ingresos de visitantes."), 3f));
                return;
            }
            var body = BuildJson(
                ("gameState",        JsonUtility.ToJson(gs)),
                ("prompt",          $"\"{EscapeJson(prompt)}\""),
                ("waitForCompletion","false"));
            StartCoroutine(SendPostRaw($"{BaseUrl}/api/simulation/ai/strategic-advice", body, onAdvice));
        }

        /// GET /api/simulation/health
        public void CheckHealth(Action<bool> onResult)
        {
            StartCoroutine(CheckHealthCoroutine(onResult));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // COROUTINES
        // ═══════════════════════════════════════════════════════════════════════

        private IEnumerator SendPost<T>(string url, string jsonBody, Action<T> onSuccess)
        {
            OnRequestStart.Invoke();

            using var req = BuildRequest(url, jsonBody);
            yield return req.SendWebRequest();
            OnRequestEnd.Invoke();

            if (req.result != UnityWebRequest.Result.Success)
            {
                var errMsg = $"HTTP {req.responseCode}: {req.downloadHandler?.text ?? req.error}";
                Debug.LogError($"[BackendManager] {errMsg}");
                OnError.Invoke(errMsg);
                yield break;
            }

            T result;
            try   { result = JsonUtility.FromJson<T>(req.downloadHandler.text); }
            catch (Exception e)
            {
                OnError.Invoke($"Error deserializando respuesta: {e.Message}");
                yield break;
            }

            onSuccess?.Invoke(result);
        }

        private IEnumerator SendPostRaw(string url, string jsonBody, Action<string> onSuccess)
        {
            OnRequestStart.Invoke();
            using var req = BuildRequest(url, jsonBody);
            yield return req.SendWebRequest();
            OnRequestEnd.Invoke();

            if (req.result != UnityWebRequest.Result.Success)
            {
                OnError.Invoke($"HTTP {req.responseCode}: {req.downloadHandler?.text ?? req.error}");
                yield break;
            }
            onSuccess?.Invoke(req.downloadHandler.text);
        }

        private IEnumerator CheckHealthCoroutine(Action<bool> onResult)
        {
            using var req = UnityWebRequest.Get($"{BaseUrl}/api/simulation/health");
            req.timeout = 5;
            yield return req.SendWebRequest();
            onResult?.Invoke(req.result == UnityWebRequest.Result.Success);
        }

        private static IEnumerator DelayedCallback(Action action, float delay = 0.5f)
        {
            yield return new WaitForSeconds(delay);
            action?.Invoke();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════════════

        private static UnityWebRequest BuildRequest(string url, string jsonBody)
        {
            var req = new UnityWebRequest(url, "POST");
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Accept", "application/json");

            if (!string.IsNullOrEmpty(jsonBody))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            return req;
        }

        // Construye JSON inline sin dependencias externas
        private static string BuildJson(params (string key, string value)[] pairs)
        {
            var sb = new StringBuilder("{");
            for (int i = 0; i < pairs.Length; i++)
            {
                if (i > 0) sb.Append(',');
                var (k, v) = pairs[i];
                // Si v ya arranca con { o [ o " o dígito, se inserta raw; si no, se pone como string
                if (v.StartsWith("{") || v.StartsWith("[") || v.StartsWith("\"") ||
                    char.IsDigit(v[0]) || v == "true" || v == "false" || v == "null")
                    sb.Append($"\"{k}\":{v}");
                else
                    sb.Append($"\"{k}\":\"{EscapeJson(v)}\"");
            }
            sb.Append('}');
            return sb.ToString();
        }

        private static string EscapeJson(string s)
            => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    // ─── Fábrica de datos offline para demos sin backend ──────────────────────
    public static class LocalGameFactory
    {
        public static GameState CreateNewGame(int scenarioType, string saveName)
        {
            var scenario = new Scenario
            {
                id             = System.Guid.NewGuid().ToString(),
                name           = scenarioType switch { 1 => "Giedi Prime", 2 => "Caladan", _ => "Arrakeen" },
                description    = "Demo Offline — El motor de agentes simulará las respuestas.",
                currentSolaris = scenarioType switch { 1 => 75000m, 2 => 40000m, _ => 50000m },
                storedFoodUnits= 200,
                currentMonth   = 1
            };

            scenario.enclaves.Add(new Enclave
            {
                id = System.Guid.NewGuid().ToString(),
                name = "Zona de Aclimatación I",
                type = 0,
                maxCreatureCapacity = 5,
                nivelAdquisitivo = 2,
                hectareas = 50
            });

            scenario.enclaves.Add(new Enclave
            {
                id = System.Guid.NewGuid().ToString(),
                name = "Gran Exhibición de Arrakis",
                type = 1,
                maxCreatureCapacity = 20,
                nivelAdquisitivo = 8,
                hectareas = 100
            });

            return new GameState
            {
                id             = System.Guid.NewGuid().ToString(),
                saveName       = saveName,
                activeScenario = scenario
            };
        }

        public static SimulationResult SimulateMonth(GameState gs)
        {
            var month = gs.activeScenario.currentMonth;
            gs.activeScenario.currentMonth++;
            gs.activeScenario.currentSolaris += 3_500m;

            return new SimulationResult
            {
                month          = month,
                currentSolaris = gs.activeScenario.currentSolaris,
                events         = new System.Collections.Generic.List<SimulationEvent>
                {
                    new() { month = month, eventType = "Visitantes", description = "Gran Exhibición de Arrakis: 850 visitantes. Donaciones: 3.500 Solaris.", solarisChange = 3500 },
                    new() { month = month, eventType = "Salud",      description = "[Aclimatación I] Alimentación óptima: +5 salud en todas las criaturas.", solarisChange = 0 },
                    new() { month = month, eventType = "Salud",      description = "🧠 StrategicAdvisorAgent: Análisis completado — Temporada alta de visitantes detectada.", solarisChange = 0 },
                    new() { month = month, eventType = "Gastos",     description = "⚙️ LogisticsAutomationAgent: Logística aplicada — Sin traslados este mes.", solarisChange = -800 },
                }
            };
        }
    }
}
