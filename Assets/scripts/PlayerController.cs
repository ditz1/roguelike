using System;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    Rigidbody rb;
    GameObject weapon;
    GameObject external_view_weapon;
    Ray player_look;
    RaycastHit contact;
    Camera cam;
    public GameObject bullet;
   
    float maxPlatformHeight = 0.65f;  
    //float platformCheckDistance = 2.6f;  
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

    //float p_max_velocity = 18.0f;
    float p_move_speed = 8.5f;
    bool isGrounded = false;

    float gravity_scalar = 0.5f;

    GameObject right_hand;
    GameObject left_hand;
    GameObject weapon_rh_pos;
    GameObject weapon_lh_pos;
    GameObject first_person_rh;
    GameObject first_person_lh;
    
    void Start()
    {
        // this is just needed for debugging
        if (Display.displays.Length > 1)
        {
            Display.displays[1].Activate();
        }
        weapon = FindWeapon(); // this finds weapon for first person view
        // make a copy of weapon for external view
        external_view_weapon = Instantiate(weapon, new Vector3(0, 0, 0), weapon.transform.rotation);
        external_view_weapon.transform.parent = GameObject.Find("model").transform;
        MaskObjectToLayer(external_view_weapon, "MaskToPlayer");
        MaskObjectToLayer(weapon, "FirstPersonOnly");

        GameObject weapon_container = weapon.transform.parent.gameObject;
        if (weapon_container != null)
        {
            first_person_lh = FindChildInObject(weapon_container, "playerhand_l");
            first_person_rh = FindChildInObject(weapon_container, "playerhand_r");
        }
        // adjust weapon params based on weapon prefab

        //external_view_weapon.localScale = new Vector3(1, 1, 1);

        rb = GetComponent<Rigidbody>();
        cam = Camera.main;
        player_look = new Ray(cam.transform.position, cam.transform.forward);

        if (weapon != null)
        {
            bulletSpawnPoint = FindChildInObject(weapon, "barrel")?.transform;
            weapon_rh_pos = FindChildInObject(external_view_weapon, "RHgrabpos");
            weapon_lh_pos = FindChildInObject(external_view_weapon, "LHgrabpos");
        }

        right_hand = FindChildInObject(gameObject, "RightHandGrab");
        left_hand = FindChildInObject(gameObject, "LeftHandGrab");

        Debug.Log("Initialization results: " +
                 "weapon=" + (weapon != null) + ", " +
                 "bulletSpawnPoint=" + (bulletSpawnPoint != null) + ", " +
                 "right_hand=" + (right_hand != null) + ", " +
                 "left_hand=" + (left_hand != null) + ", " +
                 "weapon_rh_pos=" + (weapon_rh_pos != null) + ", " +
                 "weapon_lh_pos=" + (weapon_lh_pos != null));

        Cursor.lockState = CursorLockMode.Locked;
    }

    GameObject FindChildInObject(GameObject parent, string name)
    {
        if (parent == null) return null;

        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
                return child.gameObject;
        }
        Debug.LogWarning("Child object with name " + name + " not found in " + parent.name);
        return null;
    }

    void MaskObjectToLayer(GameObject obj, string layerName)
    {
        if (obj == null) return;

        obj.layer = LayerMask.NameToLayer(layerName);
        foreach (Transform child in obj.transform)
        {
            MaskObjectToLayer(child.gameObject, layerName);
        }
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
        AdjustWeaponPosition();
        // only adjust if camera X rotation is > -15 degrees and < 30
        if (NormalizeAngle(cam.transform.localEulerAngles.x) > -15 && NormalizeAngle(cam.transform.localEulerAngles.x) < 30)
        {
             AdjustFirstPersonHandsPosition(); // adjust weapon position for first person view
        }
        
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
        // should be attached to camera
        foreach (Transform child in transform)
        {
            if (child.name == "Main Camera" && child.GetChild(0).name.Contains("weapon"))
            {
                GameObject weapon_container = child.GetChild(0).gameObject;

                if (weapon_container.transform.childCount > 0)
                {
                    Debug.Log("Found weapon: " + weapon_container.transform.GetChild(0).name);
                    return weapon_container.transform.GetChild(0).gameObject;
                }
            }
        }

        Debug.LogWarning("No weapon with w_ prefix found!");
        return null;
    }

    void AdjustWeaponParams(GameObject weapon)
    {
        string weaponName = weapon.name.ToLower();
        weaponName.Replace("w_", "");
        
        if (weapon != null)
        {
            if (weaponName.Contains("ak")){
                weapon.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                weapon.transform.rotation = Quaternion.Euler(0, 0, 90f);
            }    

        }
        else
        {
            Debug.LogWarning("Weapon is null, cannot adjust parameters.");
        }
    }

    void AdjustWeaponPosition()
    {
        if (external_view_weapon != null && right_hand != null && left_hand != null && weapon_rh_pos != null && weapon_lh_pos != null)
        {
            // First, calculate the position to move the weapon to (based on right hand)
            Vector3 positionOffset = right_hand.transform.position - weapon_rh_pos.transform.position;

            // Then calculate the rotation
            // Get vectors representing the directions between hands and grab points
            Vector3 targetHandsDirection = (left_hand.transform.position - right_hand.transform.position).normalized;
            Vector3 weaponGrabsDirection = (weapon_lh_pos.transform.position - weapon_rh_pos.transform.position).normalized;

            // Calculate rotation to align these directions
            Quaternion alignRotation = Quaternion.FromToRotation(weaponGrabsDirection, targetHandsDirection);

            // Apply position and rotation to the weapon
            external_view_weapon.transform.position += positionOffset;
            external_view_weapon.transform.rotation = alignRotation * external_view_weapon.transform.rotation;

            // Add debug logging
        }
        else
        {
            // Add detailed debug output for what's missing
            Debug.LogWarning("Weapon adjustment failed because: " + 
                             (external_view_weapon == null ? "weapon is null; " : "") +
                             (right_hand == null ? "right_hand is null; " : "") +
                             (left_hand == null ? "left_hand is null; " : "") +
                             (weapon_rh_pos == null ? "weapon_rh_pos is null; " : "") +
                             (weapon_lh_pos == null ? "weapon_lh_pos is null; " : ""));
        }
    }

    void AdjustFirstPersonHandsPosition()
    {
        if (weapon != null && first_person_lh != null && first_person_rh != null)
        {
            // Find grab positions on the weapon
            GameObject leftGrabPos = FindChildInObject(weapon, "LHgrabpos");
            GameObject rightGrabPos = FindChildInObject(weapon, "RHgrabpos");

            if (leftGrabPos == null || rightGrabPos == null)
            {
                Debug.LogWarning("Could not find grab positions on weapon");
                return;
            }

            // Find hand and arm transforms
            GameObject leftHand = FindChildInObject(first_person_lh, "hand");
            GameObject leftArm = FindChildInObject(first_person_lh, "arm");
            GameObject rightHand = FindChildInObject(first_person_rh, "hand");
            GameObject rightArm = FindChildInObject(first_person_rh, "arm");

            if (leftHand == null || leftArm == null || rightHand == null || rightArm == null)
            {
                Debug.LogWarning("Could not find hand/arm children objects");
                return;
            }

            // Get player center position
            Vector3 playerCenter = cam.transform.position + Vector3.down * 0.5f;
            // move playercenter a bit behind player
            playerCenter -= transform.forward * 0.2f;
            // and move player center a bit right
            //playerCenter += transform.right * 0.2f;
            Vector3 playerCenterLeft = playerCenter - transform.right * 0.3f;
            Vector3 playerCenterRight = playerCenter + transform.right * 0.2f;

            // Following the same approach as AdjustWeaponPosition:

            // 1. Calculate position offsets (position hands at grab positions)
            Vector3 leftPositionOffset = leftGrabPos.transform.position - leftHand.transform.position;
            Vector3 rightPositionOffset = rightGrabPos.transform.position - rightHand.transform.position;

            // 2. Calculate rotation alignments
            // Get vectors representing the directions between parts
            Vector3 playerToLeftGrab = (leftGrabPos.transform.position - playerCenterLeft).normalized;
            Vector3 playerToRightGrab = (rightGrabPos.transform.position - playerCenterRight).normalized;

            Vector3 armToHand = (leftHand.transform.position - leftArm.transform.position).normalized;
            Vector3 armToHandRight = (rightHand.transform.position - rightArm.transform.position).normalized;

            // Calculate rotation to align these directions
            Quaternion leftAlignRotation = Quaternion.FromToRotation(armToHand, playerToLeftGrab);
            Quaternion rightAlignRotation = Quaternion.FromToRotation(armToHandRight, playerToRightGrab);

            // 3. Apply position and rotation to the hand objects
            first_person_lh.transform.position += leftPositionOffset;
            first_person_rh.transform.position += rightPositionOffset;

            first_person_lh.transform.rotation = leftAlignRotation * first_person_lh.transform.rotation;
            first_person_rh.transform.rotation = rightAlignRotation * first_person_rh.transform.rotation;

            // Draw debug lines to verify alignment
            Debug.DrawLine(playerCenterLeft, leftGrabPos.transform.position, Color.magenta);
            Debug.DrawLine(playerCenterRight, rightGrabPos.transform.position, Color.yellow);
            Debug.DrawLine(leftArm.transform.position, leftHand.transform.position, Color.blue);
            Debug.DrawLine(rightArm.transform.position, rightHand.transform.position, Color.red);
        }
        else
        {
            Debug.LogWarning("First person weapon positioning failed because: " + 
                            (weapon == null ? "weapon is null; " : "") +
                            (first_person_lh == null ? "first_person_lh is null; " : "") +
                            (first_person_rh == null ? "first_person_rh is null; " : ""));
        }
    }

    float NormalizeAngle(float angle) {
        while (angle > 180) angle -= 360;
        while (angle < -180) angle += 360;
        return angle;
    }

    void OnDrawGizmos()
    {
        if (right_hand != null && left_hand != null && weapon_rh_pos != null && weapon_lh_pos != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(right_hand.transform.position, left_hand.transform.position);

            Gizmos.color = Color.red;
            Gizmos.DrawLine(weapon_rh_pos.transform.position, weapon_lh_pos.transform.position);
        }
    }
}