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
        [Serializable]
        public sealed class ObjectArtwork
        {
            public string Label;
            public Sprite Image;
        }
        public TMP_FontAsset Font;
        public Transform View;
        [Tooltip("Optional illustrations keyed by detector label, e.g. cup or book. Camera frames are never saved as artwork.")]
        public ObjectArtwork[] Artwork = Array.Empty<ObjectArtwork>();
        public Action ScanRequested, DemoRequested, MakeRequested, NextRequested, ClearRequested;
        public Action<string> ObjectSelected;
        public Action<int> ObjectRemoved, TileSelected;
        readonly Color ink = new Color(.93f, .97f, 1f);
        readonly Color mint = new Color(.42f, .94f, .82f);
        readonly Color indigo = new Color(.36f, .40f, .83f);
        readonly Color dark = new Color(.04f, .12f, .19f);
        readonly Button[] candidates = new Button[8];
        readonly Button[] selected = new Button[3];
        readonly Button[] tiles = new Button[8];
        readonly Image[] artworkImages = new Image[3];
        readonly GameObject[] placeholders = new GameObject[3];
        readonly string[] candidateLabels = new string[8];
        GameObject selectionPanel, puzzlePanel;
        TMP_Text status, clue, formedWord, puzzleObjects, mode;
        Button make, next, clear;

        void Awake()
        {
            GetComponent<RectTransform>().sizeDelta = new Vector2(900, 740);
            Surface(transform, "Backdrop", 0, 0, 900, 740, 42,
                new Color(.035f, .13f, .17f, .86f), new Color(.09f, .09f, .23f, .86f));
            Surface(transform, "Accent", 36, 22, 828, 5, 2, mint, indigo);
            Label(transform, "RoomSpell", 36, 40, 500, 48, 38, ink);
            mode = Label(transform, "CAMERA OFF", 594, 49, 270, 34, 21, mint);
            mode.alignment = TextAlignmentOptions.Right;
            status = Label(transform, "Find three things that belong together.", 36, 98, 828, 58, 26, ink);
            status.overflowMode = TextOverflowModes.Overflow;
            CreateButton(transform, "Scan", 36, 176, 270, 56, () => ScanRequested?.Invoke(), true);
            CreateButton(transform, "Recenter", 324, 176, 270, 56, PlaceInFront);
            var demo = CreateButton(transform, "Try demo", 612, 176, 252, 56, () => DemoRequested?.Invoke());
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            demo.gameObject.SetActive(false);
#endif
            selectionPanel = Panel("Choose objects");
            Label(selectionPanel.transform, "YOUR THREE OBJECTS", 36, 256, 800, 30, 20, mint);
            for (var i = 0; i < selected.Length; i++)
            {
                var index = i;
                selected[i] = CreateButton(selectionPanel.transform, "Object " + (i + 1),
                    36 + i * 282, 300, 264, 128, () => ObjectRemoved?.Invoke(index));
                var label = selected[i].GetComponentInChildren<TMP_Text>();
                label.rectTransform.anchoredPosition = new Vector2(16, -88);
                label.rectTransform.sizeDelta = new Vector2(232, 32);
                label.fontSize = 24;
                var art = new GameObject("Object artwork", typeof(RectTransform), typeof(Image));
                Rectangle(art, selected[i].transform, 100, 14, 64, 64);
                artworkImages[i] = art.GetComponent<Image>();
                artworkImages[i].preserveAspect = true;
                artworkImages[i].raycastTarget = false;
                artworkImages[i].enabled = false;
                placeholders[i] = new GameObject("Placeholder art", typeof(RectTransform));
                Rectangle(placeholders[i], selected[i].transform, 100, 14, 64, 64);
                var parent = placeholders[i].transform;
                Surface(parent, "Halo", 0, 0, 64, 64, 32, new Color(.23f, .45f, .53f, .7f), indigo);
                Surface(parent, "Spark vertical", 28, 10, 8, 44, 4, mint, ink);
                Surface(parent, "Spark horizontal", 10, 28, 44, 8, 4, ink, mint);
                Surface(parent, "Spark center", 23, 23, 18, 18, 9, ink, ink);
                selected[i].interactable = false;
            }
            for (var i = 0; i < candidates.Length; i++)
            {
                var index = i;
                candidates[i] = CreateButton(selectionPanel.transform, "",
                    36 + i % 4 * 211, 452 + i / 4 * 60, 195, 50,
                    () => ObjectSelected?.Invoke(candidateLabels[index]));
                candidates[i].gameObject.SetActive(false);
            }
            CreateButton(selectionPanel.transform, "+ Pen (manual)", 36, 582, 405, 48, () => ObjectSelected?.Invoke("pen"));
            CreateButton(selectionPanel.transform, "+ Notebook (manual)", 459, 582, 405, 48, () => ObjectSelected?.Invoke("notebook"));
            make = CreateButton(selectionPanel.transform, "Play", 36, 656, 828, 58, () => MakeRequested?.Invoke(), true);
            make.interactable = false;
            puzzlePanel = Panel("Solve word");
            clue = Label(puzzlePanel.transform, "", 48, 282, 804, 96, 32, ink);
            clue.alignment = TextAlignmentOptions.Center;
            Surface(puzzlePanel.transform, "Word tray", 36, 398, 828, 82, 24,
                new Color(.08f, .24f, .28f, .92f), new Color(.17f, .19f, .39f, .92f));
            formedWord = Label(puzzlePanel.transform, "", 48, 405, 804, 68, 46, mint);
            formedWord.alignment = TextAlignmentOptions.Center;
            for (var i = 0; i < tiles.Length; i++)
            {
                var index = i;
                tiles[i] = CreateButton(puzzlePanel.transform, "", 36 + i * 105, 510, 93, 76, () => TileSelected?.Invoke(index), true);
                tiles[i].GetComponentInChildren<TMP_Text>().fontSize = 36;
            }
            clear = CreateButton(puzzlePanel.transform, "Retry", 36, 616, 405, 58, () => ClearRequested?.Invoke());
            next = CreateButton(puzzlePanel.transform, "Next word", 459, 616, 405, 58, () => NextRequested?.Invoke(), true);
            puzzleObjects = Label(puzzlePanel.transform, "", 36, 690, 828, 30, 21, ink);
            puzzleObjects.alignment = TextAlignmentOptions.Center;
            puzzlePanel.SetActive(false);
        }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(.7f);
            PlaceInFront();
        }

        public void PlaceInFront()
        {
            if (!View) return;
            var forward = Vector3.ProjectOnPlane(View.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
            transform.SetPositionAndRotation(View.position + forward * 1.4f - Vector3.up * .08f,
                Quaternion.LookRotation(forward, Vector3.up));
        }

        public void SetStatus(string text) => status.text = text;
        public void SetMode(string text) => mode.text = text;

        public void ShowCandidates(ObjectObservation[] observations)
        {
            // Keep a pointing target in its slot when inference confidence changes.
            for (var i = 0; i < candidateLabels.Length; i++)
                if (!observations.Any(x => x.Label == candidateLabels[i])) candidateLabels[i] = null;
            foreach (var observation in observations)
            {
                if (candidateLabels.Contains(observation.Label)) continue;
                var empty = Array.IndexOf(candidateLabels, null);
                if (empty < 0) break;
                candidateLabels[empty] = observation.Label;
            }
            for (var i = 0; i < candidates.Length; i++)
            {
                var exists = candidateLabels[i] != null;
                candidates[i].gameObject.SetActive(exists);
                candidates[i].interactable = exists;
                candidates[i].GetComponentInChildren<TMP_Text>().text = candidateLabels[i] ?? "";
            }
        }

        public void ShowSelection(IReadOnlyList<string> labels)
        {
            selectionPanel.SetActive(true);
            puzzlePanel.SetActive(false);
            for (var i = 0; i < selected.Length; i++)
            {
                var exists = i < labels.Count;
                selected[i].interactable = exists;
                selected[i].GetComponentInChildren<TMP_Text>().text = exists ? labels[i] + "  ×" : "Object " + (i + 1);
                var sprite = exists ? Artwork?.FirstOrDefault(x => x != null &&
                    string.Equals(x.Label, labels[i], StringComparison.OrdinalIgnoreCase))?.Image : null;
                artworkImages[i].sprite = sprite;
                artworkImages[i].enabled = sprite != null;
                placeholders[i].SetActive(sprite == null);
            }
            make.interactable = labels.Count == 3;
        }

        public void ShowRound(PuzzleRound round, bool[] used)
        {
            selectionPanel.SetActive(false);
            puzzlePanel.SetActive(true);
            clue.text = round.Clue;
            formedWord.text = string.Join(" ", round.FormedWord.ToCharArray());
            puzzleObjects.text = string.Join("  ·  ", round.Objects.Select(x => x.ToLowerInvariant()));
            var left = (900 - (round.Tiles.Length * 105 - 12)) * .5f;
            for (var i = 0; i < tiles.Length; i++)
            {
                tiles[i].gameObject.SetActive(i < round.Tiles.Length);
                if (i >= round.Tiles.Length) continue;
                tiles[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(left + i * 105, -510);
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

        RoomSpellSurface Surface(Transform parent, string name, float x, float y, float width, float height,
            float radius, Color left, Color right)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(RoomSpellSurface));
            Rectangle(child, parent, x, y, width, height);
            var graphic = child.GetComponent<RoomSpellSurface>();
            graphic.Radius = radius;
            graphic.Left = left;
            graphic.Right = right;
            graphic.raycastTarget = false;
            return graphic;
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

        Button CreateButton(Transform parent, string text, float x, float y, float width, float height, Action click, bool primary = false)
        {
            var image = Surface(parent, text, x, y, width, height, 18,
                primary ? mint : new Color(.10f, .24f, .29f, .94f),
                primary ? new Color(.49f, .78f, .94f) : new Color(.20f, .22f, .40f, .94f));
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.7f, .9f, .9f);
            colors.disabledColor = new Color(.65f, .7f, .8f, .65f);
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => click());
            var label = Label(image.transform, text, 8, 0, width - 16, height, 25, primary ? dark : ink);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }
    }
}
