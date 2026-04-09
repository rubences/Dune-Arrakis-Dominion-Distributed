// ============================================================
// DuneArrakis Dominion - TransferCreatureDialogManager
// Panel modal para seleccionar el enclave destino
// al que se quiere transferir una criatura.
// ============================================================

using System.Collections.Generic;
using DuneArrakis.Unity.Core;
using DuneArrakis.Unity.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DuneArrakis.Unity.UI
{
    public class TransferCreatureDialogManager : MonoBehaviour
    {
        public static TransferCreatureDialogManager Instance { get; private set; }

        [Header("Dialog References")]
        public GameObject  dialogPanel;
        public TextMeshProUGUI txtTitle;
        public Transform   enclaveButtonContainer;
        public GameObject  enclaveButtonPrefab;
        public Button      btnClose;

        private string _pendingCreatureId;
        private string _sourceEnclaveId;

        private void Awake()
        {
            Instance = this;
            dialogPanel?.SetActive(false);
            btnClose?.onClick.AddListener(Close);
        }

        public void Open(string creatureId, string sourceEnclaveId)
        {
            _pendingCreatureId = creatureId;
            _sourceEnclaveId   = sourceEnclaveId;

            if (txtTitle != null)
                txtTitle.text = "Seleccionar enclave destino";

            // Clear previous buttons
            foreach (Transform child in enclaveButtonContainer)
                Destroy(child.gameObject);

            // Populate with valid target enclaves
            var state = GameController.Instance?.CurrentState;
            if (state?.activeScenario?.enclaves == null) return;

            foreach (var enclave in state.activeScenario.enclaves)
            {
                if (enclave.id == sourceEnclaveId) continue;

                var go  = Instantiate(enclaveButtonPrefab, enclaveButtonContainer);
                var btn = go.GetComponent<Button>();
                var txt = go.GetComponentInChildren<TextMeshProUGUI>();
                var targetId = enclave.id;

                if (txt != null)
                    txt.text = $"{enclave.name} ({enclave.type == 0 ? "Aclimatación" : "Exhibición"})";

                btn?.onClick.AddListener(() =>
                {
                    GameController.Instance.TransferCreature(_sourceEnclaveId, targetId, _pendingCreatureId);
                    Close();
                });
            }

            dialogPanel?.SetActive(true);
        }

        public void Close() => dialogPanel?.SetActive(false);
    }
}
