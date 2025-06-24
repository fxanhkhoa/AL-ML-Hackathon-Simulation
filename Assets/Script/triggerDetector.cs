using Assets.Script.WebRTC;
using TMPro;
using UnityEngine;

public class triggerDetector : MonoBehaviour
{

    [SerializeField] private CameraWebRTCSocketIO appController;
    [SerializeField] private TextMeshProUGUI finishText;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Entered trigger: " + other.name);
        if (!appController.isFinished)
        {
            appController.isFinished = true;
            appController.triggerFinished();
            finishText.text = "Finished";
        }

        // You can add your custom logic here.
        // For example, if you only want to react to a specific type of object:
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player entered the invisible trigger!");
            // Perform actions like:
            // - Playing a sound
            // - Granting an item
            // - Starting a cutscene
            // - Destroying the trigger (if it's a one-time use)
            // Destroy(gameObject);
        }
    }

    // This function is called when another collider exits this trigger
    private void OnTriggerExit(Collider other)
    {
        Debug.Log("Exited trigger: " + other.name);

        // Add any logic for when objects leave the trigger
    }

    // This function is called once per frame while another collider is inside this trigger
    private void OnTriggerStay(Collider other)
    {
        // Debug.Log("Staying in trigger: " + other.name);
        // Useful for continuous effects while an object is in the trigger zone.
    }
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
