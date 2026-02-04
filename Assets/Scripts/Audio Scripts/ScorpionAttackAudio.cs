using UnityEngine;

public class ScorpionAttackAudio : MonoBehaviour
{
    public void PlayScorpionMeleeSound()
    {
        AudioManager.instance.PlayOneShot(FMODEvents.instance.scorpionMelee, transform.position);
    }

    public void PlayScorpionRangeSound()
    {
        AudioManager.instance.PlayOneShot(FMODEvents.instance.scorpionRange, transform.position);
    }
}
