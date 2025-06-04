using UnityEngine;

public static class Helpers
{
    public static GameObject FindChildInObject(GameObject parent, string name)
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
}
