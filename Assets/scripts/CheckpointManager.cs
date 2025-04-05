using UnityEngine;
using System.Collections.Generic;

public class CheckpointManager : MonoBehaviour
{
    public GameObject checkpointPrefab;
    private List<string> triggeredCheckpoints = new List<string>();
    private Dictionary<string, GameObject> checkpoints = new Dictionary<string, GameObject>();
    // assuming tile size doesnt change
    float tileSize = 6.0f;

    void Start()
    {
        // Wait a short time to ensure dungeon has been generated
        Invoke("SpawnCheckpoints", 0.5f);
    }

    void SpawnCheckpoints()
    {
        // Find the DungeonManager (assumed to be the parent)
        Transform dungeonParent = transform.parent;
        if (dungeonParent == null)
        {
            Debug.LogError("CheckpointManager should be a child of DungeonManager");
            return;
        }

        // Find all entry and exit tiles
        foreach (Transform child in dungeonParent)
        {
            if (child.name.StartsWith("EntryTile_") || child.name.StartsWith("ExitTile_"))
            {
                // Create a checkpoint at this location
                GameObject checkpoint = Instantiate(checkpointPrefab, child.position, Quaternion.identity);
                checkpoint.name = "Checkpoint_" + child.name;
                checkpoint.transform.parent = transform;
                
                // Move the checkpoint slightly up to ensure player hits it
                checkpoint.transform.position = new Vector3(
                    child.position.x + tileSize * 1.25f,
                    child.position.y + 5.0f,
                    child.position.z - tileSize * 1.25f
                );

                if (child.name.Contains("NORTH")){
                    checkpoint.transform.rotation = Quaternion.Euler(0, 90, 0);
                } else if (child.name.Contains("SOUTH")){
                    checkpoint.transform.rotation = Quaternion.Euler(0, 90, 0);
                } else if (child.name.Contains("EAST")){
                    checkpoint.transform.rotation = Quaternion.Euler(0, 0, 0);
                } else if (child.name.Contains("WEST")){
                    checkpoint.transform.rotation = Quaternion.Euler(0, 0, 0);
                }
                
                // Set up the checkpoint logic - THIS IS THE CRITICAL FIX
                CheckpointLogic checkpointLogic = checkpoint.GetComponent<CheckpointLogic>();
                if (checkpointLogic == null)
                {
                    // Add the component if it doesn't exist
                    checkpointLogic = checkpoint.AddComponent<CheckpointLogic>();
                }
                
                // Ensure we set the manager reference correctly
                checkpointLogic.checkpointName = child.name;
                checkpointLogic.manager = this;  // This should be a direct reference to this script instance
                
                // Store reference to the checkpoint
                checkpoints.Add(child.name, checkpoint);
                Debug.Log("Created checkpoint: " + child.name + " with manager: " + (checkpointLogic.manager != null ? "valid" : "null"));
            }
        }
        
        Debug.Log($"Spawned {checkpoints.Count} checkpoints");
    }
    
    // Called by CheckpointLogic when player triggers a checkpoint
    public void OnCheckpointTriggered(string checkpointName)
    {
        if (!triggeredCheckpoints.Contains(checkpointName))
        {
            triggeredCheckpoints.Add(checkpointName);
            Debug.Log($"Player reached checkpoint: {checkpointName}");
            
            // Handle room-specific logic
            if (checkpointName.StartsWith("EntryTile_"))
            {
                string roomNumberStr = checkpointName.Substring("EntryTile_".Length);
                if (int.TryParse(roomNumberStr, out int roomNumber))
                {
                    OnRoomEntered(roomNumber);
                }
            }
            else if (checkpointName.StartsWith("ExitTile_"))
            {
                string roomNumberStr = checkpointName.Substring("ExitTile_".Length);
                if (int.TryParse(roomNumberStr, out int roomNumber))
                {
                    OnRoomExited(roomNumber);
                }
            }
        }
    }
    
    void OnRoomEntered(int roomNumber)
    {
        Debug.Log($"Player entered room {roomNumber}");
        
        switch (roomNumber)
        {
            case 0: // Spawn room
                // Actions for spawn room
                break;
            case 1: // Entry room
                // Actions for entry room
                break;
            case 2: // Ballroom
                // Actions for ballroom
                break;
            case 3: // Shop 
                // Actions for shop
                break;
            case 4: // Exit
                // Actions for exit room
                break;
        }
    }
    
    void OnRoomExited(int roomNumber)
    {
        Debug.Log($"Player exited room {roomNumber}");
        // Add room-specific actions here
    }
    
    public bool HasTriggeredCheckpoint(string checkpointName)
    {
        return triggeredCheckpoints.Contains(checkpointName);
    }
    
    public void ResetCheckpoint(string checkpointName)
    {
        if (triggeredCheckpoints.Contains(checkpointName))
        {
            triggeredCheckpoints.Remove(checkpointName);
            Debug.Log($"Reset checkpoint: {checkpointName}");
        }
    }
    
    public void ResetAllCheckpoints()
    {
        triggeredCheckpoints.Clear();
        Debug.Log("Reset all checkpoints");
    }
}