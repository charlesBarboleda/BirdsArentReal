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

    readonly NetworkList<ulong> _markedByClientIds =
    new NetworkList<ulong>();

    public bool IsMarked =>
    _markedByClientIds.Count > 0;

    readonly NetworkVariable<int> _profileSeed =
        new NetworkVariable<int>();

    readonly NetworkVariable<bool> _isMarked =
new NetworkVariable<bool>(
    false,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server);

    public event System.Action<NPCProfile, bool> MarkedStateChanged;

    bool _wasMarked;

    public override void OnNetworkSpawn()
    {
        _profileSeed.OnValueChanged += OnProfileSeedChanged;

        _markedByClientIds.OnListChanged += OnMarkedByListChanged;

        _wasMarked = IsMarked;

        if (IsServer)
        {
            GenerateProfileSeed();
        }

        if (_profileSeed.Value != 0)
        {
            GenerateProfile();
        }

        if (NPCProfileUIController.Instance != null)
        {
            NPCProfileUIController.Instance.RegisterProfile(this);
        }
    }

    public override void OnNetworkDespawn()
    {
        _profileSeed.OnValueChanged -= OnProfileSeedChanged;

        _markedByClientIds.OnListChanged -= OnMarkedByListChanged;

        if (NPCProfileUIController.Instance != null)
        {
            NPCProfileUIController.Instance.UnregisterProfile(this);
        }
    }

    void OnMarkedByListChanged(
     NetworkListEvent<ulong> changeEvent)
    {
        bool isMarked = IsMarked;

        if (_wasMarked == isMarked)
            return;

        _wasMarked = isMarked;

        MarkedStateChanged?.Invoke(
            this,
            isMarked);
    }

    public bool IsMarkedBy(ulong clientId)
    {
        return _markedByClientIds.Contains(clientId);
    }

    [Rpc(SendTo.Server)]
    public void RequestToggleMarkRpc(
     RpcParams rpcParams = default)
    {
        if (!IsServer)
            return;

        ulong clientId =
            rpcParams.Receive.SenderClientId;

        if (IsMarkedBy(clientId))
        {
            RemoveMark(clientId);
            return;
        }

        if (GetPlayerMarkCount(clientId) >= 3)
            return;

        AddMark(clientId);
    }

    int GetPlayerMarkCount(ulong clientId)
    {
        int count = 0;

        NPCProfile[] profiles =
            FindObjectsByType<NPCProfile>();

        foreach (NPCProfile profile in profiles)
        {
            if (profile.IsMarkedBy(clientId))
            {
                count++;
            }
        }

        return count;
    }

    void AddMark(ulong clientId)
    {
        if (_markedByClientIds.Contains(clientId))
            return;

        _markedByClientIds.Add(clientId);
    }

    void RemoveMark(ulong clientId)
    {
        if (!_markedByClientIds.Contains(clientId))
            return;

        _markedByClientIds.Remove(clientId);
    }

    void OnProfileSeedChanged(int previousSeed, int newSeed)
    {
        GenerateProfile();
    }

    public void SetMarked(bool marked)
    {
        if (!IsServer)
            return;

        _isMarked.Value = marked;
    }

    void GenerateProfileSeed()
    {
        _profileSeed.Value = Random.Range(
            int.MinValue,
            int.MaxValue);
    }

    void GenerateProfile()
    {
        Random.State previousState = Random.state;

        Random.InitState(_profileSeed.Value);

        FullName = GenerateFullName();
        DateOfBirth = GenerateDateOfBirth(out int age);
        Age = age;
        Occupation = GenerateOccupation();
        Personality = GeneratePersonality();

        WantedLevel = 0;

        Random.state = previousState;
    }

    string GenerateFullName()
    {
        string firstName = RandomizedFirstName(Gender);
        string lastName = RandomizedLastName();

        return $"{firstName} {lastName}";
    }

    string GenerateDateOfBirth(out int age)
    {
        age = Random.Range(18, 66);

        int currentYear = System.DateTime.Now.Year;

        int birthYear = currentYear - age;

        int month = Random.Range(1, 13);
        int day = Random.Range(1, 29);

        return $"{birthYear}-{month:D2}-{day:D2}";
    }

    string GeneratePersonality()
    {
        PersonalityTraits[] traits =
            (PersonalityTraits[])System.Enum.GetValues(
                typeof(PersonalityTraits));

        return FormatEnumText(
            traits[Random.Range(0, traits.Length)].ToString());
    }

    string GenerateOccupation()
    {
        Occupations[] occupations =
            (Occupations[])System.Enum.GetValues(
                typeof(Occupations));

        return FormatEnumText(
            occupations[Random.Range(0, occupations.Length)].ToString());
    }

    string RandomizedLastName()
    {
        LastNames[] lastNames =
            (LastNames[])System.Enum.GetValues(
                typeof(LastNames));

        return lastNames[
            Random.Range(0, lastNames.Length)
        ].ToString();
    }

    string RandomizedFirstName(Gender gender)
    {
        if (gender == Gender.Male)
        {
            MaleNames[] maleNames =
                (MaleNames[])System.Enum.GetValues(
                    typeof(MaleNames));

            return maleNames[
                Random.Range(0, maleNames.Length)
            ].ToString();
        }

        if (gender == Gender.Female)
        {
            FemaleNames[] femaleNames =
                (FemaleNames[])System.Enum.GetValues(
                    typeof(FemaleNames));

            return femaleNames[
                Random.Range(0, femaleNames.Length)
            ].ToString();
        }

        MaleNames[] male =
            (MaleNames[])System.Enum.GetValues(
                typeof(MaleNames));

        FemaleNames[] female =
            (FemaleNames[])System.Enum.GetValues(
                typeof(FemaleNames));

        string[] combinedNames =
            new string[male.Length + female.Length];

        for (int i = 0; i < male.Length; i++)
        {
            combinedNames[i] = male[i].ToString();
        }

        for (int i = 0; i < female.Length; i++)
        {
            combinedNames[male.Length + i] =
                female[i].ToString();
        }

        return combinedNames[
            Random.Range(0, combinedNames.Length)
        ];
    }

    public void SetWantedLevel(int level)
    {
        if (!IsServer)
            return;

        WantedLevel = Mathf.Clamp(level, 0, 5);
    }

    string FormatEnumText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        System.Text.StringBuilder result =
            new System.Text.StringBuilder();

        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];

            if (i > 0 && char.IsUpper(character))
            {
                result.Append(' ');
            }

            result.Append(character);
        }

        return result.ToString();
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
    Deceptive,

    Calm,
    Nervous,
    Confident,
    Pessimistic,
    Optimistic,
    Sarcastic,
    Serious,
    Playful,
    Curious,
    Suspicious,
    Patient,
    Impatient,
    Generous,
    Selfish,
    Loyal,
    Unreliable,
    Polite,
    Rude,
    Charismatic,
    Awkward,
    Stubborn,
    Reckless,
    Cautious,
    Secretive,
    Talkative,
    Quiet,
    Compassionate,
    Arrogant,
    Humble,
    Competitive,
    Relaxed,
    Paranoid,
    Resourceful,
    Forgetful,
    Disciplined,
    Careless
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
    Entrepreneur,

    Accountant,
    Architect,
    Lawyer,
    Journalist,
    Photographer,
    Mechanic,
    Electrician,
    Plumber,
    Carpenter,
    ConstructionWorker,
    PoliceOfficer,
    Firefighter,
    Paramedic,
    SecurityGuard,
    Detective,
    Pilot,
    Driver,
    TaxiDriver,
    DeliveryDriver,
    SoftwareDeveloper,
    GraphicDesigner,
    Barber,
    Hairdresser,
    Waiter,
    Bartender,
    StoreClerk,
    Cashier,
    Farmer,
    Fisherman,
    Veterinarian,
    Pharmacist,
    Dentist,
    Nurse,
    Psychologist,
    RealEstateAgent,
    Salesperson,
    BusinessOwner,
    Receptionist,
    Librarian,
    Researcher,
    Professor,
    Student,
    Unemployed,
    Retired
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
    Evelyn,

    Abigail,
    Emily,
    Elizabeth,
    Sofia,
    Avery,
    Ella,
    Scarlett,
    Victoria,
    Aria,
    Grace,
    Chloe,
    Camila,
    Penelope,
    Riley,
    Layla,
    Lillian,
    Nora,
    Zoey,
    Mila,
    Aubrey,
    Hannah,
    Lily,
    Addison,
    Eleanor,
    Natalie,
    Luna,
    Savannah,
    Brooklyn,
    Leah,
    Hazel,
    Violet,
    Aurora,
    Ellie,
    Stella,
    Claire,
    Lucy,
    Anna,
    Maya,
    Naomi,
    Elena,
    Caroline,
    Alice,
    Sarah,
    Julia,
    Madeline,
    Sophie,
    Eva,
    Ruby,
    Clara,
    Katherine,
    Lydia,
    Jasmine,
    Rose,
    Allison,
    Maria,
    Jade,
    Valerie,
    Cora,
    Vivian
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
    Alexander,

    Mason,
    Michael,
    Ethan,
    Daniel,
    Jacob,
    Logan,
    Jackson,
    Levi,
    Sebastian,
    Mateo,
    Jack,
    Owen,
    Theodore,
    Aiden,
    Samuel,
    Joseph,
    John,
    David,
    Wyatt,
    Matthew,
    Luke,
    Asher,
    Carter,
    Julian,
    Grayson,
    Leo,
    Jayden,
    Gabriel,
    Isaac,
    Lincoln,
    Anthony,
    Hudson,
    Dylan,
    Ezra,
    Thomas,
    Charles,
    Christopher,
    Jaxon,
    Maverick,
    Josiah,
    Andrew,
    Elias,
    Joshua,
    Nathan,
    Caleb,
    Ryan,
    Adrian,
    Miles,
    Nolan,
    Christian,
    Aaron,
    Cameron,
    Ezekiel,
    Colton,
    Luca,
    Landon,
    Hunter,
    Jonathan,
    Connor,
    Santiago
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
    Martinez,
    Anderson,
    Taylor,
    Thomas,
    Moore,
    Jackson,
    Martin,
    Lee,
    Perez,
    Thompson,
    White,
    Harris,
    Sanchez,
    Clark,
    Ramirez,
    Lewis,
    Robinson,
    Walker,
    Young,
    Allen,
    King,
    Wright,
    Scott,
    Torres,
    Nguyen,
    Hill,
    Flores,
    Green,
    Adams,
    Nelson,
    Baker,
    Hall,
    Rivera,
    Campbell,
    Mitchell,
    Carter,
    Roberts,
    Gomez,
    Phillips,
    Evans,
    Turner,
    Diaz,
    Parker,
    Cruz,
    Edwards,
    Collins,
    Reyes,
    Stewart,
    Morris,
    Morales,
    Murphy,
    Cook,
    Rogers,
    Gutierrez,
    Ortiz,
    Morgan,
    Cooper,
    Peterson,
    Bailey,
    Reed,
    Kelly,
    Howard,
    Ramos,
    Kim,
    Cox,
    Ward,
    Richardson,
    Watson,
    Brooks,
    Chavez,
    Wood,
    James,
    Bennett,
    Gray,
    Mendoza,
    Ruiz,
    Hughes,
    Price,
    Alvarez,
    Castillo,
    Sanders,
    Patel,
    Myers,
    Long,
    Ross,
    Foster,
    Jimenez,
    Powell,
    Jenkins,
    Perry,
    Russell,
    Sullivan,
    Bell,
    Coleman,
    Butler,
    Henderson,
    Barnes,
    Fisher,
    Vasquez,
    Simmons,
    Romero,
    Jordan,
    Patterson,
    Alexander,
    Hamilton,
    Graham,
    Reynolds,
    Griffin,
    Wallace,
    Moreno,
    West,
    Cole,
    Hayes,
    Bryant,
    Herrera,
    Gibson,
    Ellis,
    Tran,
    Medina,
    Aguilar,
    Stevens,
    Murray,
    Ford,
    Castro,
    Marshall,
    Owens,
    Harrison,
    Fernandez,
    McDonald,
    Woods,
    Washington,
    Kennedy,
    Wells,
    Vargas,
    Henry,
    Chen,
    Freeman,
    Webb,
    Tucker,
    Guzman,
    Burns,
    Crawford,
    Olson,
    Simpson,
    Porter,
    Hunter,
    Gordon,
    Mendez,
    Silva,
    Shaw,
    Snyder,
    Mason,
    Dixon,
    Munoz,
    Hunt,
    Hicks,
    Holmes
}

public enum Gender
{
    Male,
    Female,
    NonBinary
}