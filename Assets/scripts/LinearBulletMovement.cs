using UnityEngine;

public class LinearBulletMovement : MonoBehaviour
{
    private Vector3 startPoint;
    private Vector3 endPoint;
    private float speed;
    private float journeyLength;
    private float distanceTraveled = 0f;
    private bool initialized = false;

    public void Initialize(Vector3 start, Vector3 end, float moveSpeed)
    {
        startPoint = start;
        endPoint = end;
        speed = moveSpeed;
        journeyLength = Vector3.Distance(start, end);
        initialized = true;
        
        // Point bullet toward target
        Vector3 direction = (end - start).normalized;
        transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(90, 0, 0);
    }

    void Update()
    {
        if (initialized)
        {
            // Move along the straight line from start to end
            distanceTraveled += speed * Time.deltaTime * 4.0f;
            
            // Calculate position along the line (0 = start, 1 = end)
            float journeyFraction = distanceTraveled / journeyLength;
            
            // Use Vector3.Lerp to move along the exact straight line
            transform.position = Vector3.Lerp(startPoint, endPoint, journeyFraction);
            
            // Optional: Destroy bullet if it reaches the end
            if (journeyFraction >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}