using TMPro;
using UnityEngine;

public class DecryptingText : MonoBehaviour
{
    [SerializeField] TMP_Text _text;

    [Header("Animation")]
    [SerializeField] float _characterRevealSpeed = 20f;

    [Header("Characters")]
    [SerializeField]
    string _randomCharacters =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%&";

    string _targetText;
    float _revealProgress;
    bool _isDecrypting;

    void Update()
    {
        if (!_isDecrypting)
            return;

        UpdateDecryption();
    }

    public void Play(string targetText)
    {
        _targetText = targetText ?? string.Empty;

        _revealProgress = 0f;
        _isDecrypting = true;

        UpdateDecryption();
    }

    public void Stop()
    {
        _isDecrypting = false;
    }

    void UpdateDecryption()
    {
        _revealProgress +=
            _characterRevealSpeed * Time.deltaTime;

        int revealedCharacters =
            Mathf.FloorToInt(_revealProgress);

        revealedCharacters =
            Mathf.Clamp(
                revealedCharacters,
                0,
                _targetText.Length);

        _text.text =
            GenerateDisplayText(revealedCharacters);

        if (revealedCharacters >= _targetText.Length)
        {
            _text.text = _targetText;
            _isDecrypting = false;
        }
    }

    string GenerateDisplayText(int revealedCharacters)
    {
        char[] result =
            _targetText.ToCharArray();

        for (int i = revealedCharacters; i < result.Length; i++)
        {
            if (result[i] == ' ')
                continue;

            result[i] =
                _randomCharacters[
                    Random.Range(
                        0,
                        _randomCharacters.Length)];
        }

        return new string(result);
    }
}