using System;
using Unity.VisualScripting;
using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class PlayerController : NetworkBehaviour
{
    Rigidbody rb;
    public GameObject weapon;

    public string player_name = "DitzTest";
    GameObject weapon_container;
    GameObject external_view_weapon;
    Ray player_look;
    RaycastHit contact;
    Camera cam;
    public GameObject bullet;

    [SerializeField] List<GameObject> stuctures; 

    ParticleSystem muzzleFlash;
    NetworkManager networkManager;
   
    float maxPlatformHeight = 0.25f;  
    //float platformCheckDistance = 2.6f;  
    float climbSpeed = 30.0f; 

    private bool isClimbing = false;
    private Vector3 climbTargetPosition;
    
    // Bullet settings
    public float bulletSpeed = 20000f;
    public Transform bulletSpawnPoint;
    public float fireRate = 0.1f;
    private float nextFireTime = 0f;
    
    public float mouseSensitivity = 2.0f;
    private float xRotation = 0f;

    bool isReloading = false;

    //float p_max_velocity = 18.0f;
    float p_move_speed = 8.5f;
    bool isGrounded = false;

    float gravity_scalar = 0.5f;

    Quaternion default_weapon_rotation = Quaternion.Euler(0, 0, 0);

    bool mag_in_place = true; // specifically for the animation of reloading

    float sway_ticker = 0.0f; 

    GameObject right_hand;
    GameObject left_hand;
    GameObject weapon_rh_pos;
    GameObject weapon_lh_pos;
    GameObject first_person_rh;
    GameObject first_person_lh;

    Transform default_rh_transform;
    Transform default_lh_transform;

    bool weapon_equipped = false;

    // this is pretty terrible, should really have a weapon object and then have .Shoot() but 
    // for now i guess we can do this, isn't really the main focus of the game

    public int ammo_reserve = 0; // total ammo player has not in gun
    public int curr_ammo_in_mag = 0; // current ammo in magazine
    public int mag_capacity = 0; // how much the mag can hold on reload

    public int player_hp = 100;

    public int player_cash = 0;

    bool build_mode = false;

    int selected_stucture = 0;



    public enum PlayerMoveState
    {
        IDLE,
        RUN_FORWARD,
        RUN_BACKWARD,
        RUN_LEFT,
        RUN_RIGHT,
        JUMP,
    }

    public PlayerMoveState playerMoveState = PlayerMoveState.IDLE;

    private GameObject weaponMagazine;
    private Vector3 magazineOriginalPosition;
    private Quaternion magazineOriginalRotation;
    private float reloadTime = 0.5f; // Time in seconds for reload animation
    private float reloadTimer = 0f;
    private enum ReloadState { 
        NONE, 
        GRAB_MAG, 
        WAIT, 
        RETURN_MAG 
    }
    private ReloadState currentReloadState = ReloadState.NONE;
    
    void Start()
    {
                
        // this is just needed for debugging
        if (Display.displays.Length > 1)
        {
            Display.displays[1].Activate();
        }
        
        // adjust weapon params based on weapon prefab
        // try to find a weapon if player already has one
        weapon = FindWeapon();
        if (weapon != null)
        {
            LoadWeapon(); // load the weapon and assign it to the player
        } else {
            // call find weapon anyway, will fail, but will at least set weapon_container
            FindWeapon();
            AssignDefaultViewmodel();
        }

        

        //external_view_weapon.localScale = new Vector3(1, 1, 1);

        rb = GetComponent<Rigidbody>();
        cam = GetComponentInChildren<Camera>();
        player_look = new Ray(cam.transform.position, cam.transform.forward);

        Cursor.lockState = CursorLockMode.Locked;
        if (!IsOwner){
            cam.enabled = false;
        }
    }


    // this is basically start method
    public override void OnNetworkSpawn()
    {
        networkManager = NetworkManager.Singleton;
        rb = GetComponent<Rigidbody>();
        cam = GetComponentInChildren<Camera>();
        player_look = new Ray(cam.transform.position, cam.transform.forward);
        Cursor.lockState = CursorLockMode.Locked;

        // Find your model GameObject
        GameObject playerModel = null;
        foreach (Transform child in transform) {
            if (child.name == "model") {
                playerModel = child.gameObject;
                break;
            }
        }

        foreach(Transform child in transform) {
            if (child.name == "Main Camera") {
                foreach (Transform nested_child in child){
                    if (nested_child.name == "weapon") {
                        weapon = nested_child.gameObject;
                        break;
                    }
                }
            }
        }

        if (playerModel == null) {
            Debug.LogError("Player model not found! Make sure your player has a child named 'model'");
        }
        else {
            // Apply different layers based on ownership
            if (IsOwner) {
                Debug.Log("This is local player - masking model to MaskToPlayer layer");
                MaskObjectToLayer(playerModel, "MaskToPlayer");
                MaskObjectToLayer(weapon, "FirstPersonOnly"); // this may break when testing back with multiplayer
            } else {
                Debug.Log("This is remote player - keeping model on Default layer");
                MaskObjectToLayer(playerModel, "Default");
                MaskObjectToLayer(weapon, "Ignore");
            }
        }

        // Load weapon after applying layers
        if (!IsOwner) {
            cam.enabled = false;
            LoadWeapon();
        } else {
            weapon = FindWeapon();
            if (weapon != null) {
                LoadWeapon();
            } else {
                FindWeapon();
                AssignDefaultViewmodel();
            }
        }
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
        if (obj == null) 
        {
            Debug.LogWarning("Object is null, cannot mask to layer " + layerName);
            return;
        }

        // Check if the layer exists
        int layerIndex = LayerMask.NameToLayer(layerName);
        if (layerIndex == -1)
        {
            Debug.LogError("Layer '" + layerName + "' does not exist! Did you add it in Project Settings?");
            return;
        }

        //Debug.Log("Masking object " + obj.name + " to layer " + layerName + " (index: " + layerIndex + ")");
        obj.layer = layerIndex;

        foreach (Transform child in obj.transform)
        {
            MaskObjectToLayer(child.gameObject, layerName);
        }
    }

    void AssignDefaultViewmodel()
    {
        // if no weapon, just show fists
        if (weapon == null)
        {
            Debug.LogWarning("No weapon found, using fists as default viewmodel.");
            return;
        }
        
        // MUST BE LOCAL POSITION
        // assign initial offset inside weapon container for different weapons
        Debug.Log("weapon name: " + weapon.name);
        build_mode = false;
        if (weapon.name.Contains("ak"))
        {
            weapon.transform.localPosition = new Vector3(0.021f, -0.04f, 0.0f);
        }
        else if (weapon.name.Contains("m4a1"))
        {
            weapon.transform.localPosition = new Vector3(0.08f, 0f, -0.06f);
        }
        else if (weapon.name.Contains("vector"))
        {
            weapon.transform.localPosition = new Vector3(-0.04f, 0.0f, -0.8f);
        }
        else if (weapon.name.Contains("build"))
        {
            build_mode = true;
            weapon.transform.localPosition = new Vector3(0.0f, 0.0f, -0.5f);
        }
    }


    void Update()
    {
        if (!IsOwner) {

            return;
        }
        
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

        if (weapon_equipped)
        {
            AdjustWeaponPosition();
            WeaponSway(); // sway weapon based on player movement
            // only adjust if camera X rotation is > -15 degrees and < 30
            if (NormalizeAngle(cam.transform.localEulerAngles.x) > -15 && NormalizeAngle(cam.transform.localEulerAngles.x) < 30)
            {
                if (!isReloading)
                {
                    AdjustFirstPersonHandsPosition(); // adjust weapon position for first person view
                }
            }

            // Shoot with rate limiting
            if (Input.GetMouseButton(0) && Time.time >= nextFireTime) // m1
            {
                if (build_mode)
                {
                    PlaceStructure();
                    
                }
                else if (!isReloading && curr_ammo_in_mag > 0)
                {
                    nextFireTime = Time.time + fireRate; // Set next allowed fire time
                    FireBullet();
                }
            }

            if (Input.GetKeyDown(KeyCode.R) && !isReloading && !build_mode)
            {
                isReloading = true;
            }

            if (isReloading)
            {
                ReloadWeapon();
            }
        }
        else
        {
            SwayFists();
        }

        Debug.DrawRay(player_look.origin, player_look.direction * 100, Color.red);
        
        Move();
        Actions();
        rb.AddForce(Physics.gravity * gravity_scalar, ForceMode.Acceleration);

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        transform.Rotate(Vector3.up * mouseX);
        
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        cam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    void PlaceStructure()
    {
        // raycast from camera to find a valid position
        RaycastHit hit;
        // TODO:
    }

    void Move() 
    {
        float jump_force = 10.0f;

        Vector3 inputDir = Vector3.zero;
        playerMoveState = PlayerMoveState.IDLE; // reset state to idle

        if (Input.GetKey(KeyCode.W))
        {
            playerMoveState = PlayerMoveState.RUN_FORWARD;
            inputDir += transform.forward;
        }
        
        if (Input.GetKey(KeyCode.S)) {
            playerMoveState = PlayerMoveState.RUN_BACKWARD;
            inputDir -= transform.forward;
        }
        
        if (Input.GetKey(KeyCode.A)){ 
            playerMoveState = PlayerMoveState.RUN_LEFT;
            // just set z rotation directly
            inputDir -= transform.right;
        }
        
        if (Input.GetKey(KeyCode.D)){ 
            playerMoveState = PlayerMoveState.RUN_RIGHT;
            inputDir += transform.right; 
        }

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
            playerMoveState = PlayerMoveState.IDLE;
            // Apply friction when no input, preserve Y
            velocity.x = Mathf.Lerp(velocity.x, 0, Time.deltaTime * 5f);
            velocity.z = Mathf.Lerp(velocity.z, 0, Time.deltaTime * 5f);
        }

        // Apply jump
        if (Input.GetKey(KeyCode.Space) && isGrounded)
        {
            velocity.y = jump_force;  // replace Y velocity instead of adding force
            isGrounded = false;
        }

        // Assign modified velocity back to Rigidbody
        rb.linearVelocity = velocity;

        // Platform climbing logic
        if (!isClimbing && isGrounded)
        {
            CheckForPlatform();
        }
        if (isClimbing && isGrounded)
        {
            ClimbPlatform();
        }
    }

    void SwayFists()
    {

        if (first_person_lh == null || first_person_rh == null)
        {
            // if these are null we have to find the fists, will be in weapon container
            GameObject cameraobj = FindChildInObject(this.gameObject, "Main Camera");
            GameObject weaponContainer = FindChildInObject(cameraobj, "weapon");
            weapon_container = weaponContainer;
            default_weapon_rotation = weapon_container.transform.localRotation;
        }
        
         
        // tilt fists up and down when moving slighting
        if (playerMoveState != PlayerMoveState.IDLE)
        {
            sway_ticker += Time.deltaTime;
            float swayAmount = Mathf.Sin(sway_ticker * 3f) * 2f; // 3f controls speed, 2f controls intensity
            weapon_container.transform.localRotation = Quaternion.Euler(swayAmount, -0.5f, 1.5f);
        }
        else
        {
            // Smoothly return to default when idle
            weapon_container.transform.localRotation = Quaternion.Lerp(
                weapon_container.transform.localRotation,
                default_weapon_rotation,
                Time.deltaTime * 5f
            );
        }
    }

    void Actions()
    {
        // swap fists if no weapon
        //if (weapon == null)
        //{
        //    first_person_lh.transform.position += new Vector3(0, 0.1f, 0); // move left hand up
        //    first_person_rh.transform.position += new Vector3(0, 0.1f, 0); // move right hand up
        //}
        
        if (Input.GetKeyDown(KeyCode.F)) // m1
        {
            PickUpWeapon();
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
        forwardPos.y += playerHeight / 2; // Adjust height to player's center

        // Simple raycast down from that position
        RaycastHit hit;
        if (Physics.Raycast(forwardPos, Vector3.down, out hit, 0.7f))
        {
            float distance_to_target = Vector3.Distance(forwardPos, hit.point);
            // If we hit something, climb it
            climbTargetPosition = new Vector3(
                forwardPos.x, 
                hit.point.y + 0.6f, // just a flat offset
                forwardPos.z
            );
            Debug.Log("Distance to target: " + distance_to_target);
            // shorter distance to target means higher platform
            if (distance_to_target > maxPlatformHeight)
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

    void PickUpWeapon()
    {
        // first shoot ray forward from camera to check if we hit a collider of a weapon
        if (weapon_container == null)
        {
            Debug.LogWarning("Weapon container not found, cannot pick up weapon.");
            return;
        }
        RaycastHit hit;
        if (Physics.Raycast(player_look, out hit, 100))
        {
            GameObject hitObject = hit.collider.gameObject;

            if (hitObject.name.Contains("w_"))
            {
                Debug.Log("Hit weapon: " + hitObject.name);
                GameObject newWeapon = Instantiate(hitObject, weapon_container.transform.position, weapon_container.transform.rotation);
                newWeapon.transform.SetParent(weapon_container.transform, false);
                if (weapon != null)
                {
                    Destroy(weapon); // destroy old weapon
                    Destroy(external_view_weapon); // destroy old external view weapon
                }
                weapon = newWeapon;
                weapon.transform.localPosition = new Vector3(0, 0, 0);
                weapon.transform.localRotation = Quaternion.Euler(0, 0, 0);
                weapon.transform.localScale = new Vector3(1, 1, 1);
                Debug.Log("Created new weapon: " + newWeapon.name + " at position " + newWeapon.transform.position);
                LoadWeapon();
            }
        }
    }

    void LoadWeapon()
    {
        AssignDefaultViewmodel(); // assign initial offset inside weapon container for different weapons
        GameObject localModel = null;
        foreach (Transform child in transform)
        {
            if (child.name == "model")
            {
                localModel = child.gameObject;
                break;
            }
        }


        if (localModel == null)
        {
            Debug.LogWarning("Can't cull local model correctly!");
            return;
        }



        // make a copy of weapon for external view
        external_view_weapon = Instantiate(weapon, new Vector3(0, 0, 0), weapon.transform.rotation);
        external_view_weapon.transform.parent = GameObject.Find("model").transform;

        if (IsOwner)
        {
            MaskObjectToLayer(localModel, "MaskToPlayer");
            MaskObjectToLayer(external_view_weapon, "MaskToPlayer");
            MaskObjectToLayer(weapon, "FirstPersonOnly");
        }
        else
        {
            MaskObjectToLayer(localModel, "Default");
            MaskObjectToLayer(external_view_weapon, "Default");
            MaskObjectToLayer(weapon, "Ignore");
        }

        if (weapon == null)
        {
            Debug.LogWarning("No weapon found, cannot load weapon.");
            return;
        }


        weapon_container = weapon.transform.parent.gameObject;
        if (weapon_container != null)
        {
            first_person_lh = FindChildInObject(weapon_container, "playerhand_l");
            first_person_rh = FindChildInObject(weapon_container, "playerhand_r");
        }

        bulletSpawnPoint = FindChildInObject(weapon, "barrel")?.transform;

        muzzleFlash = FindChildInObject(weapon, "muzzleflash")?.GetComponent<ParticleSystem>();
        if (muzzleFlash == null)
        {
            Debug.LogWarning("Muzzle flash not found on weapon!");
        }

        weapon_rh_pos = FindChildInObject(external_view_weapon, "RHgrabpos");
        weapon_lh_pos = FindChildInObject(external_view_weapon, "LHgrabpos");
        weaponMagazine = FindChildInObject(weapon, "mag");
        if (weaponMagazine != null)
        {
            magazineOriginalPosition = weaponMagazine.transform.localPosition;
            magazineOriginalRotation = weaponMagazine.transform.localRotation;
        }
        else
        {
            Debug.LogWarning("Weapon magazine not found!");
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

        weapon_equipped = true;
        // set weapon to be a child of the weapon container
        weapon.transform.parent = weapon_container.transform;

        if (weapon.name.Contains("ak"))
        {
            mag_capacity = 30;
            ammo_reserve = 90;
            fireRate = 0.1f;
        }
        else if (weapon.name.Contains("m4a1"))
        {
            mag_capacity = 30;
            ammo_reserve = 90;
            fireRate = 0.075f;
        }
        else if (weapon.name.Contains("vector"))
        {
            mag_capacity = 25;
            ammo_reserve = 75;
            fireRate = 0.04f;
        }

        curr_ammo_in_mag = mag_capacity;

    }
    void WeaponRecoil()
    {
        float recoilAmount = 0.5f; // defaults 
        float recoilSpeed = 12f;

        // heavier firing should have high recoil_amt, lower recoil_speed
        // lighter firing should have low recoil_amt, higher recoil_speed
        if (weapon.name.Contains("ak"))
        {
            recoilAmount = 1.2f;
            recoilSpeed = 6f;
        }
        else if (weapon.name.Contains("m4a1"))
        {
            recoilAmount = 0.3f;
            recoilSpeed = 10f;
        }
        else if (weapon.name.Contains("vector"))
        {
            recoilAmount = 0.2f;
            recoilSpeed = 15f;
        }

        weapon.transform.localPosition = Vector3.Lerp(weapon.transform.localPosition, new Vector3(0, 0, -1f * recoilAmount), Time.deltaTime * recoilSpeed);
    }

    void ReloadWeapon()
    {

        if (weaponMagazine == null)
        {
            isReloading = false;
            Debug.LogWarning("Cannot reload: magazine not found");
            return;
        }

        // Find the grab position on the magazine
        GameObject magazineGrabPos = FindChildInObject(weaponMagazine, "grab");
        if (magazineGrabPos == null)
        {
            isReloading = false;
            Debug.LogWarning("Cannot reload: magazine grab position not found");
            return;
        }

        // Simple state machine for reload animation
        switch (currentReloadState)
        {
            case ReloadState.NONE:
                // Start the reload sequence
                
                currentReloadState = ReloadState.GRAB_MAG;
                break;

            case ReloadState.GRAB_MAG:
                if (weapon_container != null)
                {                    
                    // lerp the rotation
                    weapon_container.transform.localRotation = Quaternion.Lerp(
                        weapon_container.transform.localRotation,
                        Quaternion.Euler(0, 0, -15),
                        Time.deltaTime * 10f
                    );
                }
                // Move left hand to magazine grab position
                if (first_person_lh != null)
                {
                    // Position the hand at the magazine grab position
                    GameObject leftHand = FindChildInObject(first_person_lh, "hand");
                    if (leftHand != null)
                    {
                        // Calculate position offset for hand
                        Vector3 handOffset = magazineGrabPos.transform.position - leftHand.transform.position;
                        first_person_lh.transform.position += handOffset * Time.deltaTime * 10f;

                        // If hand is close enough to grab position
                        if (Vector3.Distance(leftHand.transform.position, magazineGrabPos.transform.position) < 0.05f)
                        {                            
                            // Move to next state
                            currentReloadState = ReloadState.WAIT;
                            reloadTimer = 0f;
                        }
                    }
                }
                break;

            case ReloadState.WAIT:
                // Wait for specified time
                // during wait, move hand even further down and magazine down
                reloadTimer += Time.deltaTime;
                if (reloadTimer >= reloadTime)
                {
                    currentReloadState = ReloadState.RETURN_MAG;
                    mag_in_place = false;
                } else {
                    Vector3 magMovePos = weaponMagazine.transform.localPosition;
                    
                    // bandaid fix for now
                    if (weapon.name.Contains("ak")){
                        magMovePos.x -= 0.5f;
                    } else {
                        magMovePos.z += 0.5f;
                    }
                    
                    weaponMagazine.transform.localPosition = Vector3.Lerp(
                        weaponMagazine.transform.localPosition,
                        magMovePos,
                        Time.deltaTime * 10f
                    );
                    // have the hand follow the magazine
                    Vector3 handMovePos = first_person_lh.transform.localPosition;
                    handMovePos.y -= 0.1f;
                    first_person_lh.transform.localPosition = Vector3.Lerp(
                        first_person_lh.transform.localPosition,
                        handMovePos,
                        Time.deltaTime * 10f
                    );
                }
                break;

            case ReloadState.RETURN_MAG:
                if (!mag_in_place)
                {
                    // Return magazine to original position
                    weaponMagazine.transform.localPosition = Vector3.Lerp(
                        weaponMagazine.transform.localPosition,
                        magazineOriginalPosition,
                        Time.deltaTime * 10f
                    );

                    Vector3 handMovePos1 = first_person_lh.transform.localPosition;
                    handMovePos1.y += 0.2f;
                    first_person_lh.transform.localPosition = Vector3.Lerp(
                        first_person_lh.transform.localPosition,
                        handMovePos1,
                        Time.deltaTime * 10f
                    );
                }
                // If magazine is close enough to original position
                if (Vector3.Distance(weaponMagazine.transform.localPosition, magazineOriginalPosition) < 0.1f)
                {
                    mag_in_place = true;

                    if (ammo_reserve > 0)
                    {
                        int ammo_needed = mag_capacity - curr_ammo_in_mag; 
                        int ammo_to_take = Mathf.Min(ammo_needed, ammo_reserve);

                        curr_ammo_in_mag += ammo_to_take;
                        ammo_reserve -= ammo_to_take;
                    }

                    weaponMagazine.transform.localPosition = magazineOriginalPosition;
                    weaponMagazine.transform.localRotation = magazineOriginalRotation;
                }
                if (weapon_container != null && mag_in_place)
                {
                    // rotate by 10 degrees on z axis
                    weapon_container.transform.localRotation = Quaternion.Lerp(
                        weapon_container.transform.localRotation,
                        Quaternion.Euler(0, 0, 0),
                        Time.deltaTime * 10f
                    );
                    // Reset everything
                    if (weapon_container.transform.localRotation == Quaternion.Euler(0, 0, 0))
                    {
                        currentReloadState = ReloadState.NONE;
                        isReloading = false;
                    }                    
                }
                break;
        }
    }


    void FireBullet()
    {
        WeaponRecoil();

        if (bullet != null)
        {
            // Capture firing data at the moment of shooting
            Vector3 fireOrigin = cam.transform.position;
            Vector3 fireDirection = cam.transform.forward;
            Vector3 startPoint = bulletSpawnPoint != null ? 
                bulletSpawnPoint.position : 
                fireOrigin + fireDirection * 1f;

            // Get the exact end point from raycast
            Vector3 endPoint;
            bool hit = Physics.Raycast(fireOrigin, fireDirection, out contact, 100);
            muzzleFlash?.Play(); // Play muzzle flash effect if available
            curr_ammo_in_mag--;
            

            if (hit && contact.collider != null)
            {
                // If we hit something, check if it's an enemy
                EnemyController enemy = contact.collider.GetComponent<EnemyController>();
                if (enemy != null)
                {
                    enemy.TakeDamage(20);
                    // also give cash to player for hitting enemy
                    player_cash += 10;
                }
                endPoint = contact.point;
            }
            else
            {
                // No hit, use a point far along the raycast line
                endPoint = fireOrigin + fireDirection * 100f;
            }

            // Create bullet with NO rigidbody
            GameObject newBullet = Instantiate(bullet);
            newBullet.transform.position = startPoint;
            newBullet.transform.SetParent(null);

            // Remove any rigidbody if it exists
            Rigidbody existingRb = newBullet.GetComponent<Rigidbody>();
            if (existingRb != null)
            {
                Destroy(existingRb);
            }

            newBullet.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);

            // Add the linear bullet movement script
            LinearBulletMovement bulletMovement = newBullet.AddComponent<LinearBulletMovement>();
            bulletMovement.Initialize(startPoint, endPoint, bulletSpeed);

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
                weapon_container = child.GetChild(0).gameObject;

                if (weapon_container.transform.childCount > 0)
                {
                    for (int i = 0; i < weapon_container.transform.childCount; i++)
                    {
                        if (weapon_container.transform.GetChild(i).name.StartsWith("w_"))
                        {
                            Debug.Log("Found weapon: " + weapon_container.transform.GetChild(i).name);
                            return weapon_container.transform.GetChild(i).gameObject;
                        }
                    }
                }
            }
        }

        Debug.LogWarning("No weapon with w_ prefix found!");
        return null;
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

    public void DamagePlayer(int damage)
    {
        player_hp -= damage;
        if (player_hp <= 0)
        {
            player_hp = 0;
        }
        Debug.Log("Player took " + damage + " damage!");
    }

    void WeaponSway()
    {
        // make sure bulletspawnpoint does not move

        if (playerMoveState == PlayerMoveState.RUN_LEFT)
        {
            // lerp weapon to the left
            weapon.transform.localPosition = Vector3.Lerp(weapon.transform.localPosition, new Vector3(0.03f, 0, 0), Time.deltaTime * 5f);
        }
        else if (playerMoveState == PlayerMoveState.RUN_RIGHT)
        {
            // lerp weapon to the right
            weapon.transform.localPosition = Vector3.Lerp(weapon.transform.localPosition, new Vector3(-0.03f, 0, 0), Time.deltaTime * 5f);
        }
        else if (playerMoveState == PlayerMoveState.RUN_FORWARD)
        {
            // lerp weapon to the front
            weapon.transform.localPosition = Vector3.Lerp(weapon.transform.localPosition, new Vector3(0, 0, -0.03f), Time.deltaTime * 5f);
        }
        else
        {
            // lerp weapon to the center
            weapon.transform.localPosition = Vector3.Lerp(weapon.transform.localPosition, new Vector3(0, 0, 0), Time.deltaTime * 5f);
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