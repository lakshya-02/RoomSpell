using System;
using System.Collections.Generic;
using System.Linq;

namespace RoomSpell
{
    public static class WordCatalog
    {
        sealed class Theme
        {
            public readonly string[] Objects;
            public readonly string[] Answers;
            public readonly string[] Clues;

            public Theme(string objects, string answers, params string[] clues)
            {
                Objects = objects.Split('|');
                Answers = answers.Split('|');
                Clues = clues;
            }
        }

        // shortcut: curated relationships keep rounds playable offline; replace with validated text generation when a provider is configured.
        static readonly Theme[] themes =
        {
            new Theme("cup|pen|notebook|book|laptop|keyboard|mouse|chair|desk|cell phone", "STUDY|LEARN|FOCUS|CREATE",
                "A focused activity for reading and understanding.", "Gain knowledge or a new skill.",
                "Give your attention to one task.", "Make something new at your workspace."),
            new Theme("cup|bowl|spoon|fork|knife|bottle|banana|apple|orange|oven|toaster|dining table|refrigerator|carrot|broccoli|sink|wine glass|sandwich|pizza|cake", "COOK|RECIPE|DINNER|TASTE",
                "Prepare food to eat.", "Instructions for making a meal.",
                "An evening meal.", "Discover a flavour."),
            new Theme("chair|couch|sofa|tv|tv monitor|remote|clock|book|cup|potted plant|cat|dog", "RELAX|COMFORT|LOUNGE|REST",
                "Take it easy after a busy day.", "A feeling of ease in a familiar place.",
                "Spend time sitting comfortably.", "Take a break and recover your energy."),
            new Theme("backpack|handbag|suitcase|bottle|cell phone|umbrella|car|bicycle|motorbike|bus|train", "TRAVEL|EXPLORE|JOURNEY|PACK",
                "Go from one place to another.", "Discover an unfamiliar place.",
                "A trip that takes you somewhere.", "Get your belongings ready for a trip."),
            new Theme("sports ball|baseball bat|baseball glove|tennis racket|skateboard|surfboard|bicycle|bottle|frisbee|kite", "PLAY|ACTIVE|MOTION|SPORT",
                "Have fun through a game or activity.", "Moving and doing things with energy.",
                "The act of moving.", "A physical game or competition.")
        };

        public static bool IsUsefulObject(string label) => themes.Any(t => t.Objects.Contains(label));

        public static bool TryNext(IEnumerable<string> objects, ISet<string> usedAnswers, int seed, out PuzzleRound round)
        {
            round = null;
            if (objects == null) return false;
            var labels = objects.Select(x => x?.Trim().ToLowerInvariant()).ToArray();
            if (labels.Length != 3 || labels.Any(string.IsNullOrEmpty) || labels.Distinct().Count() != 3) return false;
            var choices = themes.Where(t => labels.All(t.Objects.Contains))
                .SelectMany(t => t.Answers.Select((answer, i) => (answer, clue: t.Clues[i])))
                .Where(x => !labels.Contains(x.answer.ToLowerInvariant()) && !usedAnswers.Contains(x.answer)).ToArray();
            if (choices.Length == 0) return false;
            var choice = choices[new Random(seed).Next(choices.Length)];
            return PuzzleRound.TryCreate(labels, choice.answer, choice.clue, seed, out round);
        }
    }
}
