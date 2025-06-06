using UnityEngine;

public class BarrierControl : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public int barrier_hp = 3;
    void Start()
    {


    }

    // Update is called once per frame
    void Update()
    {

    }

    public void DamageBarrier()
    {
        barrier_hp -= 1; // Reduce the barrier's health by the damage amount
        if (barrier_hp <= 0)
        {
            Destroy(gameObject); // Destroy the barrier if its health reaches zero
        }

    }
}
