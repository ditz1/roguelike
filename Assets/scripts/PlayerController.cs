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
   
    public float maxPlatformHeight = 0.5f;  
    public float platformCheckDistance = 0.8f;  
    public float climbSpeed = 5.0f; 

    private bool isClimbing = false;
    private Vector3 climbTargetPosition;
    
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
            if (!isClimbing){
                CheckForPlatform();
            }
            if (isClimbing)
            {
                ClimbPlatform();
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

    
    void MoveUpPlatform()
    {
        // checks right in front of the player and if the object's height
        // is less than some value, move the player up
        RaycastHit hit;
        // get player position and direction
        player_look.origin = cam.transform.position;
        player_look.direction = cam.transform.forward;
        // get a position from slightly forward in the direction the player is looking
        Vector3 forward = cam.transform.position + cam.transform.forward * 0.5f;
        // shoot raycast straight down from the forward position
        if (Physics.Raycast(forward, Vector3.down, out hit, 1.0f))
        {
            // check if the object is a platform and if the height is less than some value
            if (hit.distance < 1.0f)
            {
                // move the player up to the platform's height
                transform.position = new Vector3(transform.position.x, transform.position.y + 0.2f, transform.position.z);
            }
        }

        // draw raycast for debugging
        Debug.DrawRay(forward, Vector3.down * 1.0f, Color.green);
    }
   
    void CheckForPlatform()
    {
        // Create ray starting position (at player's feet)
        Vector3 rayStart = transform.position;
        // Use the collider's height if available, otherwise estimate based on renderer bounds
        float playerHeight = 2.0f; // Default height estimate
        
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            playerHeight = col.bounds.size.y;
        }
        else
        {
            Renderer rend = GetComponent<Renderer>();
            if (rend != null)
            {
                playerHeight = rend.bounds.size.y;
            }
        }
        
        rayStart.y -= playerHeight/2 - 0.1f;  // Offset to feet position

        // Direction to check (forward)
        Vector3 forwardDir = transform.forward;

        // Position to check for platform (slightly ahead of player)
        Vector3 forwardPos = rayStart + forwardDir * platformCheckDistance;

        // Draw debug rays
        Debug.DrawLine(rayStart, forwardPos, Color.blue, 0.1f);
        Debug.DrawRay(forwardPos, Vector3.down * maxPlatformHeight * 2f, Color.red, 0.1f);
        Debug.DrawRay(forwardPos, Vector3.up * maxPlatformHeight, Color.green, 0.1f);

        RaycastHit downHit;
        RaycastHit upHit;

        // First check if there's something above our forward position
        // that would block the player from moving forward
        if (Physics.Raycast(forwardPos, Vector3.up, out upHit, maxPlatformHeight * 2f))
        {
            // Something is in the way above, can't climb
            return;
        }

        // Check downward from the forward position to find platform
        if (Physics.Raycast(forwardPos, Vector3.down, out downHit, maxPlatformHeight * 2f))
        {
            float heightDifference = downHit.point.y - rayStart.y;

            // If platform is higher than current position but within climb range
            if (heightDifference > 0 && heightDifference <= maxPlatformHeight)
            {
                // Platform detected! Set target position on top of platform
                climbTargetPosition = new Vector3(
                    forwardPos.x,
                    downHit.point.y + playerHeight/2,
                    forwardPos.z
                );

                isClimbing = true;
                Debug.Log("Platform detected! Height difference: " + heightDifference);

                // Disable physics while climbing
                rb.isKinematic = true;
            }
        }
    }

    void ClimbPlatform()
    {
        // Move player smoothly to the target position
        transform.position = Vector3.Lerp(
            transform.position,
            climbTargetPosition,
            Time.deltaTime * climbSpeed
        );

        // Check if we've reached the target position
        if (Vector3.Distance(transform.position, climbTargetPosition) < 0.05f)
        {
            isClimbing = false;
            
            // Re-enable physics
            rb.isKinematic = false;

            Debug.Log("Platform climb complete!");
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