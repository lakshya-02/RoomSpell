using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoomSpell
{
    public sealed class RoomSpellGame : MonoBehaviour
    {
        public RoomSpellDetection Detection;
        public RoomSpellBoard Board;
        public GameObject InteractionRig;
        readonly List<string> selected = new List<string>();
        readonly HashSet<string> usedAnswers = new HashSet<string>();
        PuzzleRound round;
        bool[] usedTiles;
        int nextSlot;
        int score;
        int dummyScenario;
        public PuzzleRound CurrentRound => round;
        public int Score => score;

        void Awake()
        {
            if (!Application.isEditor || UnityEngine.XR.Management.XRGeneralSettings.Instance?.Manager?.activeLoader != null) return;
            InteractionRig.SetActive(false);
            var inputs = UnityEngine.EventSystems.EventSystem.current;
            if (!inputs) inputs = FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            inputs.GetComponent<Oculus.Interaction.PointableCanvasModule>().enabled = false;
            inputs.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().enabled = true;
        }

        void Start()
        {
            Board.ScanRequested = Scan;
            Board.DemoRequested = Demo;
            Board.MakeRequested = MakePuzzle;
            Board.NextRequested = MakePuzzle;
            Board.ClearRequested = ClearLetters;
            Board.ObjectSelected = SelectObject;
            Board.ObjectRemoved = RemoveObject;
            Board.TileSelected = PlaceTile;
            Detection.CandidatesChanged += OnCandidates;
            Detection.StatusChanged += Board.SetStatus;
            Board.ShowSelection(selected);
        }

        void Scan()
        {
            Detection.StopScanning();
            selected.Clear();
            round = null;
            Board.SetMode(Application.isEditor ? "SIMULATOR CAMERA" : "QUEST CAMERA");
            Board.ShowCandidates(Array.Empty<ObjectObservation>());
            Board.ShowSelection(selected);
            Detection.StartScanning();
        }

        void OnCandidates(ObjectObservation[] observations)
        {
            Board.ShowCandidates(observations);
            Board.SetStatus(observations.Length == 0 ? "Looking for related objects. Hold your view steady." :
                "Tap three related objects to create a word puzzle.");
        }

        void Demo()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            StartDummyScenario(dummyScenario++ % 3);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void StartDummyScenario(int scenario)
        {
            Detection.StopScanning();
            selected.Clear();
            round = null;
            Board.SetMode("DUMMY DATA");
            Board.ShowCandidates(Array.Empty<ObjectObservation>());
            Board.ShowSelection(selected);
            Board.SetStatus("DUMMY DATA: " + string.Join(" + ", DummyScenarios.Labels(scenario)) + ". Tap Dummy again for the next scenario.");
            Detection.StartDummy(scenario);
        }
#endif

        void SelectObject(string label)
        {
            if (string.IsNullOrEmpty(label) || selected.Contains(label)) return;
            if (selected.Count == 3)
            {
                Board.SetStatus("Three objects selected. Tap a selected object to remove it.");
                return;
            }
            selected.Add(label);
            Board.ShowSelection(selected);
            Board.SetStatus(label == "pen" || label == "notebook"
                ? $"{label} added manually. Selected {selected.Count}/3."
                : $"Selected {selected.Count}/3. Choose objects with a shared purpose.");
        }

        void RemoveObject(int index)
        {
            if (index < 0 || index >= selected.Count) return;
            selected.RemoveAt(index);
            Board.ShowSelection(selected);
        }

        void MakePuzzle()
        {
            if (!WordCatalog.TryNext(selected, usedAnswers, Environment.TickCount, out var nextRound))
            {
                Board.SetStatus("No new shared word for this set. Scan again and choose three linked objects.");
                return;
            }
            Detection.StopScanning();
            round = nextRound;
            usedTiles = new bool[round.Tiles.Length];
            nextSlot = 0;
            Board.ShowRound(round, usedTiles);
            Board.SetStatus("Use the clue and tap the letters in order to make a new word.");
        }

        void PlaceTile(int index)
        {
            if (round == null || round.IsFull || index < 0 || index >= usedTiles.Length || usedTiles[index]) return;
            if (!round.Place(index, nextSlot)) return;
            nextSlot++;
            usedTiles[index] = true;
            if (round.IsSolved)
            {
                usedAnswers.Add(round.Answer);
                score += 100;
                Board.SetStatus($"Solved! {round.Answer}  ·  Score {score}. Try another word from the same objects.");
            }
            else if (round.IsFull) Board.SetStatus("That order does not match the clue. Clear the letters and try again.");
            Board.ShowRound(round, usedTiles);
        }

        void ClearLetters()
        {
            if (round == null || round.IsSolved) return;
            for (var i = 0; i < round.Tiles.Length; i++) round.ClearSlot(i);
            Array.Clear(usedTiles, 0, usedTiles.Length);
            nextSlot = 0;
            Board.ShowRound(round, usedTiles);
            Board.SetStatus("Try another order. Use the clue above the letters.");
        }

        void OnDestroy()
        {
            if (!Detection) return;
            Detection.CandidatesChanged -= OnCandidates;
            Detection.StatusChanged -= Board.SetStatus;
        }
    }
}
