# Script usage

Last updated: 2026-10-13

Runtime scripts belong to the RoomSpell scene:

- RoomSpellGame: wires board actions to detection and puzzle state; read CurrentRound and Score for validation.
- RoomSpellDetection: camera/model references; start/stop scanning, confirmed candidates and inference measurements.
- RoomSpellBoard: creates UI and exposes callbacks; invoke controls through this surface in deterministic tests.
- PuzzleRound, WordCatalog, DetectionCandidates: pure C# logic also linked by the dotnet checks.
- DummyScenarios: Editor/development labels only.

Editor/terminal entrypoints:

- RoomSpellQuestSetup.Configure: destructive starter scene reconstruction; not part of routine validation.
- RoomSpellQuestSetup.Build: synchronous development Android APK build.
- RoomSpellVerification.Run: existing model/scene and simple play-flow verification.
- RoomSpellSimulatorValidation.Run / `unity command roomspell_validate`: full simulator and fixture checks; requires the runner's output/runtime environment.
- `Tools/run-roomspell-validation.ps1`: prerequisite checks, process ownership, runtime setup, validation artifacts and APK build.

Read the corresponding [systems](../systems/runtime.md) and [scene](../scenes/room-spell.md) before changing their callers.
