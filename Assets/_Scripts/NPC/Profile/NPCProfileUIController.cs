using System.Collections.Generic;
using UnityEngine;

public class NPCProfileUIController : MonoBehaviour
{
    public static NPCProfileUIController Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] NPCProfilePanel _panelPrefab;
    [SerializeField] Transform _panelParent;

    Camera _camera;

    readonly Dictionary<NPCProfile, NPCProfilePanel>
        _markedPanels = new();

    NPCProfile _currentProfile;
    NPCProfilePanel _currentAimedPanel;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        NPCProfile[] profiles =
            FindObjectsByType<NPCProfile>();

        foreach (NPCProfile profile in profiles)
        {
            RegisterProfile(profile);
        }
    }

    public void AssignCamera(Camera camera)
    {
        _camera = camera;
    }

    public void RegisterProfile(NPCProfile profile)
    {
        if (profile == null)
            return;

        profile.MarkedStateChanged -=
            OnMarkedStateChanged;

        profile.MarkedStateChanged +=
            OnMarkedStateChanged;

        if (profile.IsMarked)
        {
            CreateMarkedPanel(profile);
        }
    }

    public void UnregisterProfile(NPCProfile profile)
    {
        if (profile == null)
            return;

        profile.MarkedStateChanged -=
            OnMarkedStateChanged;

        RemoveMarkedPanel(profile);

        if (_currentProfile == profile)
        {
            HideAimedProfile();
        }
    }

    void OnMarkedStateChanged(
        NPCProfile profile,
        bool isMarked)
    {
        if (isMarked)
        {
            CreateMarkedPanel(profile);

            if (_currentProfile == profile)
            {
                HideAimedProfile();
            }
        }
        else
        {
            RemoveMarkedPanel(profile);
        }
    }

    void CreateMarkedPanel(NPCProfile profile)
    {
        if (_markedPanels.ContainsKey(profile))
            return;

        if (_panelPrefab == null)
        {
            Debug.LogError(
                "NPCProfileUIController has no panel prefab assigned.",
                this);

            return;
        }

        NPCProfilePanel panel =
            Instantiate(
                _panelPrefab,
                _panelParent);

        panel.Initialize(
            profile,
            _camera);

        _markedPanels.Add(
            profile,
            panel);
    }

    void RemoveMarkedPanel(NPCProfile profile)
    {
        if (!_markedPanels.TryGetValue(
                profile,
                out NPCProfilePanel panel))
        {
            return;
        }

        if (panel != null)
        {
            panel.StopAnimations();
            Destroy(panel.gameObject);
        }

        _markedPanels.Remove(profile);
    }

    public void ShowAimedProfile(NPCProfile profile)
    {
        if (profile == null)
            return;

        if (_currentProfile == profile)
            return;

        HideAimedProfile();

        _currentProfile = profile;

        if (_markedPanels.ContainsKey(profile))
            return;

        if (_panelPrefab == null)
            return;

        _currentAimedPanel =
            Instantiate(
                _panelPrefab,
                _panelParent);

        _currentAimedPanel.Initialize(
            profile,
            _camera);
    }

    public void HideAimedProfile()
    {
        if (_currentAimedPanel != null)
        {
            _currentAimedPanel.StopAnimations();

            Destroy(
                _currentAimedPanel.gameObject);

            _currentAimedPanel = null;
        }

        _currentProfile = null;
    }
}