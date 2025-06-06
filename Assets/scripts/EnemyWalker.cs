using UnityEngine;

public class EnemyWalker : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    EnemyController enemyController;
    public Transform the_shrine;
    Animator animator;
    public Transform target;

    float walker_range = 4.5f;
    float player_in_range_timer = 0f; // how long the player has been in range
    float player_in_range_duration = 1.5f; // duration before attacking player
    float attack_range = 2.5f;

    float shrine_in_range_timer = 0f; // how long the shrine has been in range
    float shrine_in_range_duration = 2.5f; // duration before attacking shrine

    float barrier_atk_timer = 2.4f;

    bool isAttacking = false; // Flag to prevent multiple attacks at the same time

    void Start()
    {
        enemyController = GetComponent<EnemyController>();
        if (enemyController == null)
        {
            Debug.LogError("No EnemyController component found on this GameObject!");
        }
        animator = transform.Find("zomb").GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("No Animator component found on this GameObject!");
        }

    }

    // Update is called once per frame
    void Update()
    {
        WalkToShrine();
        if (enemyController.e_health <= 0)
        {
            Vector3 direction = transform.forward; // Get the current forward direction of the enemy
            Quaternion rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 0, 180); // Rotate 90 degrees
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * 5f); // Smoothly rotate to the new direction
            Destroy(gameObject, 2f); // Destroy the enemy after 2 seconds
        }
        Attack();
    }

    void Attack()
    {
        if (the_shrine != null)
        {
            Vector3 direction = the_shrine.position - transform.position;
            if (direction.magnitude < attack_range)
            {
                shrine_in_range_timer += Time.deltaTime;

                if (shrine_in_range_timer >= shrine_in_range_duration)
                {
                    
                    GameObject shrine_canvas = the_shrine.Find("Healthbar").gameObject;
                    Shrine shrine = shrine_canvas.GetComponent<Shrine>();
                    if (shrine != null)
                    {
                        if (animator != null) { animator.Play("zombatk"); }

                        shrine.DamageShrine(10); // Deal 10 damage to the player
                        Debug.Log("Enemy attacked the shrine!");
                    }
                    else
                    {
                        Debug.LogError("Shrine null!");
                    }
                    shrine_in_range_timer = 0f;
                }
            }
            else
            {
                shrine_in_range_timer = 0f;
                
            }
        }

        Transform p_transform = enemyController.player;
        // if distance from player is less than 2.5f, attack
        if (p_transform != null)
        {
            Vector3 direction = p_transform.position - transform.position;
            if (direction.magnitude < attack_range)
            {
                player_in_range_timer += Time.deltaTime;

                if (player_in_range_timer >= player_in_range_duration)
                {
                    
                    PlayerController playerController = p_transform.GetComponent<PlayerController>();
                    if (playerController != null)
                    {
                        if (animator != null) { animator.Play("zombatk"); }
                        playerController.DamagePlayer(10); // Deal 10 damage to the player
                        Debug.Log("Enemy attacked the player!");
                    }
                    else
                    {
                        Debug.LogError("PlayerController component not found on the player!");
                    }
                    player_in_range_timer = 0f;
                }
            }
            else
            {
                player_in_range_timer = 0f;
                
            }
        }
        
    }

    void WalkToShrine()
    {
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName("zombatk"))
        {
            return;
        }
        
        if (the_shrine != null)
        {
            // if distance is very close to player, walk towards player instead of shrine
            Transform p_transform = enemyController.player;
            if (p_transform != null && Vector3.Distance(transform.position, p_transform.position) < walker_range)
            {
                target = p_transform; // Set the shrine to the player transform
                Debug.Log("Enemy is close to player, walking towards player instead of shrine.");
            }
            else
            {
                target = the_shrine;
            }
            Vector3 direction = target.position - transform.position;
            direction.y = 0; // Keep the movement on the horizontal plane
            if (direction.magnitude > 0.5f) // Check if the enemy is not already at the shrine
            {
                transform.position += direction.normalized * Time.deltaTime * 2f; // Move towards the shrine
            }
        }
        else
        {
            the_shrine = GameObject.FindGameObjectWithTag("Shrine")?.transform; // Find the shrine by tag
            Debug.LogError("The shrine is not assigned!");
        }
    }

    // void OnTriggerEnter(Collider other)
    // {
    //     if (other.CompareTag("Barrier"))
    //     {
    //         Debug.Log("other: " + other.gameObject.name);
    //         Debug.Log("colliding with barrier");
    //         BarrierControl barrier = other.GetComponent<BarrierControl>();
    //         if (barrier != null)
    //         {
    //             if (animator != null) { animator.Play("zombatk"); }
    //             barrier.DamageBarrier(); // Deal 10 damage to the barrier
    //             Debug.Log("Enemy damaged the barrier!");
    //         }
    //         else
    //         {
    //             Debug.LogError("BarrierControl component not found on the barrier!");
    //         }
    //     }
    // }

    void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Barrier") && !isAttacking)
        {
            barrier_atk_timer += Time.deltaTime;

            if (barrier_atk_timer >= 2.5f)
            {
                isAttacking = true;
                Debug.Log("other: " + other.gameObject.name);
                Debug.Log("staying with barrier");
                BarrierControl barrier = other.GetComponent<BarrierControl>();
                if (barrier != null)
                {
                    if (animator != null) { animator.Play("zombatk"); }
                    barrier.DamageBarrier();
                    Debug.Log("Enemy damaged the barrier!");
                }
                barrier_atk_timer = 0f;
                isAttacking = false;
            }
        }
    }

    

    void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, walker_range);
    }
}
