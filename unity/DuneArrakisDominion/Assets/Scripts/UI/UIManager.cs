// ============================================================
// DuneArrakis Dominion - UIManager
// Controlador visual principal. Gestiona todos los paneles UI,
// animaciones, toasts y el panel de estado de agentes.
// Diseñado para una estética cinematográfica y llamativa.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using DuneArrakis.Unity.Core;
using DuneArrakis.Unity.Data;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DuneArrakis.Unity.UI
{
    public enum ToastType { Info, Success, Warning, Error }

    public class UIManager : MonoBehaviour
    {
        // ── Panel References ───────────────────────────────────────────────────
        [Header("Root Panels")]
        public GameObject panelMainMenu;
        public GameObject panelHUD;
        public GameObject panelMonthResults;
        public GameObject panelAgentMonitor;
        public GameObject panelCreatureShop;
        public GameObject panelAdvice;
        public GameObject panelLoading;

        [Header("HUD Elements")]
        public TextMeshProUGUI txtSolaris;
        public TextMeshProUGUI txtMonth;
        public TextMeshProUGUI txtScenario;
        public TextMeshProUGUI txtFoodStock;
        public Button btnEndTurn;
        public Button btnAdvice;

        [Header("Agent Monitor")]
        public TextMeshProUGUI txtAgentStrategic;
        public TextMeshProUGUI txtAgentLogistics;
        public Animator agentPulseAnimator;

        [Header("Month Results Panel")]
        public TextMeshProUGUI txtResultTitle;
        public Transform       eventLogContainer;
        public GameObject      eventLogEntryPrefab;
        public Button          btnContinue;

        [Header("Advice Panel")]
        public TextMeshProUGUI txtAdviceContent;
        public Button          btnCloseAdvice;

        [Header("Toast System")]
        public Transform toastContainer;
        public GameObject toastPrefab;

        [Header("Loading")]
        public TextMeshProUGUI txtLoadingStatus;
        public Animator        loadingSpinnerAnimator;

        // ── Animations / Tweens ────────────────────────────────────────────────
        [Header("Animations")]
        [Tooltip("Duración en segundos del fade in/out de paneles")]
        public float panelFadeDuration = 0.35f;

        // ── Unity ──────────────────────────────────────────────────────────────
        private void Start()
        {
            // Wire up button callbacks
            btnEndTurn?.onClick.AddListener(() => GameController.Instance.EndTurnAndProcess());
            btnAdvice?.onClick.AddListener(() => GameController.Instance.RequestStrategicAdvice());
            btnContinue?.onClick.AddListener(() =>
            {
                HidePanel(panelMonthResults);
                GameController.Instance.ContinueAfterResolution();
            });
            btnCloseAdvice?.onClick.AddListener(() => HidePanel(panelAdvice));

            // Wire up game controller events
            var gc = GameController.Instance;
            if (gc != null)
            {
                gc.OnPhaseChanged.AddListener(OnPhaseChanged);
                gc.OnStateUpdated.AddListener(RefreshAll);
                gc.OnAgentStatusChanged.AddListener(OnAgentStatusUpdate);
            }

            // Start on main menu
            ShowOnly(panelMainMenu);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // PHASE MANAGEMENT
        // ═══════════════════════════════════════════════════════════════════════

        public void OnPhaseChanged(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.MainMenu:
                    ShowOnly(panelMainMenu);
                    break;

                case GamePhase.Loading:
                    ShowPanel(panelLoading);
                    SetLoadingText("Conectando con el servidor de simulación...");
                    break;

                case GamePhase.Planning:
                    HidePanel(panelLoading);
                    ShowPanel(panelHUD);
                    ShowPanel(panelAgentMonitor);
                    SetAgentStatus("StrategicAdvisor",   "⏸ En espera de tu orden");
                    SetAgentStatus("LogisticsAutomation", "⏸ En espera de tu orden");
                    btnEndTurn?.GetComponent<CanvasGroup>()?.gameObject.SetActive(true);
                    break;

                case GamePhase.AgentsProcessing:
                    SetLoadingText("Los agentes IA están procesando el turno...");
                    ShowPanel(panelLoading);
                    btnEndTurn?.gameObject.SetActive(false);
                    agentPulseAnimator?.SetBool("IsActive", true);
                    break;

                case GamePhase.MonthResolution:
                    HidePanel(panelLoading);
                    agentPulseAnimator?.SetBool("IsActive", false);
                    btnEndTurn?.gameObject.SetActive(true);
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // DATA REFRESH
        // ═══════════════════════════════════════════════════════════════════════

        public void RefreshAll(GameState state)
        {
            if (state?.activeScenario == null) return;
            var s = state.activeScenario;

            if (txtSolaris  != null) txtSolaris.text  = $"<color=#FFD700>◈</color> {s.currentSolaris:N0} Solaris";
            if (txtMonth    != null) txtMonth.text    = $"Mes  {s.currentMonth}";
            if (txtScenario != null) txtScenario.text = s.name.ToUpper();
            if (txtFoodStock!= null) txtFoodStock.text= $"🌾 {s.storedFoodUnits} Unidades";
        }

        public void ShowMonthResults(SimulationResult result)
        {
            if (txtResultTitle != null)
                txtResultTitle.text = $"Resolución — Mes {result.month}";

            // Limpiar entradas anteriores
            foreach (Transform child in eventLogContainer)
                Destroy(child.gameObject);

            // Rellenar eventos del mes
            foreach (var ev in result.events)
                SpawnEventEntry(ev);

            ShowPanel(panelMonthResults);
        }

        public void ShowAdvicePanel(string advice)
        {
            if (txtAdviceContent != null)
                txtAdviceContent.text = advice;
            ShowPanel(panelAdvice);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // AGENT MONITOR
        // ═══════════════════════════════════════════════════════════════════════

        public void OnAgentStatusUpdate(string payload)
        {
            var parts = payload.Split(':', 2);
            if (parts.Length != 2) return;
            SetAgentStatus(parts[0], parts[1]);
        }

        private void SetAgentStatus(string agentKey, string status)
        {
            switch (agentKey)
            {
                case "StrategicAdvisor"    when txtAgentStrategic  != null:
                    txtAgentStrategic.text  = $"Agente Estratégico\n<size=70%><color=#AAAAAA>{status}</color></size>";
                    break;
                case "LogisticsAutomation" when txtAgentLogistics  != null:
                    txtAgentLogistics.text  = $"Agente Logístico\n<size=70%><color=#AAAAAA>{status}</color></size>";
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // TOAST NOTIFICATIONS
        // ═══════════════════════════════════════════════════════════════════════

        public void ShowToast(string message, ToastType type = ToastType.Info)
        {
            if (toastPrefab == null || toastContainer == null) return;

            var go = Instantiate(toastPrefab, toastContainer);
            var txt = go.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
            {
                var color = type switch
                {
                    ToastType.Success => "#44FF88",
                    ToastType.Warning => "#FFCC44",
                    ToastType.Error   => "#FF4455",
                    _                 => "#CCCCFF"
                };
                txt.text = $"<color={color}>{ToastIcon(type)}</color>  {message}";
            }

            // Auto-destruir después de 4 segundos
            Destroy(go, 4f);

            // Pequeña animación de entrada
            StartCoroutine(AnimateToast(go));
        }

        private static string ToastIcon(ToastType t) => t switch
        {
            ToastType.Success => "✔",
            ToastType.Warning => "⚠",
            ToastType.Error   => "✖",
            _                 => "ℹ"
        };

        private IEnumerator AnimateToast(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
            var rt = go.GetComponent<RectTransform>();

            cg.alpha = 0;
            var startPos = rt.anchoredPosition + new Vector2(0, -20);
            rt.anchoredPosition = startPos;

            float t = 0;
            while (t < panelFadeDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / panelFadeDuration);
                cg.alpha = p;
                rt.anchoredPosition = Vector2.Lerp(startPos, startPos + new Vector2(0, 20), p);
                yield return null;
            }

            yield return new WaitForSeconds(3f);

            t = 0;
            while (t < panelFadeDuration)
            {
                t += Time.deltaTime;
                cg.alpha = 1f - Mathf.Clamp01(t / panelFadeDuration);
                yield return null;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // PANEL HELPERS
        // ═══════════════════════════════════════════════════════════════════════

        public void ShowPanel(GameObject panel)
        {
            if (panel == null) return;
            panel.SetActive(true);
            StartCoroutine(FadeCanvasGroup(panel, 0, 1, panelFadeDuration));
        }

        public void HidePanel(GameObject panel)
        {
            if (panel == null) return;
            StartCoroutine(FadeCanvasGroup(panel, 1, 0, panelFadeDuration, () => panel.SetActive(false)));
        }

        private void ShowOnly(GameObject targetPanel)
        {
            panelMainMenu?.SetActive(false);
            panelHUD?.SetActive(false);
            panelMonthResults?.SetActive(false);
            panelAgentMonitor?.SetActive(false);
            panelLoading?.SetActive(false);
            panelAdvice?.SetActive(false);
            ShowPanel(targetPanel);
        }

        private IEnumerator FadeCanvasGroup(GameObject go, float from, float to, float duration, System.Action onComplete = null)
        {
            var cg = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
            cg.alpha = from;
            float t = 0;
            while (t < duration)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            cg.alpha = to;
            onComplete?.Invoke();
        }

        private void SetLoadingText(string text)
        {
            if (txtLoadingStatus != null) txtLoadingStatus.text = text;
        }

        private void SpawnEventEntry(SimulationEvent ev)
        {
            if (eventLogEntryPrefab == null) return;

            var go  = Instantiate(eventLogEntryPrefab, eventLogContainer);
            var txt = go.GetComponentInChildren<TextMeshProUGUI>();
            if (txt == null) return;

            var icon = ev.eventType switch
            {
                "Compra"       => "🛒",
                "Muerte"       => "💀",
                "Reproduccion" => "🥚",
                "Visitantes"   => "👥",
                "Gastos"       => "💸",
                "Salud"        => "❤️",
                "Traslado"     => "📦",
                "Construccion" => "🏗",
                _              => "📋"
            };

            var solarisTag = ev.solarisChange != 0
                ? ev.solarisChange > 0
                    ? $" <color=#44FF88>+{ev.solarisChange:N0} ◈</color>"
                    : $" <color=#FF4455>{ev.solarisChange:N0} ◈</color>"
                : "";

            txt.text = $"{icon}  {ev.description}{solarisTag}";
        }
    }
}
