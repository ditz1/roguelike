using UnityEngine;

public class TurretController : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] GameObject turretHead;
    [SerializeField] Transform pivotPoint;
    Transform nearest_enemy;
    [SerializeField] ParticleSystem muzzleflash;
    [SerializeField] ParticleSystem muzzleflash_1;

    EnemyController enemyController;

    bool localIsPlaced = false;

    float distance_to_nearest_enemy;
    float fireRate = 1f; // Time in seconds between shots
    float nextFireTime = 0f;


    void Start()
    {
        nearest_enemy = null;
        distance_to_nearest_enemy = Mathf.Infinity;
    }

    // Update is called once per frame
    void Update()
    {
        if (!localIsPlaced)
        {
            // Check if the turret is placed
            if (GetComponent<Structure>().isPlaced)
            {
                localIsPlaced = true;
                Debug.Log("Turret is now placed.");
                transform.rotation = Quaternion.Euler(0, 0, 0); // Reset rotation to face forward
            }
            else
            {
                return; // Exit Update if turret is not placed
            }
        }

        if (nearest_enemy == null)
        {
            nearest_enemy = FindNearestEnemy();
        }
        else
        {
            // look at nearest enemy
            Vector3 direction = nearest_enemy.position - turretHead.transform.position;
            distance_to_nearest_enemy = Vector3.Distance(turretHead.transform.position, nearest_enemy.position);
            pivotPoint.localRotation = Quaternion.LookRotation(direction);
        }
        Shoot();


        
    }

    void Shoot()
    {
        nextFireTime += Time.deltaTime;
        if (distance_to_nearest_enemy <= 10f)
        {
            muzzleflash.Play();
            muzzleflash_1.Play();
            if (nearest_enemy != null && nextFireTime >= fireRate)
            {
                nextFireTime = 0f;                
                enemyController.TakeDamage(5); // Assuming TakeDamage is a method in EnemyController
            }
        }
    }

    Transform FindNearestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Zomb");
        float minDistance = Mathf.Infinity;
        Transform nearest = null;

        foreach (GameObject enemy in enemies)
        {
            float distance = Vector3.Distance(turretHead.transform.position, enemy.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = enemy.transform;
                enemyController = enemy.GetComponent<EnemyController>();
            }
        }
        if (nearest == null)
        {
            Debug.LogWarning("No enemies found with tag 'Zomb'.");
            distance_to_nearest_enemy = Mathf.Infinity;
        }
        
        return nearest;
    }
}
