using UnityEngine;

public class CrosshairUI : MonoBehaviour
{
    public static CrosshairUI Instance { get; private set; }

    public GameObject Crosshair => gameObject;

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
