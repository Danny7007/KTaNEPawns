using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;


public class PawnsScript : MonoBehaviour
{

    public KMBombInfo Bomb;
    public KMAudio Audio;

    public SpriteRenderer[] sprites;
    public TextMesh[] coordTexts;
    public GameObject[] leds;
    public GameObject[] arrowLeds;
    public Sprite[] pieceIcons;
    public Material[] ledColors;

    public KMSelectable[] buttons;
    public Coroutine[] buttonMovements = new Coroutine[4];
    int selectedIndex = 0;

    private int[,] chessboard = new int[8, 8] { { -1, -1, -1, -1, -1, -1, -1, -1 }, { -1, -1, -1, -1, -1, -1, -1, -1 }, { -1, -1, -1, -1, -1, -1, -1, -1 }, { -1, -1, -1, -1, -1, -1, -1, -1 }, { -1, -1, -1, -1, -1, -1, -1, -1 }, { -1, -1, -1, -1, -1, -1, -1, -1 }, { -1, -1, -1, -1, -1, -1, -1, -1 }, { -1, -1, -1, -1, -1, -1, -1, -1 } };
    
    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    bool[] correctAnswers = new bool[8];
    bool[] submitted = new bool[8];

    ChessPiece[] piecesOnBoard = new ChessPiece[8];
    ChessPiece[] pawns = new ChessPiece[8];

    private List<int> allNumbers, numbers = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63 };
    //This above list is taken from wikipedia, an untrustworthy source. There may be non-numbers within this list. Please refer to ktane.timwi.de/HTML/Number%20Checker.html for further information

    void Awake ()
    {
        moduleId = moduleIdCounter++;
        buttons[0].OnInteract += delegate () { ArrowPress(7);      return false; };
        buttons[1].OnInteract += delegate () { ArrowPress(1);      return false; };
        buttons[2].OnInteract += delegate () { SubmitPress(true);  return false; };
        buttons[3].OnInteract += delegate () { SubmitPress(false); return false; };
        for (int i = 0; i < 4; i++)
        {
            int j = i;
            buttons[j].OnInteract += delegate ()
            {
                if (buttonMovements[j] != null) StopCoroutine(buttonMovements[j]);
                buttonMovements[j] = StartCoroutine(ButtonMove(j));
                return false;
            };
        }
    }

    void Start ()
    {
        PlaceBlockers();
        PlacePieces();
        GetAnswers();
        DoLogging();
        DisplayThings();
    }

    void PlaceBlockers()
    {
        int[] snletters = Bomb.GetSerialNumberLetters().Select(x => (x - 'A') % 8).ToArray();
        int[] snnumbers = Bomb.GetSerialNumberNumbers().Select(x => 7 - ((x + 7) % 8)).ToArray();
        for (int i = 0; i < Math.Min(snletters.Count(), snnumbers.Count()); i++)
        {
            chessboard[snnumbers[i], snletters[i]] = (int)PieceNames.Blocker;
            numbers.Remove(snnumbers[i] * 8 + snletters[i]);
            Debug.LogFormat("[Pawns #{0}] Blocker placed at {1}-{2}.", moduleId, "abcdefgh"[snletters[i]], snnumbers[i] + 1);
        }
    }

    void PlacePieces()
    {
        for (int i = 0; i < 8; i++)
        {
            int position = numbers.PickRandom();
            numbers.Remove(position);
            int piece = UnityEngine.Random.Range(1, 6);
            chessboard[position / 8, position % 8] = piece;
            piecesOnBoard[i] = new ChessPiece(Coordinate(position), piece);
        }
        for (int i = 0; i < 8; i++)
        {
            int[] pawnPosition = (UnityEngine.Random.Range(0, 5) < 3) ?
                piecesOnBoard[i].GetCaptures(chessboard).Where(x => numbers.Contains(NumPosition(x))).PickRandom() :
                Coordinate(numbers.PickRandom());
            pawns[i] = new ChessPiece(pawnPosition, (int)PieceNames.Pawn);
        }
    }

    void GetAnswers()
    {
        for (int i = 0; i < 8; i++)
        {
            if (piecesOnBoard[i].GetCaptures(chessboard).Select(x => NumPosition(x)).Contains(pawns[i].Row * 8 + pawns[i].Col))
                correctAnswers[i] = true;
            else correctAnswers[i] = false;
        }
    }

    void DoLogging()
    {
        for (int i = 0; i < 8; i++)
        {
            Debug.LogFormat("[Pawns #{0}] Move #{1}: {2} at {3} attempting to capture Pawn at {4}", 
                moduleId, i + 1, piecesOnBoard[i].PieceName, piecesOnBoard[i].GetCoordinate(), pawns[i].GetCoordinate()
                + (correctAnswers[i] ? ". This move is possible." : ". This move cannot be made."));
        }

        Debug.LogFormat("[Pawns #{0}] ", moduleId);
        Debug.LogFormat("[Pawns #{0}] The full chessboard is as follows:", moduleId);

        string gridLogger = string.Empty;
        for (int i = 0; i < 64; i++)
        {
            gridLogger += ".PNBRQKX"[chessboard[i / 8, i % 8] + 1];
            if (i % 8 == 7)
            {
                Debug.LogFormat("[Pawns #{0}] {1}", moduleId, gridLogger);
                gridLogger = string.Empty;
            }
        }
    }

    void DisplayThings()
    {
        coordTexts[0].text = piecesOnBoard[selectedIndex].GetCoordinate();
        coordTexts[1].text = pawns[selectedIndex].GetCoordinate();
        if (submitted[selectedIndex] && correctAnswers[selectedIndex])
        {
            sprites[0].sprite = null;
            sprites[1].sprite = pieceIcons[piecesOnBoard[selectedIndex].PieceType];
        }
        else
        {
            sprites[0].sprite = pieceIcons[piecesOnBoard[selectedIndex].PieceType];
            sprites[1].sprite = pieceIcons[0];
        }
    }

    IEnumerator ButtonMove(int index)
    {
        KMSelectable button = buttons[index];
        button.AddInteractionPunch(0.5f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, button.transform);
        while (button.transform.localPosition.y > -0.007f)
        {
            button.transform.localPosition += 0.1f * Time.deltaTime * Vector3.down;
            yield return null;
        }
        while (button.transform.localPosition.y < 0)
        {
            button.transform.localPosition += 0.1f * Time.deltaTime * Vector3.up;
            yield return null;
        }
    }
    void ArrowPress(int offset)
    {
        arrowLeds[selectedIndex].GetComponent<MeshRenderer>().material = ledColors[0];
        selectedIndex = (selectedIndex + offset) % 8;
        arrowLeds[selectedIndex].GetComponent<MeshRenderer>().material = ledColors[1];
        DisplayThings();
    }
    void SubmitPress(bool input)
    {
        if (moduleSolved || submitted[selectedIndex])
            return;
        if (input == correctAnswers[selectedIndex])
        {
            submitted[selectedIndex] = true;
            leds[selectedIndex].GetComponent<MeshRenderer>().material = ledColors[1];
            Debug.LogFormat("[Pawns #{0}] Submitted move #{1} as a {2}.", moduleId, selectedIndex + 1, input ? "capture" : "pass");
            if (submitted.All(x => x))
                Solve();
        }
        else
        {
            GetComponent<KMBombModule>().HandleStrike();
            Debug.LogFormat("[Pawns #{0}] Submitted move #{1} as a {2}, which is incorrect. Strike incurred.", moduleId, selectedIndex + 1, input ? "capture" : "pass");
        }
        DisplayThings();
    }
    void Solve()
    {
        moduleSolved = true;
        GetComponent<KMBombModule>().HandlePass();
        Debug.LogFormat("[Pawns #{0}] All moves submitted. Module solved.", moduleId);
    }

    int NumPosition(int[] coordinate)
    {
        return coordinate[0] * 8 + coordinate[1];
    }
    int[] Coordinate(int position)
    {
        return new int[] { position / 8, position % 8 };
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use !{0} to do something.";
    #pragma warning restore 414

    IEnumerator ProcessTwitchCommand (string Command)
    {
      yield return null;
    }

    IEnumerator TwitchHandleForcedSolve ()
    {
      yield return null;
    }
}
