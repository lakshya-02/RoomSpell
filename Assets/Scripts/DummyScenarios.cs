#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace RoomSpell
{
    public static class DummyScenarios
    {
        public static string[] Labels(int scenario) => scenario switch
        {
            0 => new[] { "cup", "pen", "notebook" },
            1 => new[] { "cup", "bowl", "spoon" },
            2 => new[] { "cup", "suitcase", "oven" },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
    }
}
#endif
