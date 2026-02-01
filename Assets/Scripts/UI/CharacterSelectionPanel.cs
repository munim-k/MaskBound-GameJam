using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using MaskBound.Core.Enums;
using System.Collections.Generic;

namespace MaskBound.UI
{
    /// <summary>
    /// UI controller for character selection panel.
    /// Handles button clicks, visual updates, and confirmation flow.
    /// </summary>
    public class CharacterSelectionPanel : MonoBehaviour
    {
        [Header("Character Selection Buttons")]
        [SerializeField] private Button orcButton;
        [SerializeField] private Button scorpioButton;
        [SerializeField] private Button gargoyleButton;

        [Header("Button Texts")]
        [SerializeField] private TextMeshProUGUI orcButtonText;
        [SerializeField] private TextMeshProUGUI scorpioButtonText;
        [SerializeField] private TextMeshProUGUI gargoyleButtonText;

        [Header("Selection Display")]
        [SerializeField] private TextMeshProUGUI selectedAffinityText;
        [SerializeField] private GameObject confirmButton;

        [Header("Ready Status")]
        [SerializeField] private TextMeshProUGUI readyStatusText;

        [Header("References")]
        [SerializeField] private CharacterSelectionManager selectionManager;

        private EnemyFamily mySelection = 0;
        private bool isConfirmed = false;

        private void Start()
        {
            // Wire up button click handlers
            if (orcButton != null)
                orcButton.onClick.AddListener(() => OnAffinityButtonClicked(EnemyFamily.Orc));
            
            if (scorpioButton != null)
                scorpioButton.onClick.AddListener(() => OnAffinityButtonClicked(EnemyFamily.ScorpionMan));
            
            if (gargoyleButton != null)
                gargoyleButton.onClick.AddListener(() => OnAffinityButtonClicked(EnemyFamily.Gargoyle));

            if (confirmButton != null)
            {
                confirmButton.GetComponent<Button>()?.onClick.AddListener(OnConfirmButtonClicked);
                confirmButton.SetActive(false);
            }

            // Subscribe to selection manager events
            if (selectionManager != null)
            {
                selectionManager.OnSelectionsChanged += UpdateButtonStates;
                selectionManager.OnConfirmationsChanged += UpdateReadyStatus;
            }

            // Initial UI state
            UpdateSelectedText();
            UpdateButtonStates(new Dictionary<ulong, EnemyFamily>());
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (selectionManager != null)
            {
                selectionManager.OnSelectionsChanged -= UpdateButtonStates;
                selectionManager.OnConfirmationsChanged -= UpdateReadyStatus;
            }
        }

        /// <summary>
        /// Handle affinity button click
        /// </summary>
        private void OnAffinityButtonClicked(EnemyFamily affinity)
        {
            if (isConfirmed)
            {
                Debug.LogWarning("[CharacterSelection] Cannot change selection after confirming!");
                return;
            }

            // Send selection to server
            selectionManager.SelectAffinityServerRpc((int)affinity);
            
            // Update local state (will be confirmed by server via network callback)
            mySelection = affinity;
            UpdateSelectedText();
            
            // Show confirm button
            if (confirmButton != null)
                confirmButton.SetActive(true);

            Debug.Log($"[CharacterSelection] Selected {affinity}");
        }

        /// <summary>
        /// Handle confirm button click
        /// </summary>
        private void OnConfirmButtonClicked()
        {
            if (mySelection == 0)
            {
                Debug.LogWarning("[CharacterSelection] Cannot confirm without selection!");
                return;
            }

            if (isConfirmed)
            {
                Debug.LogWarning("[CharacterSelection] Already confirmed!");
                return;
            }

            // Send confirmation to server
            selectionManager.ConfirmSelectionServerRpc();
            
            // Update local state
            isConfirmed = true;
            
            // Hide confirm button and disable selection buttons
            if (confirmButton != null)
                confirmButton.SetActive(false);

            DisableAllButtons();
            
            Debug.Log($"[CharacterSelection] Confirmed selection: {mySelection}");
        }

        /// <summary>
        /// Update button states based on current selections
        /// </summary>
        private void UpdateButtonStates(Dictionary<ulong, EnemyFamily> selections)
        {
            if (isConfirmed)
            {
                DisableAllButtons();
                return;
            }

            var myClientId = NetworkManager.Singleton.LocalClientId;
            var available = selectionManager.GetAvailableAffinities();

            // Update Orc button
            UpdateButton(orcButton, orcButtonText, EnemyFamily.Orc, 
                available.Contains(EnemyFamily.Orc), 
                mySelection == EnemyFamily.Orc);

            // Update Scorpio button
            UpdateButton(scorpioButton, scorpioButtonText, EnemyFamily.ScorpionMan, 
                available.Contains(EnemyFamily.ScorpionMan), 
                mySelection == EnemyFamily.ScorpionMan);

            // Update Gargoyle button
            UpdateButton(gargoyleButton, gargoyleButtonText, EnemyFamily.Gargoyle, 
                available.Contains(EnemyFamily.Gargoyle), 
                mySelection == EnemyFamily.Gargoyle);
        }

        /// <summary>
        /// Update individual button state
        /// </summary>
        private void UpdateButton(Button button, TextMeshProUGUI buttonText, EnemyFamily affinity, bool isAvailable, bool isSelected)
        {
            if (button == null || buttonText == null) return;

            string baseName = affinity switch
            {
                EnemyFamily.Orc => "Orc Hunter",
                EnemyFamily.ScorpionMan => "Scorpio Hunter",
                EnemyFamily.Gargoyle => "Gargoyle Hunter",
                _ => "Unknown"
            };

            if (isSelected)
            {
                button.interactable = false;
                buttonText.text = $"{baseName} (Selected)";
                button.GetComponent<Image>().color = new Color(0.5f, 1f, 0.5f); // Green tint
            }
            else if (isAvailable)
            {
                button.interactable = true;
                buttonText.text = baseName;
                button.GetComponent<Image>().color = Color.white;
            }
            else
            {
                button.interactable = false;
                buttonText.text = $"{baseName} (Taken)";
                button.GetComponent<Image>().color = new Color(0.7f, 0.7f, 0.7f); // Gray
            }
        }

        /// <summary>
        /// Update selected affinity display text
        /// </summary>
        private void UpdateSelectedText()
        {
            if (selectedAffinityText == null) return;

            if (mySelection == 0)
            {
                selectedAffinityText.text = "Selected: None";
            }
            else
            {
                string name = mySelection switch
                {
                    EnemyFamily.Orc => "Orc Hunter",
                    EnemyFamily.ScorpionMan => "Scorpio Hunter",
                    EnemyFamily.Gargoyle => "Gargoyle Hunter",
                    _ => "Unknown"
                };
                selectedAffinityText.text = $"Selected: {name}";
            }
        }

        /// <summary>
        /// Update ready status display
        /// </summary>
        private void UpdateReadyStatus(HashSet<ulong> confirmed)
        {
            if (readyStatusText != null)
            {
                int confirmedCount = selectionManager.GetConfirmedCount();
                int totalCount = selectionManager.GetTotalClientCount();
                readyStatusText.text = $"Players Ready: {confirmedCount}/{totalCount}";
            }
        }

        /// <summary>
        /// Disable all selection buttons
        /// </summary>
        private void DisableAllButtons()
        {
            if (orcButton != null) orcButton.interactable = false;
            if (scorpioButton != null) scorpioButton.interactable = false;
            if (gargoyleButton != null) gargoyleButton.interactable = false;
        }

        /// <summary>
        /// Public method to show this panel
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            mySelection = 0;
            isConfirmed = false;
            UpdateSelectedText();
            UpdateButtonStates(new Dictionary<ulong, EnemyFamily>());
            
            if (confirmButton != null)
                confirmButton.SetActive(false);

            Debug.Log("[CharacterSelection] Panel shown");
        }

        /// <summary>
        /// Public method to hide this panel
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
