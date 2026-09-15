using UnityEngine;
using UnityEngine.UI;

public class NPCProfilePanel : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] GameObject _visualRoot;
    [SerializeField] RectTransform _panelRectTransform;

    [Header("Portrait")]
    [SerializeField] Image _portrait;

    [Header("Profile Values")]
    [SerializeField] DecryptingText _nameText;
    [SerializeField] DecryptingText _ageText;
    [SerializeField] DecryptingText _dateOfBirthText;
    [SerializeField] DecryptingText _occupationText;
    [SerializeField] DecryptingText _personalityText;
    [SerializeField] DecryptingText _wantedLevelText;

    [Header("World Position")]
    [SerializeField] Vector3 _worldOffset = new(0f, 2.2f, 0f);

    NPCProfile _profile;
    Camera _camera;

    public NPCProfile Profile => _profile;

    public void Initialize(
        NPCProfile profile,
        Camera camera)
    {
        _profile = profile;
        _camera = camera;

        _visualRoot.SetActive(true);

        _portrait.sprite = profile.Portrait;

        _nameText.Play(profile.FullName);
        _ageText.Play(profile.Age.ToString());
        _dateOfBirthText.Play(profile.DateOfBirth);
        _occupationText.Play(profile.Occupation);
        _personalityText.Play(profile.Personality);
        _wantedLevelText.Play(profile.WantedLevel.ToString());

        UpdatePosition();
    }

    void LateUpdate()
    {
        if (_profile == null)
            return;

        UpdatePosition();
    }

    void UpdatePosition()
    {
        if (_camera == null)
            return;

        NPCPortraitSource portraitSource =
            _profile.GetComponent<NPCPortraitSource>();

        if (portraitSource == null ||
            portraitSource.ProfileUIAnchor == null)
        {
            return;
        }

        Vector3 worldPosition =
            portraitSource.ProfileUIAnchor.position +
            _worldOffset;

        Vector3 screenPosition =
            _camera.WorldToScreenPoint(worldPosition);

        if (screenPosition.z <= 0f)
        {
            _visualRoot.SetActive(false);
            return;
        }

        _visualRoot.SetActive(true);

        _panelRectTransform.position = screenPosition;
    }

    public void StopAnimations()
    {
        _nameText.Stop();
        _ageText.Stop();
        _dateOfBirthText.Stop();
        _occupationText.Stop();
        _personalityText.Stop();
        _wantedLevelText.Stop();
    }
}