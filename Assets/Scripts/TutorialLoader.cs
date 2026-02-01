using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialLoader : MonoBehaviour
{
    public void LoadTutorial() {
        SceneManager.LoadScene("Tutorial");
    }
}
