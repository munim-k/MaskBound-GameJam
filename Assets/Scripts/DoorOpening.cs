using UnityEngine;

public class DoorOpening : MonoBehaviour
{
    [SerializeField] private EnemySpawner spawner;
    [SerializeField] private int ArenaIndex;
    void Start()
    {
       spawner.OnArenaCompletedServer+= HandleArenaCompleted;
    }
    
    void HandleArenaCompleted(int index)
    {
        if(index == ArenaIndex)
        {
           Animation anim = GetComponent<Animation>();
              anim.Play();
        }
    }

    public void OpenDoor(int index)
    {
        if (index == ArenaIndex)
        {
            Animation anim = GetComponent<Animation>();
            anim.Play();
        }
    }
}
