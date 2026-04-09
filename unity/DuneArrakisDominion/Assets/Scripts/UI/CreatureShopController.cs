// ============================================================
// DuneArrakis Dominion - CreatureShopController
// Panel de tienda para adquirir nuevas criaturas.
// Muestra fichas con coste, dieta y hábitat de cada especie.
// ============================================================

using System.Collections;
using DuneArrakis.Unity.Core;
using DuneArrakis.Unity.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DuneArrakis.Unity.UI
{
    public class CreatureShopController : MonoBehaviour
    {
        [Header("References")]
        public GameObject shopPanel;
        public Transform  cardContainer;
        public GameObject cardPrefab;
        public TextMeshProUGUI txtSolaris;
        public Button     btnClose;

        // The enclave to buy into (set before opening)
        private string _targetEnclaveId;

        private void Start()
        {
            btnClose?.onClick.AddListener(Close);
            shopPanel?.SetActive(false);
        }

        public void Open(string enclaveId)
        {
            _targetEnclaveId = enclaveId;

            // Refresh Solaris display
            var solaris = GameController.Instance?.CurrentState?.activeScenario?.currentSolaris ?? 0;
            if (txtSolaris != null)
                txtSolaris.text = $"◈ {solaris:N0} Solaris disponibles";

            // Build cards
            foreach (Transform child in cardContainer) Destroy(child.gameObject);
            foreach (var info in CreatureCatalog.All)  SpawnCard(info, solaris);

            shopPanel?.SetActive(true);
        }

        private void SpawnCard(CreatureInfo info, decimal currentSolaris)
        {
            if (cardPrefab == null) return;
            var go = Instantiate(cardPrefab, cardContainer);

            // Populate card fields by component name
            SetText(go, "TxtName",       $"{info.Emoji}  {info.Name}");
            SetText(go, "TxtCommonName", info.CommonName);
            SetText(go, "TxtCost",       $"◈ {info.AcquisitionCost:N0}");
            SetText(go, "TxtMonthly",    $"🌾 {info.MonthlyFoodCost:N0} / mes");

            // Disable card if can't afford
            var canAfford = currentSolaris >= info.AcquisitionCost;
            var btn = go.GetComponentInChildren<Button>();
            if (btn != null)
            {
                btn.interactable = canAfford;
                var captured = info;
                btn.onClick.AddListener(() =>
                {
                    GameController.Instance.PurchaseCreature(_targetEnclaveId, (int)captured.Type);
                    Close();
                });
            }
        }

        private static void SetText(GameObject root, string childName, string value)
        {
            var t = root.transform.Find(childName);
            if (t == null) return;
            var txt = t.GetComponent<TextMeshProUGUI>();
            if (txt != null) txt.text = value;
        }

        public void Close() => shopPanel?.SetActive(false);
    }
}
