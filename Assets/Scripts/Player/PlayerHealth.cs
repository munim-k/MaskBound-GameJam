using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using TMPro;
using FMOD.Studio;

public class PlayerHealth : NetworkBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI healthText;

    [Header("Settings")]
    [SerializeField] private float maxHealth = 100f;

    // NetworkVariable to sync health across all clients
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        100f, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    private EventInstance lowHealthInstance;

    public override void OnNetworkSpawn()
    {
        // Initialize low health FMOD event
        lowHealthInstance = AudioManager.instance.CreateInstance(FMODEvents.instance.playerLowHealth);

        // 1. Find UI first
        if (IsOwner)
        {
            GameObject hud = GameObject.FindWithTag("PlayerHUD");
            if (hud != null)
            {
                healthSlider = hud.GetComponentInChildren<Slider>();
                healthText = hud.GetComponentInChildren<TextMeshProUGUI>();

                if (healthSlider != null) 
                {
                    healthSlider.maxValue = maxHealth;
                    healthSlider.minValue = 0;
                }
            }
        }
        
        // 2. Server sets initial value BEFORE UI update
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }

        // 3. Subscribe to future changes
        currentHealth.OnValueChanged += UpdateUI;

        // 4. Force immediate update with current value
        UpdateUI(0, currentHealth.Value);
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= UpdateUI;
    }

    public void TakeDamage(float amount)
    {
        if (!IsServer) return; // Only server calculates damage

        AudioManager.instance.PlayOneShot(FMODEvents.instance.playerHurt, transform.position);

        currentHealth.Value -= amount;

        if (currentHealth.Value < maxHealth * 0.2f && currentHealth.Value > 0)
        {
            PLAYBACK_STATE playbackState;
            lowHealthInstance.getPlaybackState(out playbackState);
            if (playbackState != PLAYBACK_STATE.PLAYING)
            {
                lowHealthInstance.start();
            }
        }
            
        if (currentHealth.Value <= 0)
        {
            currentHealth.Value = 0;
            lowHealthInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            AudioManager.instance.PlayOneShot(FMODEvents.instance.playerDeath, transform.position);
            Die();
        }
    }

    private void UpdateUI(float previousValue, float newValue)
    {
        // ONLY update the HUD if this script belongs to the local player
        if (!IsOwner) return; 

        if (healthSlider != null)
        {
            healthSlider.value = newValue;
        }

        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(newValue)} / {maxHealth}";
        }
    }

    private void Die()
    {
        // Logic for respawning or disabling player goes here
    }
}