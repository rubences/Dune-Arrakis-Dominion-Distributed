// ============================================================
// DuneArrakis Dominion - EnclaveView
// Component que representa visualmente un único Enclave en escena.
// Actualiza sus indicadores de criaturas, visitantes y salud.
// ============================================================

using DuneArrakis.Unity.Data;
using DuneArrakis.Unity.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DuneArrakis.Unity.UI
{
    public class EnclaveView : MonoBehaviour
    {
        [Header("Data")]
        public string EnclaveId;

        [Header("Visual Elements")]
        public TextMeshProUGUI txtName;
        public TextMeshProUGUI txtType;
        public TextMeshProUGUI txtCapacity;        // "3/5 criaturas"
        public TextMeshProUGUI txtVisitors;
        public Slider          healthBar;
        public Image           panelBackground;
        public Transform       creatureSlotContainer;
        public GameObject      creatureSlotPrefab;

        [Header("Type Colors")]
        public Color colorAclimatacion = new Color(0.2f, 0.5f, 0.8f);
        public Color colorExhibicion   = new Color(0.8f, 0.5f, 0.1f);

        // ── Pulse for active visitors ──────────────────────────────────────────
        private Animator _animator;

        private void Awake() => _animator = GetComponent<Animator>();

        private void Start()
        {
            GameController.Instance?.OnStateUpdated.AddListener(OnStateUpdated);
        }

        private void OnStateUpdated(GameState state)
        {
            var enclave = FindMyEnclave(state);
            if (enclave != null) Refresh(enclave);
        }

        public void Refresh(Enclave enclave)
        {
            EnclaveId = enclave.id;

            if (txtName != null)    txtName.text    = enclave.name;
            if (txtType != null)    txtType.text    = enclave.type == 0 ? "ACLIMATACIÓN" : "EXHIBICIÓN";
            if (panelBackground != null)
                panelBackground.color = enclave.type == 0 ? colorAclimatacion : colorExhibicion;

            // Capacidad
            int alive = CountAlive(enclave);
            if (txtCapacity != null)
                txtCapacity.text = $"{alive} / {enclave.maxCreatureCapacity}  criaturas";

            // Visitantes
            if (txtVisitors != null)
                txtVisitors.text = enclave.type == 1
                    ? $"👥 {enclave.totalVisitorsThisMonth:N0} visitantes"
                    : "🔒 Sin visitantes";

            // Barra de salud media
            if (healthBar != null)
            {
                float avg = AverageHealth(enclave);
                healthBar.value = avg / 100f;
            }

            // Actualizar slots de criaturas
            RefreshCreatureSlots(enclave);

            // Animar si hay visitantes
            _animator?.SetBool("HasVisitors", enclave.totalVisitorsThisMonth > 0);
        }

        private void RefreshCreatureSlots(Enclave enclave)
        {
            if (creatureSlotContainer == null || creatureSlotPrefab == null) return;

            foreach (Transform child in creatureSlotContainer)
                Destroy(child.gameObject);

            foreach (var creature in enclave.creatures)
            {
                if (!creature.isAlive) continue;
                var slot = Instantiate(creatureSlotPrefab, creatureSlotContainer);
                var slotComp = slot.GetComponent<CreatureSlot>();
                slotComp?.Setup(creature, enclave.id);
            }
        }

        private Enclave FindMyEnclave(GameState state)
        {
            if (string.IsNullOrEmpty(EnclaveId)) return null;
            return state?.activeScenario?.enclaves.Find(e => e.id == EnclaveId);
        }

        private static int CountAlive(Enclave e)
        {
            int count = 0;
            foreach (var c in e.creatures) if (c.isAlive) count++;
            return count;
        }

        private static float AverageHealth(Enclave e)
        {
            if (e.creatures.Count == 0) return 0;
            float total = 0;
            int alive = 0;
            foreach (var c in e.creatures)
            {
                if (!c.isAlive) continue;
                total += c.health;
                alive++;
            }
            return alive == 0 ? 0 : total / alive;
        }
    }
}
