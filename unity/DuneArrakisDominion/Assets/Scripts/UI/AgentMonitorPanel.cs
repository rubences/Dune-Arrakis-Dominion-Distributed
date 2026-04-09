// ============================================================
// DuneArrakis Dominion - AgentMonitorPanel
// Panel lateral que visualiza el estado de los Agentes IA
// en tiempo real con un efecto de "pulso" holográfico.
// ============================================================

using System.Collections;
using DuneArrakis.Unity.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DuneArrakis.Unity.UI
{
    public class AgentMonitorPanel : MonoBehaviour
    {
        [Header("Agent: Strategic Advisor")]
        public Image             strategicIcon;
        public TextMeshProUGUI   strategicName;
        public TextMeshProUGUI   strategicStatus;
        public Image             strategicStatusDot;
        public Animator          strategicPulseAnim;

        [Header("Agent: Logistics Automation")]
        public Image             logisticsIcon;
        public TextMeshProUGUI   logisticsName;
        public TextMeshProUGUI   logisticsStatus;
        public Image             logisticsStatusDot;
        public Animator          logisticsPulseAnim;

        [Header("Status Colors")]
        public Color colorIdle       = new Color(0.4f, 0.4f, 0.4f);
        public Color colorProcessing = new Color(0.1f, 0.8f, 1.0f);
        public Color colorDone       = new Color(0.2f, 1.0f, 0.4f);
        public Color colorError      = new Color(1.0f, 0.2f, 0.2f);

        private void Start()
        {
            GameController.Instance?.OnAgentStatusChanged.AddListener(OnAgentStatusChanged);
            GameController.Instance?.OnPhaseChanged.AddListener(OnPhaseChanged);
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            bool processing = phase == GamePhase.AgentsProcessing;

            strategicPulseAnim?.SetBool("IsProcessing", processing);
            logisticsPulseAnim?.SetBool("IsProcessing", processing);

            if (processing)
            {
                SetDotColor(strategicStatusDot,  colorProcessing);
                SetDotColor(logisticsStatusDot,  colorProcessing);
            }
        }

        private void OnAgentStatusChanged(string payload)
        {
            var parts = payload.Split(':', 2);
            if (parts.Length != 2) return;

            var agent  = parts[0];
            var status = parts[1];

            var dotColor = GetDotColor(status);

            if (agent == "StrategicAdvisor")
            {
                if (strategicStatus != null)  strategicStatus.text = status;
                SetDotColor(strategicStatusDot, dotColor);
                strategicPulseAnim?.SetBool("IsProcessing", IsProcessing(status));
            }
            else if (agent == "LogisticsAutomation")
            {
                if (logisticsStatus != null) logisticsStatus.text = status;
                SetDotColor(logisticsStatusDot, dotColor);
                logisticsPulseAnim?.SetBool("IsProcessing", IsProcessing(status));
            }
        }

        private Color GetDotColor(string status)
        {
            if (status.Contains("✅")) return colorDone;
            if (status.Contains("❌")) return colorError;
            if (status.Contains("🧠") || status.Contains("⚙️") || status.Contains("Analizando") || status.Contains("Evaluando"))
                return colorProcessing;
            return colorIdle;
        }

        private static bool IsProcessing(string status)
            => status.Contains("🧠") || status.Contains("⚙️") || status.Contains("procesando") || status.Contains("Evaluando");

        private static void SetDotColor(Image img, Color color)
        {
            if (img != null) img.color = color;
        }
    }
}
