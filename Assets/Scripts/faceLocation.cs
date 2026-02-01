using UnityEngine;

public class faceLocation : MonoBehaviour
{
    [SerializeField] private GameObject faceParentObjectField;
    
    // Public getter for FaceEngine to access
    public GameObject FaceParentObject => faceParentObjectField;
}
