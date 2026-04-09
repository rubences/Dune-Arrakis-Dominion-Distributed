// ============================================================
// DuneArrakis Dominion - GameController
// Orquestador central de Unity. Gestiona el estado de partida,
// las transiciones de fase y los comandos del jugador.
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using DuneArrakis.Unity.Data;
using DuneArrakis.Unity.Network;
using DuneArrakis.Unity.UI;
using UnityEngine;
using UnityEngine.Events;

namespace DuneArrakis.Unity.Core
{
    public enum GamePhase
    {
        MainMenu,
        Loading,
        Planning,           // jugador elige acciones del mes
        AgentsProcessing,   // agentes IA + motor procesando
        MonthResolution,    // mostrando resultados
        GameOver
    }

    public class GameController : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────
        public static GameController Instance { get; private set; }

        // ── State ──────────────────────────────────────────────────────────────
        public GameState    CurrentState { get; private set; }
        public GamePhase    Phase        { get; private set; } = GamePhase.MainMenu;
        public List<SimulationEvent> LastMonthEvents { get; private set; } = new();

        // ── Events ─────────────────────────────────────────────────────────────
        public UnityEvent<GamePhase>        OnPhaseChanged       = new();
        public UnityEvent<GameState>        OnStateUpdated       = new();
        public UnityEvent<SimulationResult> OnMonthResolved      = new();
        public UnityEvent<string>           OnAgentStatusChanged = new();

        // ── Inspector refs ─────────────────────────────────────────────────────
        [Header("Scene References")]
        public UIManager uiManager;

        // ── Agent Status Display ───────────────────────────────────────────────
        private readonly Dictionary<string, string> _agentStatus = new()
        {
            { "StrategicAdvisor",   "En espera" },
            { "LogisticsAutomation","En espera" }
        };

        // ── Unity ──────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Conectar eventos del BackendManager
            BackendManager.Instance.OnStateLoaded.AddListener(OnStateReceived);
            BackendManager.Instance.OnMonthResult.AddListener(OnMonthResultReceived);
            BackendManager.Instance.OnError.AddListener(OnBackendError);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // PUBLIC COMMANDS (llamados desde UI)
        // ═══════════════════════════════════════════════════════════════════════

        public void StartNewGame(int scenarioType, string saveName)
        {
            SetPhase(GamePhase.Loading);
            BackendManager.Instance.NewGame(scenarioType, saveName, OnStateReceived);
        }

        public void EndTurnAndProcess()
        {
            if (Phase != GamePhase.Planning) return;
            SetPhase(GamePhase.AgentsProcessing);

            // Notificar agentes como "activos" en UI
            SetAgentStatus("StrategicAdvisor",    "🧠 Analizando escenario...");
            SetAgentStatus("LogisticsAutomation",  "⚙️ Evaluando logística...");

            BackendManager.Instance.ProcessMonth(CurrentState, OnMonthResultReceived);
        }

        public void PurchaseCreature(string enclaveId, int creatureType)
        {
            if (Phase != GamePhase.Planning) return;
            BackendManager.Instance.PurchaseCreature(CurrentState, enclaveId, creatureType,
                state =>
                {
                    CurrentState = state;
                    OnStateUpdated.Invoke(CurrentState);
                    uiManager?.ShowToast($"Criatura adquirida con éxito.", ToastType.Success);
                });
        }

        public void FeedCreature(string creatureId, int foodAmount)
        {
            if (Phase != GamePhase.Planning) return;
            BackendManager.Instance.FeedCreature(CurrentState, creatureId, foodAmount,
                state =>
                {
                    CurrentState = state;
                    OnStateUpdated.Invoke(CurrentState);
                    uiManager?.ShowToast("Criatura alimentada.", ToastType.Info);
                });
        }

        public void TransferCreature(string srcEnclaveId, string dstEnclaveId, string creatureId)
        {
            if (Phase != GamePhase.Planning) return;
            BackendManager.Instance.TransferCreature(CurrentState, srcEnclaveId, dstEnclaveId, creatureId,
                state =>
                {
                    CurrentState = state;
                    OnStateUpdated.Invoke(CurrentState);
                    uiManager?.ShowToast("Criatura trasladada.", ToastType.Success);
                });
        }

        public void RequestStrategicAdvice()
        {
            if (CurrentState == null) return;
            var prompt = "Analiza el estado del zoológico y proporciona recomendaciones estratégicas urgentes.";
            SetAgentStatus("StrategicAdvisor", "🧠 Consultando al asesor...");
            BackendManager.Instance.GetStrategicAdvice(CurrentState, prompt, advice =>
            {
                SetAgentStatus("StrategicAdvisor", "✅ Análisis disponible");
                uiManager?.ShowAdvicePanel(advice);
            });
        }

        public void ContinueAfterResolution()
        {
            SetPhase(GamePhase.Planning);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CALLBACKS
        // ═══════════════════════════════════════════════════════════════════════

        private void OnStateReceived(GameState state)
        {
            CurrentState = state;
            OnStateUpdated.Invoke(CurrentState);
            SetPhase(GamePhase.Planning);
            uiManager?.RefreshAll(CurrentState);
        }

        private void OnMonthResultReceived(SimulationResult result)
        {
            LastMonthEvents = result.events;

            // Actualizar Solaris en el estado local (el backend envía el resultado, no el estado completo)
            if (CurrentState?.activeScenario != null)
                CurrentState.activeScenario.currentSolaris = result.currentSolaris;

            // Marcar agentes como completados
            SetAgentStatus("StrategicAdvisor",    "✅ Análisis completado");
            SetAgentStatus("LogisticsAutomation",  "✅ Logística aplicada");

            OnMonthResolved.Invoke(result);
            SetPhase(GamePhase.MonthResolution);
            uiManager?.ShowMonthResults(result);
        }

        private void OnBackendError(string errorMsg)
        {
            SetAgentStatus("StrategicAdvisor",    "❌ Error de conexión");
            SetAgentStatus("LogisticsAutomation",  "❌ Error de conexión");
            SetPhase(GamePhase.Planning);
            uiManager?.ShowToast($"Error: {errorMsg}", ToastType.Error);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void SetPhase(GamePhase phase)
        {
            Phase = phase;
            OnPhaseChanged.Invoke(phase);
        }

        private void SetAgentStatus(string agentName, string status)
        {
            _agentStatus[agentName] = status;
            OnAgentStatusChanged.Invoke($"{agentName}:{status}");
        }

        public Dictionary<string, string> GetAgentStatuses() => _agentStatus;
    }
}
