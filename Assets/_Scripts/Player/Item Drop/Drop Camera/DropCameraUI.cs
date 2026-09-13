using UnityEngine;

public class DropCameraUI : MonoBehaviour
{
    public static DropCameraUI Instance { get; private set; }

    public GameObject Panel => gameObject;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
}