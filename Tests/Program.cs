using System;
using System.Collections.Generic;
using System.Linq;
using RoomSpell;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

Check(PuzzleRound.TryCreate(new[] { "cup", "pen", "notebook" }, "study", "A focused activity", 7, out var round), "valid round");
Check(new string(round.Tiles) != round.Answer, "answer must be jumbled");
for (var slot = 0; slot < round.Answer.Length; slot++)
    Check(round.Place(Array.IndexOf(round.Tiles, round.Answer[slot]), slot), "tile placement");
Check(round.IsSolved, "solved round");
round.ClearSlot(0);
Check(!round.IsSolved, "clearing a slot removes the answer");
Check(!PuzzleRound.TryCreate(new[] { "cup", "pen", "notebook" }, "cup", "Same object", 7, out _), "reject object answer");
Check(!PuzzleRound.TryCreate(new[] { "cup", "cup", "notebook" }, "study", "Repeated object", 7, out _), "reject repeated objects");
var seen = new HashSet<string>();
for (var i = 0; i < 4; i++)
{
    Check(WordCatalog.TryNext(new[] { "cup", "pen", "notebook" }, seen, i, out round), "new study word");
    Check(seen.Add(round.Answer), "do not repeat answers");
}
Check(!WordCatalog.TryNext(new[] { "cup", "pen", "notebook" }, seen, 0, out _), "exhausted set");
Check(!WordCatalog.TryNext(new[] { "cup", "suitcase", "oven" }, new HashSet<string>(), 0, out _), "do not invent relationships");
Check(PuzzleRound.TryCreate(new[] { "cup", "bowl", "spoon" }, "RECIPE", "Meal instructions", 3, out round), "repeated letters round");
var occupied = new HashSet<int>();
for (var slot = 0; slot < round.Answer.Length; slot++)
{
    var tile = Enumerable.Range(0, round.Tiles.Length).First(i => !occupied.Contains(i) && round.Tiles[i] == round.Answer[slot]);
    occupied.Add(tile);
    Check(round.Place(tile, slot), "place repeated letters using distinct tiles");
}
Check(round.IsSolved, "solve repeated letters");
var detections = new DetectionCandidates();
var observations = new[] { new ObjectObservation("Cup", 0.9f), new ObjectObservation("cup", 0.7f),
    new ObjectObservation("book", 0.2f), new ObjectObservation("chair", float.NaN) };
Check(detections.Update(observations).Length == 0, "duplicates in one frame are not stable");
var stable = detections.Update(observations);
Check(stable.Length == 1 && stable[0].Label == "cup" && stable[0].Confidence == 0.9f, "stable confident unique object");
Check(detections.Update(Array.Empty<ObjectObservation>()).Length == 0, "clear absent detections");
Check(detections.Update(observations).Length == 0, "require confirmation after disappearance");
Console.WriteLine("Puzzle, word catalog and detection checks passed");
for (var scenario = 0; scenario < 3; scenario++)
{
    var scenarioFilter = new DetectionCandidates();
    var labels = DummyScenarios.Labels(scenario);
    var batch = labels.Select(label => new ObjectObservation(label, 0.95f)).ToArray();
    Check(scenarioFilter.Update(batch).Length == 0, "dummy requires a second frame");
    Check(scenarioFilter.Update(batch).Length == 3, "all dummy labels use shared confirmation");
    Check(WordCatalog.TryNext(labels, new HashSet<string>(), 0, out _) == (scenario < 2), "dummy theme validation");
}
var boundaryFilter = new DetectionCandidates();
var boundaryBatch = new[] { new ObjectObservation("cup", 0.5f), new ObjectObservation("book", float.PositiveInfinity) };
boundaryFilter.Update(boundaryBatch);
Check(boundaryFilter.Update(boundaryBatch).Length == 1, "confidence boundary and infinity rejected");
Console.WriteLine("Dummy scenarios and confidence boundary checks passed");
Check(WordCatalog.TryNext(new[] { "chair", "tv monitor", "clock" }, new HashSet<string>(), 0, out _), "real model room labels share a theme");
Check(WordCatalog.TryNext(new[] { "sofa", "tv monitor", "remote" }, new HashSet<string>(), 0, out _), "COCO label names are supported");
