# Third-party assets

Meta XR packages are governed by the [Meta Platforms Technologies SDK License Agreement](https://developer.oculus.com/licenses/oculussdk/), as recorded in their package metadata.

`Assets/Models/yolo12nNoNMS.sentis` and `coco-labels.txt` were downloaded from Meta's remote content service using the default object detection profile for SDK 207. The profile was retrieved from content ID `24975355045490529`; model content ID is `24801439399478345`, and label content ID is `9983996451722044`.

Model SHA-256: `6250AF3B4A66AFD8F3CB988ECBCDA9145B7CB4B571E64D242546E61F38343279`.

The downloaded model is a YOLO12 nano model converted to Sentis. Downloading it through Meta's tooling does not establish a separate permissive license for its upstream weights. Confirm the applicable model terms before distributing a release.

`Tests/Fixtures/bus.jpg` is a development-only inference fixture from the [Ultralytics assets repository](https://github.com/ultralytics/assets/blob/main/im/bus.jpg). It sits outside `Assets/` and is not bundled into the Quest application. The fixture is used to check that the model and class mapping produce a real bus detection.

`Tests/Fixtures/kitchen.jpg` is COCO 2017 validation image `000000397133.jpg`, retrieved from the dataset's image server. It is a development-only multi-object fixture outside `Assets/`. COCO images retain their original image licenses; see the [COCO terms](https://cocodataset.org/#termsofuse). `empty.png` is a generated uniform-gray negative fixture.

`Tests/Fixtures/room.jpg` is COCO 2017 validation image `000000000139.jpg`, retrieved from the same image server and used for the multi-class room fixture. The kitchen photograph was inspected during fixture selection; the small COCO model detected bowls there but did not recognize enough different kitchen classes for the multi-class test.
