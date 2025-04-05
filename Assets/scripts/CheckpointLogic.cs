using UnityEngine;

public class CheckpointLogic : MonoBehaviour
{
    public string checkpointName;
    public CheckpointManager manager;
    
    private void Start()
    {
        // Debug check to ensure manager is properly assigned
        if (manager == null)
        {
            Debug.LogError("CheckpointLogic on " + gameObject.name + " has no manager assigned!");
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger entered by: " + other.gameObject.name + " with tag: " + other.tag);
        
        // Check if the colliding object has the "Player" tag
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player detected at checkpoint: " + checkpointName);
            
            // Notify the manager that this checkpoint was triggered
            if (manager != null)
            {
                manager.OnCheckpointTriggered(checkpointName);
            }
            else
            {
                Debug.LogError("CheckpointLogic missing manager reference on " + gameObject.name);
            }
        }
    }
}