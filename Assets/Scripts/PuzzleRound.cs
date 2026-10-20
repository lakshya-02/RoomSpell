using System;
using System.Collections.Generic;
using System.Linq;

namespace RoomSpell
{
    public sealed class PuzzleRound
    {
        public readonly string[] Objects;
        public readonly string Answer;
        public readonly string Clue;
        public readonly char[] Tiles;
        readonly int?[] slots;

        PuzzleRound(string[] objects, string answer, string clue, char[] tiles)
        {
            Objects = objects;
            Answer = answer;
            Clue = clue;
            Tiles = tiles;
            slots = new int?[answer.Length];
        }

        public static bool TryCreate(IEnumerable<string> detectedObjects, string answer, string clue, int seed, out PuzzleRound round)
        {
            round = null;
            if (detectedObjects == null || answer == null || string.IsNullOrWhiteSpace(clue)) return false;

            var objects = detectedObjects.Select(x => x?.Trim().ToUpperInvariant())
                .Where(x => !string.IsNullOrEmpty(x)).Distinct().ToArray();
            answer = answer.Trim().ToUpperInvariant();
            if (objects.Length != 3 || answer.Length < 4 || answer.Length > 8 ||
                !answer.All(c => c >= 'A' && c <= 'Z') || objects.Contains(answer)) return false;

            var tiles = answer.ToCharArray();
            var random = new Random(seed);
            for (var i = tiles.Length - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (tiles[i], tiles[j]) = (tiles[j], tiles[i]);
            }
            if (new string(tiles) == answer)
            {
                var swap = Array.FindIndex(tiles, 1, c => c != tiles[0]);
                if (swap < 0) return false;
                (tiles[0], tiles[swap]) = (tiles[swap], tiles[0]);
            }
            round = new PuzzleRound(objects, answer, clue.Trim(), tiles);
            return true;
        }

        public bool Place(int tileIndex, int slotIndex)
        {
            if (tileIndex < 0 || tileIndex >= Tiles.Length || slotIndex < 0 || slotIndex >= slots.Length ||
                slots[slotIndex].HasValue) return false;
            if (slots.Contains(tileIndex)) return false;
            slots[slotIndex] = tileIndex;
            return true;
        }

        public void ClearSlot(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < slots.Length) slots[slotIndex] = null;
        }

        public bool IsSolved => slots.All(c => c.HasValue) &&
            new string(slots.Select(c => Tiles[c.Value]).ToArray()) == Answer;

        public string FormedWord => new string(slots.Select(c => c.HasValue ? Tiles[c.Value] : '_').ToArray());
        public bool IsFull => slots.All(c => c.HasValue);
    }
}
