using UnityEngine;

public class OrcAttackAudio : MonoBehaviour
{
    public void PlayOrcMeleeSound()
    {
        AudioManager.instance.PlayOneShot(FMODEvents.instance.trollMelee, transform.position);
    }

    public void PlayOrcRangeSound()
    {
        AudioManager.instance.PlayOneShot(FMODEvents.instance.trollRange, transform.position);
    }
}
