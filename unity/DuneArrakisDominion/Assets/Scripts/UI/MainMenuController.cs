// ============================================================
// DuneArrakis Dominion - MainMenuController
// Controla el menú principal: selección de escenario,
// nombre de la partida y botón de inicio.
// ============================================================

using DuneArrakis.Unity.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DuneArrakis.Unity.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("UI Refs")]
        public TMP_InputField inputSaveName;
        public TMP_Dropdown   dropdownScenario;
        public Button         btnStart;
        public TextMeshProUGUI txtVersion;
        public TextMeshProUGUI txtScenarioDescription;

        [Header("Scenario Descriptions")]
        [TextArea(2, 4)]
        public string[] scenarioDescriptions = new[]
        {
            "La capital de Arrakis, corazón del Imperio en el planeta de la especia. Fondos iniciales: 50,000 Solaris.",
            "Mundo industrial de la Casa Harkonnen, dominado por fábricas. Fondos iniciales: 75,000 Solaris.",
            "Mundo oceánico de la Casa Atreides, con vastos océanos y naturaleza exuberante. Fondos iniciales: 40,000 Solaris."
        };

        private void Start()
        {
            if (txtVersion != null)
                txtVersion.text = "DuneArrakis Dominion  v1.0  |  2026 Edition";

            dropdownScenario?.onValueChanged.AddListener(OnScenarioChanged);
            btnStart?.onClick.AddListener(OnStartGame);

            // Show description for default selection
            OnScenarioChanged(0);
        }

        private void OnScenarioChanged(int index)
        {
            if (txtScenarioDescription != null && index < scenarioDescriptions.Length)
                txtScenarioDescription.text = scenarioDescriptions[index];
        }

        private void OnStartGame()
        {
            var saveName = inputSaveName != null && !string.IsNullOrWhiteSpace(inputSaveName.text)
                ? inputSaveName.text.Trim()
                : $"Partida_{System.DateTime.Now:HHmm}";

            var scenarioType = dropdownScenario != null ? dropdownScenario.value : 0;

            btnStart.interactable = false;
            GameController.Instance.StartNewGame(scenarioType, saveName);
        }
    }
}
