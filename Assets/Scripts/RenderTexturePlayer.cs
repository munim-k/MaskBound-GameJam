using Unity.Netcode;
using UnityEngine;

public class RenderTexturePlayer : NetworkBehaviour
{
    [SerializeField] private GameObject camera;
    void Start()
    {
        if(!IsOwner)
            camera.SetActive(false);
        
    }
}
