using System;
using System.Linq;
using System.Reflection;
using RoomSpell;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class RoomSpellUIValidation
{
    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Run in Play mode.");
        var game = UnityEngine.Object.FindAnyObjectByType<RoomSpellGame>();
        var board = game.Board;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var labels = (string[])typeof(RoomSpellBoard).GetField("candidateLabels", flags).GetValue(board);
        board.ShowCandidates(Array.Empty<ObjectObservation>());
        board.ShowCandidates(new[] { new ObjectObservation("cup", .9f), new ObjectObservation("book", .8f) });
        var first = Array.IndexOf(labels, "cup");
        board.ShowCandidates(new[] { new ObjectObservation("book", .95f), new ObjectObservation("cup", .7f) });
        Require(Array.IndexOf(labels, "cup") == first, "Detection target must stay put when confidence order changes.");
        board.ShowCandidates(new[] { new ObjectObservation("book", .9f) });
        Require(!labels.Contains("cup"), "An absent object must not remain selectable.");
        Require(board.GetComponentsInChildren<Graphic>(true).Any(x => x.GetType().Name == "RoomSpellSurface"),
            "Rounded gradient surfaces must be present.");
        Canvas.ForceUpdateCanvases();
        foreach (var surface in board.GetComponentsInChildren<RoomSpellSurface>())
            Require(surface.GetComponent<CanvasRenderer>() && surface.canvasRenderer.GetMesh().vertexCount > 0,
                "Visible surfaces must have rendered geometry.");
        Require(board.GetComponentsInChildren<Transform>(true).Count(x => x.name == "Object artwork") == 3,
            "Three replaceable artwork slots must be present.");
        var position = board.transform.position;
        var rotation = board.transform.rotation;
        var viewPosition = board.View.position;
        board.View.position += Vector3.right * .2f;
        Require(board.transform.position == position && board.transform.rotation == rotation,
            "Board must not follow head or hands.");
        board.View.position = viewPosition;
        foreach (var button in board.GetComponentsInChildren<Button>(true))
        {
            var rect = button.GetComponent<RectTransform>().rect;
            Require(rect.width >= 48 && rect.height >= 48, "Buttons need usable hand targets.");
        }
        game.StartDummyScenario(0);
        foreach (var label in DummyScenarios.Labels(0)) board.ObjectSelected(label);
        board.MakeRequested();
        var round = game.CurrentRound;
        Require(round != null, "Selection must still create a puzzle.");
        for (var i = 0; i < round.Tiles.Length; i++) board.TileSelected(i);
        Require(round.IsFull && !round.IsSolved, "Scrambled order must not solve.");
        board.ClearRequested();
        var used = new System.Collections.Generic.HashSet<int>();
        foreach (var letter in round.Answer)
        {
            var index = Enumerable.Range(0, round.Tiles.Length).First(i => !used.Contains(i) && round.Tiles[i] == letter);
            used.Add(index);
            board.TileSelected(index);
        }
        Require(round.IsSolved && board.GetComponentsInChildren<TMP_Text>().Any(x => x.text.Contains("Solved")),
            "Solving must display feedback.");
        board.NextRequested();
        Require(game.CurrentRound != null && game.CurrentRound.Answer != round.Answer, "Next answer must differ.");
        return "Passed: stable detection targets, absence removal, rounded surfaces, artwork slots, world placement, hand target sizes, solve/retry/next.";
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
