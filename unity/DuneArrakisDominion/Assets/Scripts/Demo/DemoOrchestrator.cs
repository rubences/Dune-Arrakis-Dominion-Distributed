// ============================================================
// DuneArrakis Dominion - DemoOrchestrator
// Script de demostración que arranca automáticamente el juego
// en modo offline y simula el ciclo completo de agentes visualmente.
// Útil para mostrar la demo sin necesitar el backend live.
// ============================================================

using System.Collections;
using DuneArrakis.Unity.Core;
using DuneArrakis.Unity.Network;
using DuneArrakis.Unity.UI;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DuneArrakis.Unity.Demo
{
    public class DemoOrchestrator : MonoBehaviour
    {
        [Header("Demo Settings")]
        [Tooltip("Si true, arranca la demo automáticamente al iniciar la escena")]
        public bool AutoStartDemo = true;

        [Tooltip("Segundos entre cada turno automático (0 = esperar input manual)")]
        public float AutoAdvanceTurnSeconds = 0f;

        [Header("Demo Agent Status Texts")]
        [Multiline(3)]
        public string[] strategicAdviceLines = new[]
        {
            "🧠 El enclave de Exhibición está rindiendo bien. Considera trasladar el Gusano Juvenil cuando supere los 12 meses y 80 de salud.",
            "🧠 Los fondos actuales permiten adquirir un Halcón del Desierto. Incrementará los ingresos por visitante en un 15%.",
            "🧠 Alerta: El Tigre Laza lleva 2 meses con alimentación insuficiente. Prioriza su dieta este turno.",
        };

        private int _adviceIndex;
        private int _autoTurnCount;

        private void Start()
        {
            if (AutoStartDemo)
                StartCoroutine(RunDemoSequence());
        }

        private IEnumerator RunDemoSequence()
        {
            // Esperar a que los managers estén listos
            yield return new WaitUntil(() =>
                BackendManager.Instance != null && GameController.Instance != null);

            // Activar modo offline de facto si el backend no responde
            yield return StartCoroutine(DetectBackendMode());

            // Lanzar nueva partida
            GameController.Instance.StartNewGame(0, "Demo Arrakeen 2026");
        }

        private IEnumerator DetectBackendMode()
        {
            bool backendReachable = false;
            BackendManager.Instance.CheckHealth(ok => backendReachable = ok);
            yield return new WaitForSeconds(3f);

            if (!backendReachable)
            {
                BackendManager.Instance.OfflineMode = true;
                Debug.Log("[DemoOrchestrator] Backend no detectado → Activando modo OFFLINE.");
                FindFirstObjectByType<UIManager>()?.ShowToast(
                    "Modo Demo Offline: Backend .NET no detectado. Ejecutando con datos sintéticos.", ToastType.Warning);
            }
            else
            {
                Debug.Log("[DemoOrchestrator] Backend .NET detectado → Modo ONLINE activo.");
                FindFirstObjectByType<UIManager>()?.ShowToast(
                    "✅ Backend conectado en " + BackendManager.Instance.BaseUrl, ToastType.Success);
            }
        }

        // Llamado desde botón de UI "Demo Auto-Turno"
        public void TriggerDemoAdvice()
        {
            var advice = strategicAdviceLines[_adviceIndex % strategicAdviceLines.Length];
            _adviceIndex++;
            FindFirstObjectByType<UIManager>()?.ShowAdvicePanel(advice);
        }
    }
}
