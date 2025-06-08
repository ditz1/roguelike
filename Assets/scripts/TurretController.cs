using UnityEngine;

public class TurretController : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] GameObject turretHead;
    Transform nearest_enemy;
    [SerializeField] ParticleSystem muzzleflash;
    [SerializeField] ParticleSystem muzzleflash_1;

    EnemyController enemyController;

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
        turretHead.transform.localPosition = new Vector3(0, 0.8f, 0);
        if (nearest_enemy == null)
        {
            nearest_enemy = FindNearestEnemy();
        }
        else
        {
            Vector3 direction = nearest_enemy.position - turretHead.transform.position;
            distance_to_nearest_enemy = Vector3.Distance(turretHead.transform.position, nearest_enemy.position);
            turretHead.transform.rotation = Quaternion.LookRotation(direction);
        }
        nextFireTime += Time.deltaTime;
        if (nextFireTime >= fireRate)
        {
            nextFireTime = 0f;
            Shoot();
        }
    }

    void Shoot()
    {
        if (nearest_enemy != null && distance_to_nearest_enemy < 10f)
        {
            muzzleflash.Play();
            muzzleflash_1.Play();
            // Here you can add code to instantiate a bullet or apply damage to the enemy
            enemyController.TakeDamage(5); // Assuming TakeDamage is a method in EnemyController
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
        return nearest;
    }
}
