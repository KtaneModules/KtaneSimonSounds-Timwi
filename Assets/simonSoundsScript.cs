using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using Random = UnityEngine.Random;

public class simonSoundsScript : MonoBehaviour
{
    public KMAudio Audio;
    public KMBombInfo Bomb;

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    public KMSelectable SamBtnRed;
    public KMSelectable SamBtnBlue;
    public KMSelectable SamBtnYellow;
    public KMSelectable SamBtnGreen;
    public KMSelectable InBtnRed;
    public KMSelectable InBtnBlue;
    public KMSelectable InBtnYellow;
    public KMSelectable InBtnGreen;
    public AudioClip[] Sounds;

    private int[] selectedNumbers = new int[4] { 0, 0, 0, 0 };
    private int lastInCon = -1;
    private int lastSamCon = -1;
    private int red = 0;
    private int blue = 0;
    private int yellow = 0;
    private int green = 0;
    private const int RedInput = 0;
    private const int BlueInput = 1;
    private const int YellowInput = 2;
    private const int GreenInput = 3;
    private int timesPressed = -1;
    private int gameLength = 0;
    private List<int>[] stage;
    private int currentStage = 0;
    private List<int> selectedIndices = new List<int>();
    private static readonly string[] colorNames = new[] { "red", "blue", "yellow", "green" };
    private static readonly int[][] table1 = new[]
    {
        new[] { BlueInput, YellowInput, GreenInput, RedInput },
        new[] { RedInput, YellowInput, BlueInput, GreenInput },
        new[] { GreenInput, RedInput, YellowInput, BlueInput },
        new[] { YellowInput, GreenInput, RedInput, BlueInput },
        new[] { GreenInput, BlueInput, RedInput, YellowInput }
    };
    private static readonly int[][] table2 = new[]
    {
        new[] { BlueInput, GreenInput, RedInput, YellowInput },
        new[] { YellowInput, BlueInput, RedInput, GreenInput },
        new[] { RedInput, GreenInput, BlueInput, YellowInput },
        new[] { GreenInput, YellowInput, BlueInput, RedInput },
        new[] { YellowInput, RedInput, GreenInput, BlueInput }
    };

    private Coroutine beep;

    struct Condition
    {
        public string Explanation;
        public Func<KMBombInfo, bool> Eval;
    }

    private static readonly Condition[] sampleConditions = new Condition[]
    {
        new Condition { Explanation = "There are more than 3 port plates", Eval = bomb => bomb.GetPortPlateCount() > 3 },
        new Condition { Explanation = "There are more AA batteries than D batteries", Eval = bomb => (bomb.GetBatteryCount(Battery.AA) + bomb.GetBatteryCount(Battery.AAx3) + bomb.GetBatteryCount(Battery.AAx4)) > bomb.GetBatteryCount(Battery.D) },
        new Condition { Explanation = "The serial number contains a vowel and an even digit", Eval = bomb => (bomb.GetSerialNumberLetters().Any(x => x == 'A' || x == 'E' || x == 'I' || x == 'O' || x == 'U')) && (bomb.GetSerialNumberNumbers().Any(x => x % 2 == 0)) },
        new Condition { Explanation = "The number of lit indicators equals the number of ports", Eval = bomb => bomb.GetOnIndicators().Count() == bomb.GetPortCount() },
        new Condition { Explanation = "Otherwise", Eval = bomb => true }
    };

    private static readonly Condition[] inputConditions = new Condition[]
    {
        new Condition { Explanation = "There more solved than unsolved modules", Eval = bomb => bomb.GetSolvedModuleNames().Count() > (bomb.GetSolvableModuleNames().Count() - bomb.GetSolvedModuleNames().Count()) },
        new Condition { Explanation = "Battery holders plus port plates plus the SN last digit < 10", Eval = bomb => bomb.GetBatteryHolderCount() + bomb.GetPortPlateCount() + bomb.GetSerialNumberNumbers().Last() < 10 },
        new Condition { Explanation = "There is a serial and a parallel port", Eval = bomb => bomb.IsPortPresent(Port.Serial) && bomb.IsPortPresent(Port.Parallel) },
        new Condition { Explanation = "There is a lit BOB or an unlit NSA", Eval = bomb => bomb.IsIndicatorOn(Indicator.BOB) || bomb.IsIndicatorOff(Indicator.NSA) },
        new Condition { Explanation = "Otherwise", Eval = bomb => true }
    };

    void Awake()
    {

        moduleId = moduleIdCounter++;

        SamBtnRed.OnInteract += delegate () { SamBtnPress(red); return false; };
        SamBtnBlue.OnInteract += delegate () { SamBtnPress(blue); return false; };
        SamBtnYellow.OnInteract += delegate () { SamBtnPress(yellow); return false; };
        SamBtnGreen.OnInteract += delegate () { SamBtnPress(green); return false; };
        InBtnRed.OnInteract += delegate () { InBtnPress("Input Button Red", RedInput); return false; };
        InBtnBlue.OnInteract += delegate () { InBtnPress("Input Button Blue", BlueInput); return false; };
        InBtnYellow.OnInteract += delegate () { InBtnPress("Input Button Yellow", YellowInput); return false; };
        InBtnGreen.OnInteract += delegate () { InBtnPress("Input Button Green", GreenInput); return false; };
    }

    void Start()
    {
        BindSoundsToBtn();
        SetupOrder();
    }

    void BindSoundsToBtn()
    {
        for (int i = 0; i < 4; i++)
        {
            int index = UnityEngine.Random.Range(0, 12);
            while (selectedIndices.Contains(index))
            {
                index = UnityEngine.Random.Range(0, 12);
            }
            selectedIndices.Add(index);
            selectedNumbers[i] = index;
        }

        selectedIndices.Clear();

        red = selectedNumbers[0];
        blue = selectedNumbers[1];
        yellow = selectedNumbers[2];
        green = selectedNumbers[3];
    }

    void SetupOrder()
    {
        gameLength = UnityEngine.Random.Range(3, 6);

        stage = new List<int>[gameLength];

        for (int i = 0; i < gameLength; i++)
        {
            if (i > 0)
            {
                stage[i] = stage[i - 1].ToList();
            }
            else
            {
                stage[i] = new List<int>();
            }
            stage[i].Add(UnityEngine.Random.Range(0, 4));
            Debug.LogFormat(@"[Simon Sounds #{0}] In stage {1}, Simon played: {2}", moduleId, i + 1, string.Join(", ", stage[i].Select(num => colorNames[num]).ToArray()));
        }
    }

    IEnumerator Beep(bool delay)
    {
        if (delay)
            yield return new WaitForSeconds(3);

        while (true)
        {
            for (int i = 0; i <= currentStage; i++)
            {
                int sound = stage[currentStage][i];

                int soundLog = 0;
                if (sound == 0)
                    soundLog = red;
                else if (sound == 1)
                    soundLog = blue;
                else if (sound == 2)
                    soundLog = yellow;
                else
                    soundLog = green;

                Audio.PlaySoundAtTransform(Sounds[soundLog].name, transform);
                Debug.Log(soundLog);
                yield return new WaitForSeconds(.5f);
            }

            yield return new WaitForSeconds(3);
        }
    }

    void SamBtnPress(int btnPressed)
    {
        Audio.PlaySoundAtTransform(Sounds[btnPressed].name, transform);
        Debug.Log(btnPressed);
        if (beep == null)
            beep = StartCoroutine(Beep(delay: true));
    }

    void InBtnPress(string logMessage, int btnPressed)
    {
        if (moduleSolved)
            return;
        Debug.Log(logMessage);
        timesPressed += 1;
        Match(btnPressed);
        if (beep != null)
            StopCoroutine(beep);
        beep = StartCoroutine(Beep(delay: true));
    }

    void CheckNextStage()
    {
        if (timesPressed < currentStage)
        {
            return;
        }
        else
        {
            currentStage++;
            if (currentStage > (gameLength - 1))
            {
                StopAllCoroutines();
                GetComponent<KMBombModule>().HandlePass();
                moduleSolved = true;
            }
            timesPressed = -1;
            return;
        }

    }


    void Match(int btnPressed)
    {
        int SimonsPress = stage[currentStage][timesPressed];

        var inCon = Enumerable.Range(0, inputConditions.Length).First(i => inputConditions[i].Eval(Bomb));
        if (inCon != lastInCon)
        {
            Debug.LogFormat(@"[Simon Sounds #{0}] Input condition: {1}", moduleId, inputConditions[inCon].Explanation);
            lastInCon = inCon;
        }
        var playerInputColor = Array.IndexOf(table2[inCon], btnPressed);
        var samCon = Enumerable.Range(0, sampleConditions.Length).First(i => sampleConditions[i].Eval(Bomb));
        if (samCon != lastSamCon)
        {
            Debug.LogFormat(@"[Simon Sounds #{0}] Sample condition: {1}", moduleId, sampleConditions[samCon].Explanation);
            lastSamCon = samCon;
        }

        var inLogic = Array.IndexOf(table1[samCon], playerInputColor);

        if (inLogic == SimonsPress)
        {
            int soundIn = 0;

            if (inLogic == 0)
                soundIn = red;
            else if (inLogic == 1)
                soundIn = blue;
            else if (inLogic == 2)
                soundIn = yellow;
            else
                soundIn = green;

            CheckNextStage();
            Audio.PlaySoundAtTransform(Sounds[soundIn].name, transform);

        }
        else
        {
            GetComponent<KMBombModule>().HandleStrike();
            timesPressed = -1;
        }

        return;

    }
}