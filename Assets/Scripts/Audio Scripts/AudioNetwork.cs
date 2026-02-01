using FMODUnity;
using Unity.Netcode;
using UnityEngine;

public class AudioNetwork : NetworkBehaviour
{
    private void Start() { 
        var listener = GetComponent<StudioListener>(); 
        if (!IsOwner) 
        { 
            listener.enabled = false;
        } 
    }
}
