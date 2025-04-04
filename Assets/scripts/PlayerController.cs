using System;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    Rigidbody rb;
    GameObject weapon;
    Ray player_look;
    RaycastHit contact;
    Camera cam;
    public GameObject bullet;
    
    // Bullet settings
    public float bulletSpeed = 400f;
    public Transform bulletSpawnPoint;
    public float fireRate = 0.1f;
    private float nextFireTime = 0f;
    
    public float mouseSensitivity = 2.0f;
    private float xRotation = 0f;

    float p_max_velocity = 5.0f;
    float p_move_speed = 1.0f;
    
    void Start()
    {
        weapon = FindWeapon();
        
        rb = GetComponent<Rigidbody>();
        cam = Camera.main;
        player_look = new Ray(cam.transform.position, cam.transform.forward);
        
        // If bulletSpawnPoint wasn't assigned, create one at weapon tip
        if (bulletSpawnPoint == null && weapon != null)
        {
            // Create spawn point at end of weapon
            GameObject spawnPointObj = new GameObject("BulletSpawnPoint");
            spawnPointObj.transform.parent = weapon.transform;
            spawnPointObj.transform.localPosition = new Vector3(0, 0, 1); // Adjust based on weapon model
            bulletSpawnPoint = spawnPointObj.transform;
        }
        
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            return;
        }
        player_look.origin = cam.transform.position;
        player_look.direction = cam.transform.forward;
        AdjustWeaponDirection();
        
        // Shoot with rate limiting
        if (Input.GetMouseButton(0) && Time.time >= nextFireTime) // m1
        {
            nextFireTime = Time.time + fireRate; // Set next allowed fire time
            FireBullet();
        }

        Debug.DrawRay(player_look.origin, player_look.direction * 100, Color.red);
        
        Move();
        
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        transform.Rotate(Vector3.up * mouseX);
        
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        cam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    void Move() 
    {
        if (Input.GetKey(KeyCode.W))
        {
            if (rb.linearVelocity.magnitude > p_max_velocity)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * p_max_velocity;
            } else {
                rb.linearVelocity += transform.forward * p_move_speed;
            }
        }
        if (Input.GetKey(KeyCode.S))
        {
            if (rb.linearVelocity.magnitude > p_max_velocity)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * p_max_velocity;
            } else {
                rb.linearVelocity += -transform.forward * p_move_speed;
            }
        }
        if (Input.GetKey(KeyCode.A))
        {
            if (rb.linearVelocity.magnitude > p_max_velocity)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * p_max_velocity;
            } else {
                rb.linearVelocity += -transform.right * p_move_speed;
            }
        }
        if (Input.GetKey(KeyCode.D))
        {
            if (rb.linearVelocity.magnitude > p_max_velocity)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * p_max_velocity;
            } else {
                rb.linearVelocity += transform.right * p_move_speed;
            }
        }
        if (Input.GetKey(KeyCode.Space))
        {
            if (rb.linearVelocity.magnitude > p_max_velocity)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * p_max_velocity;
            } else {
                rb.AddForce(Vector3.up * p_move_speed, ForceMode.VelocityChange);
            }
        }
    }

   void FireBullet()
    {
        
        if (bullet != null)
        {
            // Check if the hit object is an enemy
            bool hit = Physics.Raycast(player_look, out contact, 100);

            // Determine spawn position
            Vector3 spawnPosition = bulletSpawnPoint != null ? 
                bulletSpawnPoint.position : 
                cam.transform.position + cam.transform.forward * 1f;

            // Default direction is forward from camera
            Vector3 direction = cam.transform.forward;
            Vector3 targetPoint;

            if (hit && contact.collider != null)
            {
                // If we hit something, check if it's an enemy
                EnemyController enemy = contact.collider.GetComponent<EnemyController>();
                if (enemy != null)
                {
                    enemy.TakeDamage(10);
                }

                // Use the hit point for direction
                targetPoint = contact.point;
            }
            else
            {
                // No hit, so target a point far in the distance
                targetPoint = cam.transform.position + cam.transform.forward * 100f;
            }

            // Calculate direction from spawn point to target point
            Vector3 directionToTarget = targetPoint - spawnPosition;

            // Create rotation to look at the target point
            Quaternion bulletRotation = Quaternion.LookRotation(directionToTarget);

            // Rotate 90 degrees to align cylinder's long axis with direction of travel
            bulletRotation *= Quaternion.Euler(90, 0, 0);

            // Create bullet with the calculated rotation
            GameObject newBullet = Instantiate(bullet, spawnPosition, bulletRotation);

            // Get or add rigidbody to bullet
            Rigidbody bulletRb = newBullet.GetComponent<Rigidbody>();
            if (bulletRb == null)
            {
                bulletRb = newBullet.AddComponent<Rigidbody>();
            }

            // Configure bullet physics
            bulletRb.constraints = RigidbodyConstraints.FreezeRotation;
            bulletRb.mass = 100.0f;
            bulletRb.useGravity = false;
            bulletRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Apply velocity in direction of hit point
            bulletRb.linearVelocity = directionToTarget.normalized * bulletSpeed;

            Destroy(newBullet, 1f);
        }
        else
        {
            Debug.LogError("Missing bullet prefab or no valid hit point");
        }
    }

    GameObject FindWeapon()
    {
        // Code unchanged
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("w_"))
            {
                Debug.Log("Found weapon: " + child.name);
                return child.gameObject;
            }
        }

        Debug.LogWarning("No weapon with w_ prefix found!");
        return null;
    }

    void AdjustWeaponDirection() 
    {
        if (weapon != null)
        {
            Quaternion weapon_rotation;
            switch (weapon.name)
            {
                case "w_test":
                    weapon_rotation = cam.transform.rotation * Quaternion.Euler(90f, 0, 0);
                    weapon.transform.rotation = weapon_rotation;
                    break;
                case "w_ak":
                    weapon_rotation = cam.transform.rotation * Quaternion.Euler(0, 0, 90f);
                    weapon.transform.rotation = weapon_rotation;
                    // Adjust for AK-47
                    break;
                case "w_m4":
                    // Adjust for M4
                    break;
                default:
                    weapon.transform.rotation = cam.transform.rotation;
                    Debug.LogWarning("Unknown weapon type: " + weapon.name);
                    break;
            }
        }
    }
}