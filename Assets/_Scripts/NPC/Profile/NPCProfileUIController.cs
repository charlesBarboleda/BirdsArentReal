using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class NPCProfileUIController : MonoBehaviour
{
    public static NPCProfileUIController Instance { get; private set; }

    [SerializeField] GameObject _panel;
    [SerializeField] Image _portrait;
    [SerializeField] TMP_Text _nameText;
    [SerializeField] TMP_Text _ageText;
    [SerializeField] TMP_Text _dateOfBirthText;
    [SerializeField] TMP_Text _occupationText;
    [SerializeField] TMP_Text _personalityText;
    [SerializeField] TMP_Text _wantedLevelText;

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
        _nameText.text = profile.FullName;
        _ageText.text = $"Age: {profile.Age}";
        _dateOfBirthText.text = $"DOB: {profile.DateOfBirth}";
        _occupationText.text = $"Occupation: {profile.Occupation}";
        _personalityText.text = $"Personality: {profile.Personality}";
        _wantedLevelText.text = $"Wanted Level: {profile.WantedLevel}";
    }

    public void Hide()
    {
        _panel.SetActive(false);
    }
}
