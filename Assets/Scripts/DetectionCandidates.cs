using System;
using System.Collections.Generic;
using System.Linq;

namespace RoomSpell
{
    public readonly struct ObjectObservation
    {
        public readonly string Label;
        public readonly float Confidence;
        public ObjectObservation(string label, float confidence) { Label = label; Confidence = confidence; }
    }

    public sealed class DetectionCandidates
    {
        Dictionary<string, int> consecutive = new Dictionary<string, int>();

        public ObjectObservation[] Update(IEnumerable<ObjectObservation> observations)
        {
            var valid = (observations ?? Array.Empty<ObjectObservation>())
                .Where(x => !string.IsNullOrWhiteSpace(x.Label) && !float.IsNaN(x.Confidence) &&
                    x.Confidence >= 0.5f && x.Confidence <= 1f)
                .Select(x => new ObjectObservation(x.Label.Trim().ToLowerInvariant(), x.Confidence))
                .Where(x => WordCatalog.IsUsefulObject(x.Label))
                .GroupBy(x => x.Label).Select(g => g.OrderByDescending(x => x.Confidence).First()).ToArray();
            var next = new Dictionary<string, int>();
            foreach (var item in valid)
                next[item.Label] = consecutive.TryGetValue(item.Label, out var hits) ? Math.Min(hits + 1, 2) : 1;
            consecutive = next;
            return valid.Where(x => consecutive[x.Label] == 2).OrderByDescending(x => x.Confidence).Take(8).ToArray();
        }
    }
}
