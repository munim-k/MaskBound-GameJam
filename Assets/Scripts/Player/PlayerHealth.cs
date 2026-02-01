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
    void Start()
    {
       GameObject hud = GameObject.FindWithTag("PlayerHUD");
            Debug.Log("Hud" + hud); 
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
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }

        // 3. Subscribe to future changes
        currentHealth.OnValueChanged += UpdateUI;

        // 4. Force immediate update with current value
        UpdateUI(0, currentHealth.Value);
    }
    private EventInstance lowHealthInstance;

    public override void OnNetworkSpawn()
    {
        // Initialize low health FMOD event
        if(IsOwner)
        {
            lowHealthInstance = AudioManager.instance.CreateInstance(FMODEvents.instance.playerLowHealth);
        }

        // 1. Find UI first
        
            
        
        
        // 2. Server sets initial value BEFORE UI update
        
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= UpdateUI;
    }

    public void TakeDamage(float amount)
    {
        if(IsOwner)
            AudioManager.instance.PlayOneShot(FMODEvents.instance.playerHurt, transform.position);
        
        if (!IsServer) return; // Only server modifies health

        currentHealth.Value -= amount;
        Debug.Log($"PlayerHealth: TakeDamage() called {amount}");

        // Heartbeat trigger
        if (currentHealth.Value < maxHealth * 0.2f && currentHealth.Value > 0)
        {
            // Tell the owning client to start heartbeat
            StartHeartbeatClientRpc(new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { OwnerClientId } // only owner hears heartbeat
                }
            });
        }

        // Death
        if (currentHealth.Value <= 0)
        {
            currentHealth.Value = 0;

            // Stop heartbeat on owner
            StopHeartbeatClientRpc(new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { OwnerClientId }
                }
            });

            PlayDeathSoundClientRpc(new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { OwnerClientId }
                }
            });

            Die();
        }
    }

    [ClientRpc]
    private void StartHeartbeatClientRpc(ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return; // Only owner plays the looping heartbeat

        if (!lowHealthInstance.isValid())
            lowHealthInstance = AudioManager.instance.CreateInstance(FMODEvents.instance.playerLowHealth);

        PLAYBACK_STATE state;
        lowHealthInstance.getPlaybackState(out state);
        if (state != PLAYBACK_STATE.PLAYING)
            lowHealthInstance.start();
    }

    [ClientRpc]
    private void StopHeartbeatClientRpc(ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;

        if (lowHealthInstance.isValid())
            lowHealthInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
    }

    [ClientRpc]
    private void PlayDeathSoundClientRpc(ClientRpcParams rpcParams = default)
    {
        AudioManager.instance.PlayOneShot(FMODEvents.instance.playerDeath, transform.position);
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