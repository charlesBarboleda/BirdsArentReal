using UnityEngine;
using Unity.Netcode;

public class NPCProfile : NetworkBehaviour
{
    [Header("Portrait")]
    [SerializeField] Sprite _portrait;
    public Sprite Portrait => _portrait;

    public string FullName { get; private set; }
    public Gender Gender;
    public int Age { get; private set; }
    public string DateOfBirth { get; private set; }
    public string Occupation { get; private set; }
    public string Personality { get; private set; }
    public int WantedLevel { get; private set; }

    public override void OnNetworkSpawn()
    {
        RandomizeProfile();
    }

    void RandomizeProfile()
    {
        FullName = $"{RandomizedFirstName(Gender)} {RandomizedLastName()}";
        DateOfBirth = RandomizedDateOfBirth(out int age);
        Age = age;
        Occupation = RandomizedOccupation();
        Personality = RandomizedPersonality();
        WantedLevel = 0;
    }

    public void SetWantedLevel(int level)
    {
        WantedLevel = Mathf.Clamp(level, 0, 5);
    }

    string RandomizedPersonality()
    {
        PersonalityTraits[] traits = (PersonalityTraits[])System.Enum.GetValues(typeof(PersonalityTraits));
        return traits[Random.Range(0, traits.Length)].ToString();
    }

    string RandomizedOccupation()
    {
        Occupations[] occupations = (Occupations[])System.Enum.GetValues(typeof(Occupations));
        return occupations[Random.Range(0, occupations.Length)].ToString();
    }

    string RandomizedDateOfBirth(out int age)
    {
        int year = Random.Range(1970, 2005);
        int month = Random.Range(1, 13);
        int day = Random.Range(1, 29); // Simplified to avoid month length issues
        age = Time.timeSinceLevelLoad > 0 ? Random.Range(18, 65) : 0;
        return $"{year}-{month:D2}-{day:D2}";
    }

    string RandomizedLastName()
    {
        LastNames[] lastNames = (LastNames[])System.Enum.GetValues(typeof(LastNames));
        return lastNames[Random.Range(0, lastNames.Length)].ToString();
    }

    string RandomizedFirstName(Gender gender)
    {
        if (gender == Gender.Male)
        {
            MaleNames[] maleNames = (MaleNames[])System.Enum.GetValues(typeof(MaleNames));
            return maleNames[Random.Range(0, maleNames.Length)].ToString();
        }
        else if (gender == Gender.Female)
        {
            FemaleNames[] femaleNames = (FemaleNames[])System.Enum.GetValues(typeof(FemaleNames));
            return femaleNames[Random.Range(0, femaleNames.Length)].ToString();
        }
        else
        {
            // For non-binary, we can choose from both male and female names or create a separate list. Here, we'll just combine both for simplicity. 
            MaleNames[] maleNames = (MaleNames[])System.Enum.GetValues(typeof(MaleNames));
            FemaleNames[] femaleNames = (FemaleNames[])System.Enum.GetValues(typeof(FemaleNames));
            string[] combinedNames = new string[maleNames.Length + femaleNames.Length];
            for (int i = 0; i < maleNames.Length; i++)
            {
                combinedNames[i] = maleNames[i].ToString();
            }
            for (int i = 0; i < femaleNames.Length; i++)
            {
                combinedNames[maleNames.Length + i] = femaleNames[i].ToString();
            }
            return combinedNames[Random.Range(0, combinedNames.Length)];
        }
    }
}

public enum PersonalityTraits
{
    Friendly,
    Aggressive,
    Shy,
    Outgoing,
    Intelligent,
    Lazy,
    Ambitious,
    Creative,
    Honest,
    Deceptive
}

public enum Occupations
{
    Engineer,
    Doctor,
    Teacher,
    Artist,
    Musician,
    Writer,
    Chef,
    Athlete,
    Scientist,
    Entrepreneur
}

public enum LastNames
{
    Smith,
    Johnson,
    Williams,
    Brown,
    Jones,
    Garcia,
    Miller,
    Davis,
    Rodriguez,
    Martinez
}

public enum FemaleNames
{
    Emma,
    Olivia,
    Ava,
    Isabella,
    Sophia,
    Mia,
    Charlotte,
    Amelia,
    Harper,
    Evelyn
}

public enum MaleNames
{
    Liam,
    Noah,
    Oliver,
    Elijah,
    James,
    William,
    Benjamin,
    Lucas,
    Henry,
    Alexander
}

public enum Gender
{
    Male,
    Female,
    NonBinary
}