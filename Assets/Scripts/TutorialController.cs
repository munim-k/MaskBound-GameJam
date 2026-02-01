using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement; // Or UnityEngine.UI if using legacy text

public class TutorialController : MonoBehaviour
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
                instructionText.text = "Good luck!";
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
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
        // tutorialPanel.SetActive(false);
        this.enabled = false; // Turn off this script to save performance
        SceneManager.LoadScene("MainMenu"); // Load main game scene
    }
}