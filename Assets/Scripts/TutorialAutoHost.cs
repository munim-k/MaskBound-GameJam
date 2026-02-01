using UnityEngine;
using Unity.Netcode;

public class TutorialAutoHost : MonoBehaviour
{
    void Start()
    {
        // 1. Check if NetworkManager exists
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("Tutorial Error: No NetworkManager found in the scene! Drag your NetworkManager prefab in.");
            return;
        }

        // 2. If we aren't already connected, force StartHost
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            Debug.Log("Tutorial: Auto-starting Host to enable Player movement.");
            NetworkManager.Singleton.StartHost();
        }
    }
}