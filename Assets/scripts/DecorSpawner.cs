using UnityEngine;
using System.Collections.Generic;

public class DecorSpawner : MonoBehaviour
{
    public GameObject[] decor_prefabs;
    private List<GameObject> spawned_decor = new List<GameObject>(); // Track spawned objects
    int room_size = 0;
    private Vector3 room_position;

    float x_lower_bound;
    float x_upper_bound;
    float z_lower_bound;
    float z_upper_bound;

    void Start() 
    {
        Transform transform = GetComponent<Transform>();
        room_size = (int)transform.localScale.x;
        Debug.Log("Room size: " + room_size);
        room_position = transform.position;

        ConfigureSpawnBounds();
        int num_decor = GetNumRoomDecor();
        for (int i = 0; i < num_decor; i++)
        {
            SpawnDecor(i);
        }
    }

    int GetNumRoomDecor()
    {
        if (room_size < 29)
        {
            return 1;
        } else if (room_size >= 29 && room_size < 59){
            return 3;
        } else if (room_size >= 59){
            return 5;
        }
        return 0;
    }

    void ConfigureSpawnBounds()
    {
        x_lower_bound = room_position.x - room_size / 3.0f;
        x_upper_bound = room_position.x + room_size / 3.0f;
        z_lower_bound = room_position.z - room_size / 3.0f;
        z_upper_bound = room_position.z + room_size / 3.0f;
    }

    bool CheckCollisionWithSpawnedDecor(Vector3 spawn_position)
    {
        int decorId = 0; 
        if (decorId >= decor_prefabs.Length) decorId = decor_prefabs.Length - 1;

        BoxCollider prefab_collider = decor_prefabs[decorId].GetComponent<BoxCollider>();
        if (prefab_collider == null) return false;

        Vector3 scaled_size = Vector3.Scale(prefab_collider.size, new Vector3(5, 5, 5)) / 2f; // Half extents
        Vector3 center = spawn_position + Vector3.Scale(prefab_collider.center, new Vector3(5, 5, 5));

        Collider[] hit_colliders = Physics.OverlapBox(center, scaled_size, Quaternion.identity);
        return hit_colliders.Length > 0;
    }

    void SpawnDecor(int decor_id)
    {
        if (decor_id >= decor_prefabs.Length) 
        { 
            decor_id = decor_prefabs.Length - 1; 
        }
        
        Vector3 spawn_position = new Vector3(Random.Range(x_lower_bound, x_upper_bound), 1.3f, Random.Range(z_lower_bound, z_upper_bound));
        if (decor_id > 0)
        {
            // loop until we find a position that doesn't collide with other decor
            int tries = 0;
            while (CheckCollisionWithSpawnedDecor(spawn_position) && (tries < 20))
            {
                spawn_position = new Vector3(Random.Range(x_lower_bound, x_upper_bound), 1.3f, Random.Range(z_lower_bound, z_upper_bound));
                tries++;
            }
            if (tries >= 20)
            {
                Debug.Log("Failed to find a spawn position after 20 tries");
                return;
            }
        }
        
        GameObject new_decor = Instantiate(decor_prefabs[decor_id], spawn_position, Quaternion.identity);
        new_decor.transform.localScale = new Vector3(5, 5, 5);
        spawned_decor.Add(new_decor);
    }
}