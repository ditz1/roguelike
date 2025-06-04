using System.Collections;
using UnityEngine;

public class ZombSpawner : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private GameObject zombPrefab;
    public int num_zombs_left = 0;
    int num_zombs_to_spawn = 0;
    int current_round = 0;


    // Update is called once per frame
    void Update()
    {
        if (GameManager.Instance.CurrentRound < 0)
        {
            return; // Wait for the game to start
        }

        if (GameManager.Instance.CurrentRound > current_round && num_zombs_left <= 0)
        {
            num_zombs_to_spawn = GameManager.Instance.CurrentRound * 2; // scaling
            SpawnZombs(num_zombs_to_spawn);
            current_round = GameManager.Instance.CurrentRound;
        }
        // every 5 seconds, find all zombies in the scene
        if (Time.time % 5 < 0.1f)
        {
            Debug.Log("Finding all zombies in the scene...");
            FindAllZombs();
        }
    }

    void SpawnZombs(int num_zombs)
    {
        StartCoroutine(SpawnZombsCoroutine(num_zombs));
    }
    
    IEnumerator SpawnZombsCoroutine(int num_zombs)
    {
        for (int i = 0; i < num_zombs; i++)
        {
            Vector3 spawnPosition = transform.position;
            GameObject zomb = Instantiate(zombPrefab, spawnPosition, Quaternion.identity);
            zomb.name = "Zomb_" + i;
            num_zombs_left++;
            num_zombs_to_spawn--;
            
            // Wait 0.1 seconds before next spawn
            yield return new WaitForSeconds(0.1f);
        }
    }
    
    void FindAllZombs()
    {
        GameObject[] zombs = GameObject.FindGameObjectsWithTag("Zomb");
        num_zombs_left = zombs.Length;
        Debug.Log("Found " + num_zombs_left + " zombies in the scene.");
    }
}
