using UnityEngine;

public class GargoyleAttackAudio : MonoBehaviour
{
    public void PlayGargoyleMeleeSound()
    {
        AudioManager.instance.PlayOneShot(FMODEvents.instance.gargoyleMelee, transform.position);
    }

    public void PlayGargoyleRangeSound()
    {
        AudioManager.instance.PlayOneShot(FMODEvents.instance.gargoyleScreech, transform.position);
    }
}
