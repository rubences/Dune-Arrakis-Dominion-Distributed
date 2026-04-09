// ============================================================
// DuneArrakis Dominion - CreatureSlot
// Muestra una criatura individual dentro de un EnclaveView.
// Al hacer click permite alimentar o transferir la criatura.
// ============================================================

using DuneArrakis.Unity.Core;
using DuneArrakis.Unity.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DuneArrakis.Unity.UI
{
    public class CreatureSlot : MonoBehaviour
    {
        [Header("UI Elements")]
        public TextMeshProUGUI txtCreatureName;
        public TextMeshProUGUI txtHealth;
        public TextMeshProUGUI txtAge;
        public TextMeshProUGUI txtFoodStatus;
        public Slider          healthSlider;
        public Image           healthFill;
        public Button          btnFeed;
        public Button          btnTransfer;

        [Header("Health Colors")]
        public Color colorHealthy  = new Color(0.2f, 0.9f, 0.4f);
        public Color colorWarning  = new Color(1f, 0.8f, 0.1f);
        public Color colorCritical = new Color(1f, 0.2f, 0.2f);

        // ── Internal ───────────────────────────────────────────────────────────
        private Creature _creature;
        private string   _enclaveId;

        public void Setup(Creature creature, string enclaveId)
        {
            _creature  = creature;
            _enclaveId = enclaveId;

            Refresh();

            btnFeed?.onClick.RemoveAllListeners();
            btnFeed?.onClick.AddListener(OnFeedClicked);

            btnTransfer?.onClick.RemoveAllListeners();
            btnTransfer?.onClick.AddListener(OnTransferClicked);
        }

        private void Refresh()
        {
            if (_creature == null) return;

            // Name & emoji by type
            var info = System.Array.Find(CreatureCatalog.All, i => (int)i.Type == _creature.type);
            var emoji = info != null ? info.Emoji : "🐾";

            if (txtCreatureName != null)
                txtCreatureName.text = $"{emoji}  {_creature.name}";

            if (txtHealth != null)
                txtHealth.text = $"❤ {_creature.health}%";

            if (txtAge != null)
                txtAge.text = $"Edad: {_creature.ageInMonths} meses";

            // Food ratio
            float foodRatio = _creature.foodRequiredPerMonth > 0
                ? (float)_creature.foodConsumedThisMonth / _creature.foodRequiredPerMonth
                : 1f;

            if (txtFoodStatus != null)
                txtFoodStatus.text = $"🌾 {foodRatio:P0} alimentado";

            // Health slider y color
            if (healthSlider != null) healthSlider.value = _creature.health / 100f;
            if (healthFill   != null)
            {
                healthFill.color = _creature.health switch
                {
                    >= 75 => colorHealthy,
                    >= 40 => colorWarning,
                    _     => colorCritical
                };
            }
        }

        private void OnFeedClicked()
        {
            if (_creature == null) return;
            int missing = _creature.foodRequiredPerMonth - _creature.foodConsumedThisMonth;
            if (missing <= 0)
            {
                FindFirstObjectByType<UIManager>()?.ShowToast("Ya está completamente alimentada.", ToastType.Info);
                return;
            }
            GameController.Instance.FeedCreature(_creature.id, missing);
        }

        private void OnTransferClicked()
        {
            if (_creature == null) return;
            // Abre el selector de enclave destino (simplificado: transferir al primer Exhibicion disponible)
            TransferCreatureDialogManager.Instance?.Open(_creature.id, _enclaveId);
        }
    }
}
