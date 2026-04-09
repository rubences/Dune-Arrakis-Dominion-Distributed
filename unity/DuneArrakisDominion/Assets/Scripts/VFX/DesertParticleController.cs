// ============================================================
// DuneArrakis Dominion - DesertParticleController
// Controla los efectos de partículas de arena y atmósfera.
// Ajusta la intensidad según el estado del juego.
// ============================================================

using DuneArrakis.Unity.Core;
using UnityEngine;

namespace DuneArrakis.Unity.VFX
{
    public class DesertParticleController : MonoBehaviour
    {
        [Header("Particle Systems")]
        public ParticleSystem sandstormParticles;
        public ParticleSystem ambientDustParticles;
        public ParticleSystem heatHazeParticles;

        [Header("Intensity Settings")]
        [Range(0f, 1f)] public float baseSandIntensity = 0.3f;
        [Range(0f, 1f)] public float agentProcessingIntensity = 0.9f;
        [Range(0f, 1f)] public float planningIntensity = 0.2f;

        private ParticleSystem.EmissionModule _sandEmission;
        private float _baseRate;

        private void Start()
        {
            if (sandstormParticles != null)
            {
                _sandEmission = sandstormParticles.emission;
                _baseRate = _sandEmission.rateOverTime.constant;
            }

            GameController.Instance?.OnPhaseChanged.AddListener(OnPhaseChanged);
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            float intensity = phase switch
            {
                GamePhase.AgentsProcessing => agentProcessingIntensity,
                GamePhase.MonthResolution  => 0.6f,
                GamePhase.Planning         => planningIntensity,
                _                          => baseSandIntensity
            };

            SetIntensity(intensity);
        }

        private void SetIntensity(float t)
        {
            if (sandstormParticles != null)
            {
                var em = sandstormParticles.emission;
                em.rateOverTime = Mathf.Lerp(0, _baseRate * 3, t);
            }

            if (ambientDustParticles != null)
            {
                var main = ambientDustParticles.main;
                main.simulationSpeed = Mathf.Lerp(0.5f, 2.5f, t);
            }
        }
    }
}
