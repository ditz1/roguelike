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
   
    float maxPlatformHeight = 0.65f;  
    float platformCheckDistance = 2.6f;  
    public float climbSpeed = 10.0f; 

    private bool isClimbing = false;
    private Vector3 climbTargetPosition;
    
    // Bullet settings
    public float bulletSpeed = 400f;
    public Transform bulletSpawnPoint;
    public float fireRate = 0.1f;
    private float nextFireTime = 0f;
    
    public float mouseSensitivity = 2.0f;
    private float xRotation = 0f;

    float p_max_velocity = 18.0f;
    float p_move_speed = 8.5f;
    bool isGrounded = false;

    float gravity_scalar = 0.5f;
    
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
        // Always check for ground
        RaycastHit hit;
        isGrounded = Physics.Raycast(rb.transform.position, Vector3.down, out hit, 1.1f);

        Debug.DrawRay(rb.transform.position, Vector3.down * 1.1f, Color.green);


        // increase gravity effect on player        
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
        rb.AddForce(Physics.gravity * gravity_scalar, ForceMode.Acceleration);

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        transform.Rotate(Vector3.up * mouseX);
        
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        cam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    void Move() 
    {
        float jump_force = 10.0f;

        // --- Horizontal movement ---
        Vector3 inputDir = Vector3.zero;

        if (Input.GetKey(KeyCode.W)) inputDir += transform.forward;
        if (Input.GetKey(KeyCode.S)) inputDir -= transform.forward;
        if (Input.GetKey(KeyCode.A)) inputDir -= transform.right;
        if (Input.GetKey(KeyCode.D)) inputDir += transform.right;

        Vector3 velocity = rb.linearVelocity;

        // Apply horizontal movement
        if (inputDir != Vector3.zero)
        {
            inputDir.Normalize();
            Vector3 targetVelocity = inputDir * p_move_speed;

            // Preserve vertical velocity
            velocity.x = targetVelocity.x;
            velocity.z = targetVelocity.z;
        }
        else
        {
            // Apply friction when no input, preserve Y
            velocity.x = Mathf.Lerp(velocity.x, 0, Time.deltaTime * 5f);
            velocity.z = Mathf.Lerp(velocity.z, 0, Time.deltaTime * 5f);
        }

        // Apply jump
        if (Input.GetKey(KeyCode.Space) && isGrounded)
        {
            Debug.Log("Jumping!");
            velocity.y = jump_force;  // replace Y velocity instead of adding force
            isGrounded = false;
        }

        // Assign modified velocity back to Rigidbody
        rb.linearVelocity = velocity;

        // Platform climbing logic
        if (!isClimbing)
        {
            CheckForPlatform();
        }
        if (isClimbing)
        {
            ClimbPlatform();
        }
    }
   
    void CheckForPlatform()
    {
        // Only check when moving
        if (!Input.GetKey(KeyCode.W) && !Input.GetKey(KeyCode.S) && 
            !Input.GetKey(KeyCode.A) && !Input.GetKey(KeyCode.D))
            return;

        // Get player height
        float playerHeight = 1.0f;
        Collider col = GetComponent<Collider>();
        if (col != null) playerHeight = col.bounds.size.y;

        // Get movement direction
        Vector3 moveDir = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) moveDir += transform.forward;
        if (Input.GetKey(KeyCode.S)) moveDir -= transform.forward;
        if (Input.GetKey(KeyCode.A)) moveDir -= transform.right;
        if (Input.GetKey(KeyCode.D)) moveDir += transform.right;

        // Position in front of player
        Vector3 forwardPos = transform.position + moveDir * 1.0f;
        forwardPos.y -= 0.3f; // some offset;

        // Simple raycast down from that position
        RaycastHit hit;
        if (Physics.Raycast(forwardPos, Vector3.down, out hit, 0.5f))
        {
            // If we hit something, climb it
            climbTargetPosition = new Vector3(
                forwardPos.x, 
                hit.point.y + playerHeight/2,
                forwardPos.z
            );
            float distance_to_target = Vector3.Distance(forwardPos, hit.point);
            Debug.Log("Distance to target: " + distance_to_target);
            if (distance_to_target < maxPlatformHeight)
            {
                // Move player up to the platform's height
                transform.position = Vector3.MoveTowards(transform.position, climbTargetPosition, Time.deltaTime * climbSpeed);
                isClimbing = true;
            }

            isClimbing = true;
        }

        // Debug ray
        Debug.DrawRay(forwardPos, Vector3.down, Color.yellow, 0.1f);
    }

    void ClimbPlatform()
    {        
        // dont bother with smoothing, just move them up
        transform.position = new Vector3(transform.position.x, transform.position.y + 0.2f, transform.position.z);
        isClimbing = false;    
        Debug.Log("Platform climb complete!");
        
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