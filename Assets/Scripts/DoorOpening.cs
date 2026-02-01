using UnityEngine;

public class DoorOpening : MonoBehaviour
{
    [SerializeField] private EnemySpawner spawner;
    [SerializeField] private int ArenaIndex;
    

    public void OpenDoor(int index)
    {
        Debug.Log("DoorOpening: OpenDoor() called" + index);
        if (index == ArenaIndex)
        {
            transform.position += new Vector3(0, -10f, 0);

            AudioManager.instance.PlayOneShot(FMODEvents.instance.fogWall, transform.position);
        }
    }
}
