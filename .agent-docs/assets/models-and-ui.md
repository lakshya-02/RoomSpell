# Model and UI assets

Last updated: 2026-10-13

- `Assets/Models/RoomDetection.asset`: configured Meta inference provider. Model and matching label references, GPUCompute, split execution, confidence 0.5.
- `yolo12nNoNMS.sentis` and `coco-labels.txt`: Meta default YOLO12 nano and COCO80 mapping. Preserve the model's output/label contract when replacing it.
- TMP essential resources: LiberationSans SDF supplies all board text. Fonts and shader assets came from the installed UGUI/TMP resources.
- URP settings: PC and Mobile renderers, selected by platform configuration. Passthrough cameras clear to transparent and have postprocessing disabled.
- Android manifest: passthrough required, headset camera permission, hand tracking support and Quest 3/3S device targeting. Meta tooling generates it.
- `Tests/Fixtures/`: positive, kitchen and empty neural input fixtures outside Assets, so they do not ship in the APK.
- `Validation/Simulator/`: logs, JSON measurements and screenshots from owned terminal test sessions.

No audio clips or mixer exist. Third-party provenance and licensing notes are in `THIRD_PARTY_NOTICES.md`.
