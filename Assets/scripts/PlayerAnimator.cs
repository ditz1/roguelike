using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    GameObject player_prefab;
    Animator animator;
    Rigidbody rb;

    void Start()
    {

        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody component not found. Make sure this script is attached to the player object.");
            return;
        }

        player_prefab = GameObject.Find("Playerfull");
        if (player_prefab == null)
        {
            Debug.LogError("Player prefab not found. Make sure it is named 'Playerfull' and is a child of this object.");
            return;
        }

        animator = player_prefab.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("Animator component not found on Playerfull.");
            return;
        }

        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        if (controller == null)
        {
            Debug.LogError("Animator controller is missing.");
            return;
        }

    }

    void Update()
    {
        if (Input.GetKey(KeyCode.W))
        {
            animator.Play("runforward");
        }
        else if (Input.GetKey(KeyCode.S))
        {
            animator.Play("runback");
        }
        else if (Input.GetKey(KeyCode.A))
        {
            animator.Play("runleft");
        }
        else if (Input.GetKey(KeyCode.D))
        {
            animator.Play("runright");
        } else {
            animator.Play("idle");
        }
    }
}
