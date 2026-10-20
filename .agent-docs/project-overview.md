# Project overview

Last updated: 2026-10-13

- Unity: 6000.3.19f1, URP 17.3, Input System 1.19.
- Target: Quest 3/3S, Android ARM64, IL2CPP, Vulkan, linear color, package `com.roomspell.game`.
- Camera: MRUK 207 `PassthroughCameraAccess`, requested left camera 1280×960, 30 FPS.
- Model: Meta AI Building Blocks YOLO12 nano, COCO80 labels, Unity Inference Engine 2.6.1 on GPUCompute.
- Interaction: Meta XR Core and Interaction SDK 207, OpenXR 1.18, controller/hand ray and poke UI.
- Terminal control: Unity Pipeline package, Editor verification methods and PowerShell orchestration.
- Offline words: several curated answers per shared theme; answers exclude selected object names and solved session answers.
- No backend, blockchain, voice, uploaded camera frames or stored room images during ordinary play.

The current detector has no pen or notebook classes. Explicit manual selections and development dummy scenarios support the intended desk gameplay. The synthetic camera and image-fixture paths verify the existing detector; they do not add those missing classes.

See [runtime](systems/runtime.md) for how systems connect and [validation](../Validation/REPORT.md) for measured results.
