using Unity.Netcode;
using UnityEngine;
using Cinemachine;

public class PlayerCameraController : NetworkBehaviour
{
    [Header("Cinemachine")]
    [SerializeField] private CinemachineFreeLook freeLookCamera;
    [SerializeField] private Transform playerLookAt;

    [Header("Audio")]
    [SerializeField] private AudioListener audioListener;

    private void Awake()
    {
        // Safety: camera reference can be auto-found
        if (freeLookCamera == null)
            freeLookCamera = FindObjectOfType<CinemachineFreeLook>();

        if (audioListener == null)
            audioListener = Camera.main?.GetComponent<AudioListener>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;

        SetupLocalCamera();
    }

    private void SetupLocalCamera()
    {
        if (freeLookCamera == null)
        {
            Debug.LogError("No CinemachineFreeLook found in scene");
            return;
        }

        // 🔑 Bind camera to THIS local player
        freeLookCamera.Follow = playerLookAt;
        freeLookCamera.LookAt = playerLookAt;

        // Ensure only one AudioListener is active
        if (audioListener != null)
            audioListener.enabled = true;

        // Optional cursor control (third-person friendly)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}