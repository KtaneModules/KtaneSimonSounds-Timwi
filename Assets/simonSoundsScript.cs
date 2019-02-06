using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using Random = UnityEngine.Random;
using System.Text.RegularExpressions;

public class simonSoundsScript : MonoBehaviour
{
    public KMAudio Audio;
    public KMBombInfo Bomb;

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    public KMSelectable[] SamBtns;
    public KMSelectable[] InBtns;
    public Light[] SamLights;
    public AudioClip[] Sounds;

    public Material[] LedsOn;
    public Material[] LedsOff;

    private readonly int[] selectedNumbers = new int[4] { 0, 0, 0, 0 };
    private int lastInCon = -1;
    private int lastSamCon = -1;
    private const int RedInput = 0;
    private const int BlueInput = 1;
    private const int YellowInput = 2;
    private const int GreenInput = 3;
    private int timesPressed = -1;
    private int gameLength = 0;
    private List<int>[] stage;
    private int currentStage = 0;
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

        for (int i = 0; i < 4; i++)
        {
            SamBtns[i].OnInteract += SamBtnPress(i);
            InBtns[i].OnInteract += InBtnPress(i);
        }
    }

    private KMSelectable.OnInteractHandler InBtnPress(int btnPressed)
    {
        return delegate
        {
            InBtns[btnPressed].AddInteractionPunch();
            if (moduleSolved)
                return false;
            InBtns[btnPressed].AddInteractionPunch();
            Debug.LogFormat(@"[Simon Sounds #{0}] You pressed {1}", moduleId, colorNames[btnPressed]);
            timesPressed += 1;
            Match(btnPressed);
            if (beep != null)
                StopCoroutine(beep);
            StartCoroutine(BlinkIn(btnPressed));
            beep = StartCoroutine(Beep(delay: true));
            return false;
        };
    }

    private KMSelectable.OnInteractHandler SamBtnPress(int btnPressed)
    {
        return delegate ()
        {
            SamBtns[btnPressed].AddInteractionPunch();
            Audio.PlaySoundAtTransform(Sounds[selectedNumbers[btnPressed]].name, transform);
            SamBtns[btnPressed].AddInteractionPunch(.5f);
            if (beep == null)
                beep = StartCoroutine(Beep(delay: true));
            StartCoroutine(BlinkLight(btnPressed));
            return false;
        };
    }

    private IEnumerator BlinkLight(int btnPressed)
    {
        SamBtns[btnPressed].GetComponent<MeshRenderer>().sharedMaterial = LedsOn[btnPressed];
        SamLights[btnPressed].gameObject.SetActive(true);
        yield return new WaitForSeconds(.3f);
        SamBtns[btnPressed].GetComponent<MeshRenderer>().sharedMaterial = LedsOff[btnPressed];
        SamLights[btnPressed].gameObject.SetActive(false);
    }

    private IEnumerator BlinkIn(int btnPressed)
    {
        InBtns[btnPressed].GetComponent<MeshRenderer>().sharedMaterial = LedsOn[btnPressed];
        yield return new WaitForSeconds(.3f);
        InBtns[btnPressed].GetComponent<MeshRenderer>().sharedMaterial = LedsOff[btnPressed];
    }

    private IEnumerator BlinkLights()
    {
        for (int i = 0; i < 4; i++)
        {
            SamLights[i].gameObject.SetActive(true);
            SamBtns[i].GetComponent<MeshRenderer>().sharedMaterial = LedsOn[i];
        }
        yield return new WaitForSeconds(.3f);
        for (int i = 0; i < 4; i++)
        {
            SamLights[i].gameObject.SetActive(false);
            SamBtns[i].GetComponent<MeshRenderer>().sharedMaterial = LedsOff[i];
        }
    }

    void Start()
    {
        BindSoundsToBtn();
        SetupOrder();
        for (int i = 0; i < 4; i++)
        {
            SamBtns[i].GetComponent<MeshRenderer>().sharedMaterial = LedsOff[i];
            InBtns[i].GetComponent<MeshRenderer>().sharedMaterial = LedsOff[i];
        }
    }

    void BindSoundsToBtn()
    {
        var soundNumbers = Enumerable.Range(0, Sounds.Length).ToList();

        for (int i = 0; i < 4; i++)
        {
            int index = Random.Range(0, soundNumbers.Count);
            selectedNumbers[i] = soundNumbers[index];
            soundNumbers.RemoveAt(index);
        }
    }

    void SetupOrder()
    {
        gameLength = Random.Range(3, 6);

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
            stage[i].Add(Random.Range(0, 4));
            Debug.LogFormat(@"[Simon Sounds #{0}] In stage {1}, Simon played: {2}", moduleId, i + 1, string.Join(", ", stage[i].Select(num => colorNames[num]).ToArray()));
        }
    }

    IEnumerator Beep(bool delay)
    {
        if (delay)
            yield return new WaitForSeconds(3);

        while (!moduleSolved)
        {
            for (int i = 0; i <= currentStage; i++)
            {
                Audio.PlaySoundAtTransform(Sounds[selectedNumbers[stage[currentStage][i]]].name, transform);
                for (int j = 0; j < 4; j++)
                    StartCoroutine(BlinkLights());
                yield return new WaitForSeconds(.5f);
            }

            yield return new WaitForSeconds(3);
        }
    }

    void CheckNextStage()
    {
        if (timesPressed >= currentStage)
        {
            currentStage++;
            if (currentStage > (gameLength - 1))
            {
                StopAllCoroutines();
                GetComponent<KMBombModule>().HandlePass();
                moduleSolved = true;
            }
            timesPressed = -1;
        }
    }

    void Match(int btnPressed)
    {

        int SimonsPress = stage[currentStage][timesPressed];

        var inCon = Enumerable.Range(0, inputConditions.Length).First(i => inputConditions[i].Eval(Bomb));
        var anyChange = false;
        if (inCon != lastInCon)
        {
            Debug.LogFormat(@"[Simon Sounds #{0}] Input condition: {1}", moduleId, inputConditions[inCon].Explanation);
            lastInCon = inCon;
            anyChange = true;
        }
        var playerInputColor = Array.IndexOf(table2[inCon], btnPressed);
        var samCon = Enumerable.Range(0, sampleConditions.Length).First(i => sampleConditions[i].Eval(Bomb));
        if (samCon != lastSamCon)
        {
            Debug.LogFormat(@"[Simon Sounds #{0}] Sample condition: {1}", moduleId, sampleConditions[samCon].Explanation);
            lastSamCon = samCon;
            anyChange = true;
        }

        if (anyChange)
        {
            Debug.LogFormat(@"[Simon Sounds #{0}] Expected button presses:", moduleId);
            for (int st = currentStage; st < gameLength; st++)
            {
                Debug.LogFormat(@"[Simon Sounds #{0}] — Stage {1}: {2}", moduleId, st + 1, string.Join(", ", Enumerable.Range(0, st + 1).Select(i =>
                {
                    var sc = Enumerable.Range(0, sampleConditions.Length).First(j => sampleConditions[j].Eval(Bomb));
                    var ic = Enumerable.Range(0, inputConditions.Length).First(j => inputConditions[j].Eval(Bomb));
                    return colorNames[table2[ic][table1[sc][stage[st][i]]]];
                }).ToArray()));
            }
        }

        var inLogic = Array.IndexOf(table1[samCon], playerInputColor);

        if (inLogic == SimonsPress)
        {
            CheckNextStage();
            Audio.PlaySoundAtTransform(Sounds[selectedNumbers[inLogic]].name, transform);
        }
        else
        {
            GetComponent<KMBombModule>().HandleStrike();
            timesPressed = -1;
        }
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} sample B R G Y [play the sample buttons; B=blue, R=red, G=green, Y=yellow] | !{0} input B R G Y [press the input buttons]";
#pragma warning restore 414

    private List<KMSelectable> ProcessTwitchCommand(string command)
    {
        var list = new List<KMSelectable>();
        Match m;
        if ((m = Regex.Match(command, @"^\s*(?:(?<sample>sample|sam|listen|play|hear)|press|in|pr|submit|input)\s+(?<buttons>[BRGY ]+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
        {
            var buttons = m.Groups["buttons"].Value;
            var isSampleButtons = m.Groups["sample"].Success;
            var colors = "RBYG";
            foreach (var ch in buttons.ToUpperInvariant())
            {
                var pos = colors.IndexOf(ch);
                if (pos != -1)
                    list.Add((isSampleButtons ? SamBtns : InBtns)[pos]);
            }
            return list;
        }
        return null;
    }

    IEnumerator TwitchHandleForcedSolve()
    {

        Debug.LogFormat(@"[Simon Sounds #{0}] Module was force solved by TP.", moduleId);

        var sc = Enumerable.Range(0, sampleConditions.Length).First(j => sampleConditions[j].Eval(Bomb));
        var ic = Enumerable.Range(0, inputConditions.Length).First(j => inputConditions[j].Eval(Bomb));
        var sol = stage[gameLength - 1].Select(i => colorNames[table2[ic][table1[sc][i]]]).ToList();
        int[] solInt = new int[gameLength];

        for (int i = 0; i < gameLength; i++)
            solInt[i] = sol[i] == "red" ? 0 : sol[i] == "blue" ? 1 : sol[i] == "yellow" ? 2 : sol[i] == "green" ? 3 : -1;

        var solsToPress =
            gameLength == 3 ? new[] { 0, 0, 1, 0, 1, 2 } :
            gameLength == 4 ? new[] { 0, 0, 1, 0, 1, 2, 0, 1, 2, 3 } :
            new[] { 0, 0, 1, 0, 1, 2, 0, 1, 2, 3, 0, 1, 2, 3, 4 };

        foreach (var ix in solsToPress)
        {
            InBtns[solInt[ix]].OnInteract();
            yield return new WaitForSeconds(0.3f);
        }
    }

}