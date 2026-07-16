# World Map Prefab Scene Assembly Guide

## Current prefab structure

The structural duplication has been removed from the prefab assets.

- `MainUICanvas.prefab` owns the sliding UI, RawImage, buttons, and overlay labels.
- `WorldMapRenderRoot.prefab` owns WorldMapCamera, WorldMapRoot, and WorldMapPresenter.
- The UI prefab no longer contains the legacy `WorldMapUi` hierarchy or `ND.UI.WorldMap.WorldMapPanel`.
- `WorldMapOverlayLabelBinding` receives the Render prefab's Presenter directly.
- The label container no longer has its own Canvas, CanvasScaler, or GraphicRaycaster.

This prevents a second WorldMapRoot from being created in Play Mode.

## Scene placement

```text
Scene
├─ WorldMapRenderRoot                 <- WorldMapRenderRoot.prefab
│  ├─ WorldMapCamera
│  └─ WorldMapRoot
│     └─ WorldMapPresenter
│
└─ MainUICanvas                       <- MainUICanvas.prefab or the scene's existing Canvas
   └─ WorldMapPanel
      ├─ RawImage
      │  └─ WorldUiCanvas
      │     ├─ ProgressPercentLabel
      │     └─ RiskLabel
      └─ WorldMapButton
```

The Render prefab must be outside the Canvas. The World Map UI must use the scene's intended MainUICanvas.

## Required scene-instance connections

Separate prefab assets cannot store references to each other's scene instances. After placing or replacing the prefabs, connect these two fields.

| Component | Field | Assign |
|---|---|---|
| `WorldMapPanel/SlidePanel` | Rend Cam | `WorldMapRenderRoot/WorldMapCamera` |
| `WorldUiCanvas/WorldMapOverlayLabelBinding` | Presenter | `WorldMapRenderRoot/WorldMapRoot/WorldMapPresenter` |

The Progress and Risk label references are internal to the UI prefab and should already be assigned.

## RenderTexture check

Both components must use the same asset:

| Component | Field | Assign |
|---|---|---|
| WorldMapCamera | Target Texture | WorldMapRenderTexture |
| RawImage | Texture | WorldMapRenderTexture |

## Do not add

Do not add these components back to the UI prefab:

- `ND.UI.WorldMap.WorldMapPanel`
- WorldMapRoot
- WorldMapPresenter
- WorldMapCamera
- A nested Canvas or CanvasScaler for the Progress/Risk label group
- A second EventSystem

Adding `ND.UI.WorldMap.WorldMapPanel` back causes it to instantiate another WorldMapRoot during Awake.

## Play Mode verification

1. Confirm WorldMapCamera is disabled while the map starts closed.
2. Open the map and confirm the camera becomes enabled before the panel appears.
3. Close the map and confirm the camera is disabled after the slide completes.
4. Confirm exactly one WorldMapRoot exists.
5. Confirm exactly one WorldMapPresenter exists.
6. Confirm Progress/Risk labels move with the UI panel.
7. Confirm there are no missing-reference or duplicate town/route ID errors.

Expected counts:

```text
WorldMapCamera:    1
WorldMapRoot:      1
WorldMapPresenter: 1
EventSystem:       1
```

## Common problems

| Symptom | Cause | Fix |
|---|---|---|
| Map opens but camera never turns off | SlidePanel Rend Cam is None | Assign WorldMapCamera |
| Map is blank | Camera and RawImage use different RenderTextures | Assign the same texture |
| Two WorldMapRoots appear | UI contains a map-generating WorldMapPanel component | Remove that component |
| Labels do not update | Binding Presenter is None | Assign the Render prefab's Presenter |
| UI scale differs | An extra CanvasScaler exists under the map UI | Use the MainUICanvas scaler only |

