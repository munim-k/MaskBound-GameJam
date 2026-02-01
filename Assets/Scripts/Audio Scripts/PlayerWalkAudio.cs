using UnityEngine;

public class PlayerWalkAudio : MonoBehaviour
{
    public void PlayPlayerWalkSound()
    {
        AudioManager.instance.PlayOneShot(FMODEvents.instance.playerFootsteps, transform.position);
    }
}
