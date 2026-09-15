using UnityEngine;
using UnityEngine.UI;

public class NPCProfileUIController : MonoBehaviour
{
    public static NPCProfileUIController Instance { get; private set; }

    [SerializeField] GameObject _panel;
    [SerializeField] Image _portrait;
    [SerializeField] DecryptingText _nameText;
    [SerializeField] DecryptingText _ageText;
    [SerializeField] DecryptingText _dateOfBirthText;
    [SerializeField] DecryptingText _occupationText;
    [SerializeField] DecryptingText _personalityText;
    [SerializeField] DecryptingText _wantedLevelText;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void Show(NPCProfile profile)
    {
        _panel.SetActive(true);

        _portrait.sprite = profile.Portrait;

        _nameText.Play(profile.FullName);
        _ageText.Play(profile.Age.ToString());
        _dateOfBirthText.Play(profile.DateOfBirth);
        _occupationText.Play(profile.Occupation);
        _personalityText.Play(profile.Personality);
        _wantedLevelText.Play(profile.WantedLevel.ToString());
    }

    public void Hide()
    {
        _nameText.Stop();
        _ageText.Stop();
        _dateOfBirthText.Stop();
        _occupationText.Stop();
        _personalityText.Stop();
        _wantedLevelText.Stop();

        _panel.SetActive(false);
    }
}
