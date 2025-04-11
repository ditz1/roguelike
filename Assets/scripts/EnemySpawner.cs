using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    // when the room is spawned, spawn enemies based on room size
    public GameObject bomber_enemy;
    int room_size = 0;
    Vector3 room_position;
    int num_enemies = 0;
    //int room_edge_offset = 5;

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
        // number of enemies is scale / 10 
        num_enemies = (int)room_size / 10;
        ConfigureSpawnBounds();
        SpawnEnemies(num_enemies);
    }

    void ConfigureSpawnBounds()
    {
        // sometimes the room will have negative positions, so need to account for that
        x_lower_bound = room_position.x - room_size / 2.5f;
        x_upper_bound = room_position.x + room_size / 2.5f;
        z_lower_bound = room_position.z - room_size / 2.5f;
        z_upper_bound = room_position.z + room_size / 2.5f;
        // add offset to bounds in direction of center
    }

    void SpawnEnemies(int enemies)
    {
        for (int i = 0; i < enemies; i++)
        {
            // spawn enemy at random position in room
            Vector3 spawn_position = new Vector3(
                Random.Range(x_lower_bound, x_upper_bound),
                3.0f,
                Random.Range(z_lower_bound, z_upper_bound)
            );
            Instantiate(bomber_enemy, spawn_position, Quaternion.identity);
        }
    }
}
