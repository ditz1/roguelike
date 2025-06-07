using UnityEngine;
using UnityEngine.UI;

public class Shrine : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    Transform player;
    [SerializeField] Image hp_img;
    [SerializeField] Transform shrine_transform;
    int shrine_hp = 100; // Example initial health value for the shrine

    // Update is called once per frame
    void Start()
    {
    }
    void Update()
    {
        if (hp_img == null)
        {
            Debug.LogError("Health bar image not assigned!");
        }
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player").transform; // Find the player object by tag
        }

        if (player != null)
        {
            Vector3 direction = player.position - transform.position;
            Quaternion rotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * 5f); // Smoothly rotate towards the player
        }

        float shrine_hp_percentage = shrine_hp / 100.0f; // Calculate the health percentage
        hp_img.transform.localScale = new Vector3(shrine_hp_percentage, 1.0f, 1.0f);

        shrine_transform.Rotate(0, 2.0f * Time.deltaTime * 3.0f, 0); // Rotate the shrine continuously

    }

    public void DamageShrine(int damage)
    {
        shrine_hp -= damage; // Reduce shrine health by the damage amount
        if (shrine_hp <= 0)
        {
            shrine_hp = 0; // Ensure health doesn't go below zero
            Destroy(gameObject); // Destroy the shrine when health reaches zero
        }
    }
}
