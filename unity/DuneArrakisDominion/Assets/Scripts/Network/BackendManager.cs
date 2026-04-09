// ============================================================
// DuneArrakis Dominion - BackendManager
// Capa de comunicación HTTP con el SimulationService (.NET 8).
// Utiliza UnityWebRequest + JsonUtility para serialización.
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
        public string baseUrl = "http://localhost:5000";

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
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>POST /api/simulation/new-game?scenarioType={t}&amp;saveName={n}</summary>
        public void NewGame(int scenarioType, string saveName, Action<GameState> onSuccess = null)
            => StartCoroutine(PostRequest<GameState>(
                $"{baseUrl}/api/simulation/new-game?scenarioType={scenarioType}&saveName={UnityWebRequest.EscapeURL(saveName)}",
                null, onSuccess));

        /// <summary>POST /api/simulation/process-month</summary>
        public void ProcessMonth(GameState gameState, Action<SimulationResult> onSuccess = null)
            => StartCoroutine(PostRequest<SimulationResult>(
                $"{baseUrl}/api/simulation/process-month",
                JsonUtility.ToJson(gameState), onSuccess,
                afterSuccess: result => OnMonthResult.Invoke(result)));

        /// <summary>POST /api/simulation/purchase-creature</summary>
        public void PurchaseCreature(GameState gs, string enclaveId, int creatureType, Action<GameState> onSuccess = null)
        {
            var payload = JsonUtility.ToJson(new PurchaseCreatureRequest
            {
                enclaveId = enclaveId,
                creatureType = creatureType
            });

            // El backend espera el estado completo + el request embebido
            var full = JsonUtility.ToJson(new PurchaseCreatureFullRequest
            {
                gameState = gs,
                enclaveId = enclaveId,
                creatureType = creatureType
            });
            StartCoroutine(PostRequest<GameState>($"{baseUrl}/api/simulation/purchase-creature", full, onSuccess));
        }

        /// <summary>POST /api/simulation/feed-creature</summary>
        public void FeedCreature(GameState gs, string creatureId, int foodAmount, Action<GameState> onSuccess = null)
        {
            var full = $"{{\"gameState\":{JsonUtility.ToJson(gs)},\"creatureId\":\"{creatureId}\",\"foodAmount\":{foodAmount}}}";
            StartCoroutine(PostRequest<GameState>($"{baseUrl}/api/simulation/feed-creature", full, onSuccess));
        }

        /// <summary>POST /api/simulation/transfer-creature</summary>
        public void TransferCreature(GameState gs, string srcId, string dstId, string creatureId, Action<GameState> onSuccess = null)
        {
            var full = $"{{\"gameState\":{JsonUtility.ToJson(gs)},\"sourceEnclaveId\":\"{srcId}\",\"targetEnclaveId\":\"{dstId}\",\"creatureId\":\"{creatureId}\"}}";
            StartCoroutine(PostRequest<GameState>($"{baseUrl}/api/simulation/transfer-creature", full, onSuccess));
        }

        // ─── Strategic Advisor ────────────────────────────────────────────────
        public void GetStrategicAdvice(GameState gs, string prompt, Action<string> onAdvice = null)
        {
            var full = $"{{\"gameState\":{JsonUtility.ToJson(gs)},\"prompt\":\"{prompt}\",\"waitForCompletion\":false}}";
            StartCoroutine(PostRawRequest($"{baseUrl}/api/simulation/ai/strategic-advice", full, onAdvice));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // INTERNAL COROUTINES
        // ═══════════════════════════════════════════════════════════════════════

        private IEnumerator PostRequest<T>(string url, string jsonBody, Action<T> onSuccess, Action<T> afterSuccess = null)
        {
            OnRequestStart.Invoke();

            using var request = BuildPost(url, jsonBody);
            yield return request.SendWebRequest();

            OnRequestEnd.Invoke();

            if (request.result != UnityWebRequest.Result.Success)
            {
                var msg = $"[BackendManager] Error {request.responseCode}: {request.downloadHandler?.text}";
                Debug.LogError(msg);
                OnError.Invoke(msg);
                yield break;
            }

            var raw = request.downloadHandler.text;
            var result = JsonUtility.FromJson<T>(raw);
            onSuccess?.Invoke(result);
            afterSuccess?.Invoke(result);
        }

        private IEnumerator PostRawRequest(string url, string jsonBody, Action<string> onSuccess)
        {
            OnRequestStart.Invoke();
            using var request = BuildPost(url, jsonBody);
            yield return request.SendWebRequest();
            OnRequestEnd.Invoke();

            if (request.result != UnityWebRequest.Result.Success)
            {
                OnError.Invoke(request.downloadHandler?.text ?? request.error);
                yield break;
            }

            onSuccess?.Invoke(request.downloadHandler.text);
        }

        private static UnityWebRequest BuildPost(string url, string jsonBody)
        {
            var request = new UnityWebRequest(url, "POST");
            if (jsonBody != null)
            {
                var bytes = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler   = new UploadHandlerRaw(bytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/json");
            }
            else
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Accept", "application/json");
            }
            return request;
        }
    }

    // Helper combinado (el backend espera gameState + params en el body)
    [Serializable]
    internal class PurchaseCreatureFullRequest
    {
        public DuneArrakis.Unity.Data.GameState gameState;
        public string enclaveId;
        public int creatureType;
    }
}
