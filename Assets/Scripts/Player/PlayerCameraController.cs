using Unity.Netcode;
using UnityEngine;

public class PlayerCameraController : NetworkBehaviour
{
    [SerializeField] private Camera fpsCamera;
    [SerializeField] private AudioListener audioListener;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            // Disable camera for non-owners
            fpsCamera.enabled = false;
            audioListener.enabled = false;
            return;
        }

        // Enable camera for local player
        fpsCamera.enabled = true;
        audioListener.enabled = true;

        // Optional: lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
