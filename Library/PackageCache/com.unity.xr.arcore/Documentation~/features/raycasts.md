---
uid: arcore-raycasts
---
# Ray casts

This page is a supplement to the AR Foundation [Ray casts](xref:arfoundation-raycasts) manual. The following sections only contain information about APIs where ARCore exhibits unique platform-specific behavior.

[!include[](../snippets/arf-docs-tip.md)]

## Optional feature support

ARCore implements the following optional features of AR Foundation's [XRRaycastSubsystem](xref:UnityEngine.XR.ARSubsystems.XRRaycastSubsystem):

| Feature                    | Descriptor Property | Supported |
| :------------------------- | :------------------ | :-------: |
| **Viewport based raycast** | [supportsViewportBasedRaycast](xref:UnityEngine.XR.ARSubsystems.XRRaycastSubsystemDescriptor.supportsViewportBasedRaycast)| Yes |
| **World based raycast**    |  [supportsWorldBasedRaycast](xref:UnityEngine.XR.ARSubsystems.XRRaycastSubsystemDescriptor.supportsWorldBasedRaycast)   | Yes |
| **Tracked raycasts**       | [supportsTrackedRaycasts](xref:UnityEngine.XR.ARSubsystems.XRRaycastSubsystemDescriptor.supportsTrackedRaycasts) | Yes |

### Supported trackables

ARCore supports ray casting against the following trackable types:

| TrackableType           | Supported |
| :---------------------- | :-------: |
| **BoundingBox**         |           |
| **Depth**               |    Yes    |
| **Face**                |           |
| **FeaturePoint**        |    Yes    |
| **Image**               |           |
| **Planes**              |    Yes    |
| **PlaneEstimated**      |    Yes    |
| **PlaneWithinBounds**   |    Yes    |
| **PlaneWithinInfinity** |           |
| **PlaneWithinPolygon**  |    Yes    |

> [!NOTE]
> Refer to AR Foundation [Ray cast platform support](xref:arfoundation-raycasts-platform-support) for more information on the optional features of the Raycast subsystem.
