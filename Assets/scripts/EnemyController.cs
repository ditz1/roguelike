using UnityEngine;
using UnityEngine.UI;

public class EnemyController : MonoBehaviour
{
    int e_health = 200;
    Color e_color = Color.blue;
    private Renderer rend; // Reference to the renderer component
    public Image hp_img;
    public Transform player;

    void Start()
    {
        // Get the renderer component
        player = GameObject.FindGameObjectWithTag("Player").transform; // Find the player object by tag
        if (player == null)
        {
            Debug.LogError("Player not found! Make sure the player has the 'Player' tag.");
        }
        rend = GetComponent<Renderer>();
        if (rend == null)
        {
            Debug.LogError("No Renderer component found on this GameObject!");
        }

        if (hp_img == null)
        {
            Debug.LogError("Health bar image not assigned! Please assign it in the inspector.");
        }

        hp_img.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f); // Set the initial scale of the health bar image
        
        // Initialize color
    }

    void Update()
    {
        UpdateEnemy(); // Call the enemy update method
        UpdateHealthBar();
        UpdateEnemyRotation(); // Call the rotation update method
    }

    void UpdateEnemy()
    {
        if (e_health <= 0)
        {
            // wait 2 seconds before destroying the enemy
            // get the direction enemy is currently facing and then rotate it 90 degrees
            // this is a bit hacky but it works for now
            Vector3 direction = transform.forward; // Get the current forward direction of the enemy
            Quaternion rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 0, 180); // Rotate 90 degrees
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * 5f); // Smoothly rotate to the new direction
            rend.material.color = Color.red; // Change color to red
            Destroy(gameObject, 1f); // Uncomment this line if you want to destroy the enemy after 2 seconds
        }
    }

    // update enemy to face towards player
    void UpdateEnemyRotation()
    {
        if (player != null)
        {
            Vector3 direction = player.position - transform.position;
            Quaternion rotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * 5f); // Smoothly rotate towards the player
        }
    }

    void UpdateHealthBar()
    {
        // Update health bar logic here if needed
        float hp_as_scale = (float)e_health / 200f;
        hp_img.transform.localScale = new Vector3(hp_as_scale, 1.0f, 1.0f); // Update the scale of the health bar image
        
    }

    // this is called every frame, dont need to put in update
    public void TakeDamage(int damage)
    {
        // Handle taking damage
        if (e_health <= damage)
        {
            e_health = 0; // Prevent negative health
        }
        else
        {
            e_health -= damage;
        }
        
        // No need to call UpdateColor() here as it's called every frame in Update()
        Debug.Log("Enemy took damage: " + damage + ", Health: " + e_health);
    }
}