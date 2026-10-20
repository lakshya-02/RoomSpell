using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoomSpell
{
    public sealed class RoomSpellBoard : MonoBehaviour
    {
        public TMP_FontAsset Font;
        public Transform View;
        public Action ScanRequested, DemoRequested, MakeRequested, NextRequested, ClearRequested;
        public Action<string> ObjectSelected;
        public Action<int> ObjectRemoved, TileSelected;
        readonly Color ink = new Color(0.94f, 0.96f, 0.92f);
        readonly Color teal = new Color(0.06f, 0.30f, 0.31f);
        readonly Color gold = new Color(1f, 0.67f, 0.25f);
        readonly Button[] candidates = new Button[8];
        readonly Button[] selected = new Button[3];
        readonly Button[] tiles = new Button[8];
        readonly string[] candidateLabels = new string[8];
        GameObject selectionPanel, puzzlePanel;
        TMP_Text status, clue, formedWord, puzzleObjects, mode;
        Button make, next, clear;

        void Awake()
        {
            GetComponent<RectTransform>().sizeDelta = new Vector2(900, 740);
            var background = gameObject.AddComponent<Image>();
            background.color = new Color(0.025f, 0.10f, 0.12f, 0.97f);
            background.raycastTarget = false;
            Label(transform, "ROOMSPELL", 24, 20, 550, 50, 42, gold);
            mode = Label(transform, "CAMERA OFF", 584, 32, 292, 35, 22, gold);
            Label(transform, "Turn your surroundings into a word puzzle", 24, 77, 840, 42, 26, ink);
            status = Label(transform, "Scan your room and choose three related objects.", 24, 132, 840, 62, 25, ink);
            CreateButton(transform, "Scan room", 24, 211, 267, 58, () => ScanRequested?.Invoke());
            var demo = CreateButton(transform, "Dummy scenarios", 310, 211, 267, 58, () => DemoRequested?.Invoke());
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            demo.gameObject.SetActive(false);
#endif
            CreateButton(transform, "Recenter board", 596, 211, 280, 58, PlaceInFront);

            selectionPanel = Panel("Choose objects");
            Label(selectionPanel.transform, "CHOOSE 3 OBJECTS", 24, 287, 840, 40, 24, gold);
            for (var i = 0; i < candidates.Length; i++)
            {
                var index = i;
                candidates[i] = CreateButton(selectionPanel.transform, "Look around", 24 + (i % 4) * 215,
                    339 + (i / 4) * 65, 200, 55, () => ObjectSelected?.Invoke(candidateLabels[index]));
                candidates[i].interactable = false;
            }
            CreateButton(selectionPanel.transform, "Add pen (manual)", 24, 477, 410, 55, () => ObjectSelected?.Invoke("pen"));
            CreateButton(selectionPanel.transform, "Add notebook (manual)", 452, 477, 424, 55, () => ObjectSelected?.Invoke("notebook"));
            for (var i = 0; i < selected.Length; i++)
            {
                var index = i;
                selected[i] = CreateButton(selectionPanel.transform, "Empty", 24 + i * 288, 552, 274, 55,
                    () => ObjectRemoved?.Invoke(index));
                selected[i].interactable = false;
            }
            make = CreateButton(selectionPanel.transform, "Make my puzzle", 24, 631, 852, 64, () => MakeRequested?.Invoke());
            make.interactable = false;

            puzzlePanel = Panel("Solve word");
            clue = Label(puzzlePanel.transform, "", 24, 297, 852, 90, 30, ink);
            formedWord = Label(puzzlePanel.transform, "", 24, 400, 852, 68, 48, gold);
            formedWord.alignment = TextAlignmentOptions.Center;
            for (var i = 0; i < tiles.Length; i++)
            {
                var index = i;
                tiles[i] = CreateButton(puzzlePanel.transform, "", 24 + i * 108, 484, 96, 76, () => TileSelected?.Invoke(index));
                tiles[i].GetComponentInChildren<TMP_Text>().fontSize = 36;
            }
            clear = CreateButton(puzzlePanel.transform, "Clear letters", 24, 589, 410, 60, () => ClearRequested?.Invoke());
            next = CreateButton(puzzlePanel.transform, "Another word", 452, 589, 424, 60, () => NextRequested?.Invoke());
            puzzleObjects = Label(puzzlePanel.transform, "", 24, 674, 852, 40, 23, ink);
            puzzlePanel.SetActive(false);
        }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(0.7f);
            PlaceInFront();
        }

        public void PlaceInFront()
        {
            if (!View) return;
            var forward = Vector3.ProjectOnPlane(View.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
            transform.SetPositionAndRotation(View.position + forward * 1.4f - Vector3.up * 0.08f,
                Quaternion.LookRotation(forward, Vector3.up));
        }

        public void SetStatus(string text) => status.text = text;
        public void SetMode(string text) => mode.text = text;

        public void ShowCandidates(ObjectObservation[] observations)
        {
            for (var i = 0; i < candidates.Length; i++)
            {
                var exists = i < observations.Length;
                candidateLabels[i] = exists ? observations[i].Label : null;
                candidates[i].interactable = exists;
                candidates[i].GetComponentInChildren<TMP_Text>().text = exists
                    ? $"{observations[i].Label}  {observations[i].Confidence:P0}" : "—";
            }
        }

        public void ShowSelection(IReadOnlyList<string> labels)
        {
            selectionPanel.SetActive(true);
            puzzlePanel.SetActive(false);
            for (var i = 0; i < selected.Length; i++)
            {
                selected[i].interactable = i < labels.Count;
                selected[i].GetComponentInChildren<TMP_Text>().text = i < labels.Count ? labels[i] + " ×" : "Empty";
            }
            make.interactable = labels.Count == 3;
        }

        public void ShowRound(PuzzleRound round, bool[] used)
        {
            selectionPanel.SetActive(false);
            puzzlePanel.SetActive(true);
            clue.text = "CLUE  " + round.Clue;
            formedWord.text = string.Join(" ", round.FormedWord.ToCharArray());
            puzzleObjects.text = "Inspired by " + string.Join(" + ", round.Objects.Select(x => x.ToLowerInvariant()));
            for (var i = 0; i < tiles.Length; i++)
            {
                tiles[i].gameObject.SetActive(i < round.Tiles.Length);
                if (i >= round.Tiles.Length) continue;
                tiles[i].GetComponentInChildren<TMP_Text>().text = round.Tiles[i].ToString();
                tiles[i].interactable = !used[i] && !round.IsFull;
            }
            next.interactable = round.IsSolved;
            clear.interactable = !round.IsSolved && used.Any(x => x);
        }

        GameObject Panel(string name)
        {
            var panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return panel;
        }

        static RectTransform Rectangle(GameObject child, Transform parent, float x, float y, float width, float height)
        {
            child.transform.SetParent(parent, false);
            var rect = child.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        TMP_Text Label(Transform parent, string text, float x, float y, float width, float height, float size, Color color)
        {
            var child = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            Rectangle(child, parent, x, y, width, height);
            var label = child.GetComponent<TextMeshProUGUI>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.raycastTarget = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        Button CreateButton(Transform parent, string text, float x, float y, float width, float height, Action click)
        {
            var child = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
            Rectangle(child, parent, x, y, width, height);
            var image = child.GetComponent<Image>();
            image.color = teal;
            var button = child.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => click());
            var label = Label(child.transform, text, 8, 0, width - 16, height, 25, ink);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }
    }
}
