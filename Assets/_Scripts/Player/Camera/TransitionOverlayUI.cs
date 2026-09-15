using UnityEngine;

public class TransitionOverlayUI : MonoBehaviour
{
    public static TransitionOverlayUI Instance { get; private set; }


    [SerializeField] RectTransform _transitionPanel;
    public RectTransform TransitionPanel => _transitionPanel;

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
