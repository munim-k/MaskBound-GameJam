using UnityEngine;

public class faceLocation : MonoBehaviour
{
    [SerializeField] private GameObject faceParentObjectField;
    [SerializeField] private GameObject maskParentObjectField;
    
    // Public getter for FaceEngine to access
    public GameObject FaceParentObject => faceParentObjectField;
    public GameObject MaskParentObject => maskParentObjectField;
}
