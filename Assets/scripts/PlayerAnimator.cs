using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    GameObject player_prefab;
    Animator animator;
    Rigidbody rb;

    PlayerController playerController;

    void Start()
    {

        GameObject model = FindChildInObject(gameObject, "model");
        player_prefab = FindChildInObject(model, "playerfullsplit"); // eventually this will be character selected by player
        playerController = GetComponent<PlayerController>();
        if (playerController == null)
        {
            Debug.LogError("PlayerController component not found. Make sure this script is attached to the player object.");
            return;
        }

        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody component not found. Make sure this script is attached to the player object.");
            return;
        }

        //player_prefab = GameObject.Find("playerfullsplit");
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

    GameObject FindChildInObject(GameObject parent, string name)
    {
        if (parent == null) return null;

        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
                return child.gameObject;
        }
        return null;
    }

    void Update()
    {
        if (playerController.playerMoveState == PlayerController.PlayerMoveState.RUN_FORWARD)
        {
            animator.Play("runforward");
        }
        else if (playerController.playerMoveState == PlayerController.PlayerMoveState.RUN_BACKWARD)
        {
            animator.Play("runback");
        }
        else if (playerController.playerMoveState == PlayerController.PlayerMoveState.RUN_LEFT)
        {
            animator.Play("runleft");
            player_prefab.transform.parent.transform.localScale = new Vector3(-0.5f, 0.4f, 0.5f); // Flip the model to face left
        }
        else if (playerController.playerMoveState == PlayerController.PlayerMoveState.RUN_RIGHT)
        {
            animator.Play("runright");
            player_prefab.transform.parent.transform.localScale = new Vector3(0.5f, 0.4f, 0.5f); // Flip the model to face left

        } else {
            animator.Play("idle");
        }
    }
}
