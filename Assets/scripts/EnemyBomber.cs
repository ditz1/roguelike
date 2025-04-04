using UnityEngine;

public class EnemyBomber : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public GameObject bomb;
    EnemyController enemyController;
    float time_since_last_bomb = 0f;
    float bomb_cooldown = 3f; // Time in seconds between bomb throws
    float bomber_range = 12f; // Range within which the enemy can throw bombs
    Vector3 distance_to_player;
    void Start()
    {
        enemyController = GetComponent<EnemyController>();
        if (enemyController == null)
        {
            Debug.LogError("No EnemyController component found on this GameObject!");
        }
    }

    // Update is called once per frame
    void Update()
    {
        distance_to_player = enemyController.player.position - transform.position;
        if (distance_to_player.magnitude < bomber_range)
        {
            time_since_last_bomb += Time.deltaTime;
            if (time_since_last_bomb >= bomb_cooldown)
            {
                Attack();
                time_since_last_bomb = 0f; // Reset the cooldown timer
            }
        }
    }

    // throw bomb
    void Attack()
    {
        Transform p_transform = enemyController.player;
        if (p_transform != null)
        {
            Vector3 direction = p_transform.position - transform.position;
            Quaternion rotation = Quaternion.LookRotation(direction);
            GameObject bombInstance = Instantiate(bomb, transform.position, rotation);
            Rigidbody rb = bombInstance.GetComponent<Rigidbody>();
            // arc bomb upwards slightly
            Vector3 upwardForce = new Vector3(0, 3.5f, 0);
            rb.angularVelocity = new Vector3(10f, 0, 0); // want bomb to spin a bit
            // this will affect the bomb's trajectory if its too high tho
            if (rb != null)
            {
                rb.AddForce(direction.normalized * 10f, ForceMode.Impulse); // Adjust the force as needed
                rb.AddForce(upwardForce, ForceMode.Impulse); // Add upward force to the bomb

            }
            Destroy(bombInstance, 4f);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, bomber_range);
    }


}
