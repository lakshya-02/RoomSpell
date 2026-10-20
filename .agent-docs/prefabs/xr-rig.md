# XR rig prefab usage

Last updated: 2026-10-13

The scene instantiates SDK prefabs rather than local copies:

- OVRCameraRig: tracking space contains center/left/right eye and hand/controller anchors. OVRManager requests floor tracking and passthrough.
- OVRComprehensiveInteractionRig: nested camera-rig references, controller and hand data sources, visuals, ray/poke interactors. Rig references must point to the scene OVRCameraRig and its hand components.
- RayCanvas and PokeCanvas templates: stretched children of the board, their PointableCanvas references point to the parent world-space Canvas.

The UI world camera is the center eye camera. Board scale is 0.001 and its rectangle is 900×740. The board is placed 1.4 m forward and slightly below the head. Changing board size requires checking both canvas templates and readable text in XR.

See [scene](../scenes/room-spell.md) and [systems](../systems/runtime.md).
