using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement; // Or UnityEngine.UI if using legacy text
using Unity.Netcode;

public class TutorialController : NetworkBehaviour
{
    public TextMeshProUGUI instructionText; // Drag your UI text here
    public GameObject tutorialPanel;        // Drag your background panel here

    private int currentStep = 0; // Tracks progress

    void Start()
    {
        ShowStep(0);
    }

    void Update()
    {
        // STEP 0: MOVEMENT
        if (currentStep == 0)
        {
            // Check if player presses W, A, S, or D
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) || 
                Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D)) 
            {
                NextStep();
            }
        }
        if (currentStep == 1)
        {
            if (Input.GetKeyDown(KeyCode.C)) 
            {
                NextStep();
            }
        }
        // STEP 1: JUMPING
        else if (currentStep == 2)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                NextStep();
            }
        }
        // STEP 2: Melee Attack
        else if (currentStep == 3)
        {
            if (Input.GetMouseButtonDown(0)) // Left Click
            {
                NextStep();
            }
        } else if (currentStep == 4)
        {
            if (Input.GetKeyDown(KeyCode.M))
            {
                NextStep();
            }
        }
        // STEP 5: Elemental Mask
        else if (currentStep == 5)
        {
            if (Input.GetKeyDown(KeyCode.Return))
            {
                NextStep();
            }
        }
    }

    void ShowStep(int step)
    {
        switch (step)
        {
            case 0:
                instructionText.text = "Use WASD to Move";
                break;
            case 1:
                instructionText.text = "C to slide";
                break;
            case 2:
                instructionText.text = "Press SPACE to Jump";
                break;
            case 3:
                instructionText.text = "Left Click to Attack";
                break;
            case 4:
                instructionText.text = "M to Open Mask. \nMasks of certain type when equipped deal more damage to their corresponding elemental.\nThey can be swapped with other players.";
                break;
            case 5:
                instructionText.text = "Elemental masks deal more damage to their corresponding element. Fire Mask for Ice enemies, Lightning mask for metal enemies, Earth mask for rock enemies.\n Press enter to continue";
                break;
            case 6:
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;

                if (NetworkManager.Singleton == null)
                    return;

                // If we are Host or Server, this will kick all clients
                if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
                {
                    NetworkManager.Singleton.Shutdown();
                }
                instructionText.text = "Good luck!";
                Invoke("CloseTutorial", 3f); // Close after 2 seconds
                break;
        }
    }

    void NextStep()
    {
        currentStep++;
        ShowStep(currentStep);
        // Optional: Play a "ding" sound here
    }

    void CloseTutorial()
    {
        // 1. Get the special "DontDestroyOnLoad" scene
        GameObject[] ddolObjects = GetDontDestroyOnLoadObjects();

        // 2. Destroy everything in it
        foreach (GameObject obj in ddolObjects)
        {
            // Optional: Check for specific tags or names if you want to keep things like SteamManager
            Destroy(obj);
        }

        // 3. Optional: Explicitly trigger Garbage Collection for a fresh start
        Resources.UnloadUnusedAssets();

        // 4. Load the next scene
        SceneManager.LoadScene("MainMenu");
    }

    // Helper to grab objects in the hidden DDOL scene
    private GameObject[] GetDontDestroyOnLoadObjects()
    {
        GameObject temp = new GameObject();
        Object.DontDestroyOnLoad(temp);
        Scene ddolScene = temp.scene;
        Object.Destroy(temp);
        return ddolScene.GetRootGameObjects();
    }
}