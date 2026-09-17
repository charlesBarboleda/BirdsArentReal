using UnityEngine;
using UnityEngine.UI;

public class NPCMarkProgressUI : MonoBehaviour
{
    public static NPCMarkProgressUI Instance { get; private set; }

    [SerializeField] GameObject _panel;
    [SerializeField] Image _fillImage;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        Hide();
        SetProgress(0f);
    }

    public void Show()
    {
        if (_panel != null)
        {
            _panel.SetActive(true);
        }
    }

    public void Hide()
    {
        if (_panel != null)
        {
            _panel.SetActive(false);
        }
    }

    public void SetProgress(float normalizedProgress)
    {
        if (_fillImage == null)
            return;

        _fillImage.fillAmount =
            Mathf.Clamp01(normalizedProgress);
    }
}