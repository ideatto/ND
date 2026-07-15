# World Map Implementation Handoff

## 1. Document Purpose

This document defines the implementation direction for the world map used in the medieval idle trade simulation project.

The world map will use:

- A **node-and-graph-based logical map**
- A **2D image-based terrain map**
- **Sprite or 3D model-based town objects**
- **Straight or spline-based visual trade routes**
- **Time-based caravan progress visualization**

The implementation must keep gameplay calculations separate from visual map rendering.

---

## 2. Confirmed Design Direction

### 2.1 Logical Map

The gameplay map must be implemented as a graph.

- Towns are nodes.
- Trade routes are edges.
- Route availability is determined by graph connectivity.
- Travel distance, travel time, danger, tolls, seasonal modifiers, and route state are stored as route data.
- The logical map must not depend on visual line length, spline length, or Unity Transform distance.

Example:

```text
Town_A
 ├─ Route_AB → Town_B
 └─ Route_AC → Town_C
```

### 2.2 Visual Map

The visible map will use the following layers:

```text
Orthographic Camera
│
├─ 2D Map Background
├─ Route Layer
├─ Town Layer
├─ Caravan Layer
└─ World-Space UI Layer
```

- The terrain itself is a 2D image.
- Towns are placed above the map as either sprites or 3D models.
- Routes are rendered as straight lines by default.
- Curved routes may use splines.
- Caravan movement is visualized along the selected route.

### 2.3 Route Rendering Policy

Route rendering must support both straight and curved paths.

```csharp
public enum RoutePathType
{
    Straight,
    Spline
}
```

Recommended policy:

- Use straight routes as the default and fallback.
- Use spline routes only where visual quality benefits from curves.
- Do not require every route to have a spline.
- Spline implementation failure must not block logical route functionality.

---

## 3. Core Separation Rule

The following systems must remain independent.

### Gameplay Data

Used for actual game calculations:

- Route distance
- Travel duration
- Food consumption
- Danger rate
- Toll cost
- Seasonal modifiers
- Route blocked state
- Trade progress state

### Visual Data

Used only for presentation:

- Town world position
- Route curve shape
- Spline control points
- LineRenderer sample points
- Caravan world position
- Route highlight effects

The following relationship must always be maintained:

```text
Gameplay route distance
!=
Visual route curve length
```

Changing the shape of a route must not change game balance.

---

## 4. Recommended Data Model

## 4.1 TownData

```csharp
using UnityEngine;

[CreateAssetMenu(
    fileName = "TownData",
    menuName = "Game/World/Town Data")]
public sealed class TownData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string townId;
    [SerializeField] private string displayName;

    [Header("Map")]
    [SerializeField] private Vector2 mapPosition;
    [SerializeField] private Sprite mapIcon;

    public string TownId => townId;
    public string DisplayName => displayName;
    public Vector2 MapPosition => mapPosition;
    public Sprite MapIcon => mapIcon;
}
```

Responsibilities:

- Store immutable town identity data.
- Store the intended visual map position.
- Expose display data required by UI and world map presentation.

Do not store runtime state such as selected, unlocked, discovered, or active trade state directly in `TownData`.

---

## 4.2 RouteData

```csharp
using UnityEngine;

[CreateAssetMenu(
    fileName = "RouteData",
    menuName = "Game/World/Route Data")]
public sealed class RouteData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string routeId;

    [Header("Connection")]
    [SerializeField] private TownData startTown;
    [SerializeField] private TownData destinationTown;

    [Header("Trade")]
    [Min(0f)]
    [SerializeField] private float distance;

    [Range(0f, 1f)]
    [SerializeField] private float baseDangerRate;

    [Min(0)]
    [SerializeField] private long tollCost;

    [SerializeField] private bool bidirectional = true;

    public string RouteId => routeId;
    public TownData StartTown => startTown;
    public TownData DestinationTown => destinationTown;
    public float Distance => distance;
    public float BaseDangerRate => baseDangerRate;
    public long TollCost => tollCost;
    public bool Bidirectional => bidirectional;
}
```

Responsibilities:

- Connect two towns.
- Store game calculation data.
- Provide the route ID used by save data and presentation lookup.

Do not calculate travel distance from town Transform positions or route visual length.

---

## 4.3 WorldMapData

```csharp
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "WorldMapData",
    menuName = "Game/World/World Map Data")]
public sealed class WorldMapData : ScriptableObject
{
    [SerializeField] private List<TownData> towns = new();
    [SerializeField] private List<RouteData> routes = new();

    public IReadOnlyList<TownData> Towns => towns;
    public IReadOnlyList<RouteData> Routes => routes;
}
```

Responsibilities:

- Provide the complete town and route catalog.
- Support validation and lookup.
- Act as the shared map data source.

---

## 5. Runtime State Separation

Runtime and save state must not be written into ScriptableObject assets.

Recommended runtime/save model:

```csharp
[System.Serializable]
public sealed class RouteRuntimeState
{
    public string routeId;
    public bool isDiscovered;
    public bool isBlocked;
    public float dangerMultiplier = 1f;
    public float speedMultiplier = 1f;
}
```

Examples of runtime or save-only state:

- Route discovered state
- Route blocked state
- Seasonal danger multiplier
- Seasonal speed multiplier
- Active disaster
- Temporary event modifier

---

## 6. Recommended Scene Hierarchy

```text
WorldMapRoot
├─ Background
│  └─ WorldMapBackground
├─ RouteLayer
│  ├─ Route_TownA_TownB
│  ├─ Route_TownA_TownC
│  └─ Route_TownB_TownC
├─ TownLayer
│  ├─ Town_TownA
│  ├─ Town_TownB
│  └─ Town_TownC
├─ CaravanLayer
│  └─ ActiveCaravanMarker
└─ WorldUiLayer
   ├─ TownLabels
   ├─ RouteIndicators
   └─ SelectionEffects
```

Use an Orthographic Camera.

Recommended render order:

```text
Background
→ Routes
→ Towns
→ Caravan
→ UI and Selection Effects
```

---

## 7. Town Presentation

Each town should use a presentation component independent from `TownData`.

Recommended hierarchy:

```text
TownWorldView
├─ TownModelOrSprite
├─ SelectionRing
├─ EffectAnchor
└─ TownUiAnchor
```

Recommended responsibilities:

- Bind to one `townId`.
- Display sprite or 3D model.
- Show selected, locked, active, or destination state.
- Expose an anchor for labels and effects.
- Forward player click input to the presenter or controller.

Do not let `TownWorldView` directly modify trade progression or save data.

---

## 8. RouteVisual Structure

The route visual component must support straight and spline paths.

```csharp
using UnityEngine;
using UnityEngine.Splines;

public enum RoutePathType
{
    Straight,
    Spline
}

public sealed class RouteVisual : MonoBehaviour
{
    [SerializeField] private string routeId;
    [SerializeField] private RoutePathType pathType;

    [Header("Straight Path")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;

    [Header("Spline Path")]
    [SerializeField] private SplineContainer splineContainer;

    public string RouteId => routeId;

    public Vector3 EvaluatePosition(float progress)
    {
        progress = Mathf.Clamp01(progress);

        if (pathType == RoutePathType.Spline &&
            splineContainer != null)
        {
            Vector3 localPosition =
                splineContainer.EvaluatePosition(progress);

            return splineContainer.transform.TransformPoint(
                localPosition);
        }

        if (startPoint == null || endPoint == null)
        {
            return transform.position;
        }

        return Vector3.Lerp(
            startPoint.position,
            endPoint.position,
            progress);
    }
}
```

Implementation requirements:

- Route ID must match the corresponding `RouteData.RouteId`.
- Missing spline data must fall back to straight movement.
- Invalid route visual configuration must log a clear error.
- Duplicate route IDs must be detected during initialization.

---

## 9. Route Rendering

## 9.1 Initial Recommended Method

Use `LineRenderer`.

For spline routes:

1. Sample multiple points from the spline.
2. Pass the sampled positions to `LineRenderer`.
3. Rebuild only when route geometry changes.
4. Do not rebuild every frame.

Example:

```csharp
public void RebuildLine(
    SplineContainer spline,
    LineRenderer lineRenderer,
    int resolution)
{
    resolution = Mathf.Max(2, resolution);
    lineRenderer.positionCount = resolution;

    for (int i = 0; i < resolution; i++)
    {
        float t = i / (float)(resolution - 1);

        Vector3 localPosition =
            spline.EvaluatePosition(t);

        Vector3 worldPosition =
            spline.transform.TransformPoint(localPosition);

        lineRenderer.SetPosition(i, worldPosition);
    }
}
```

## 9.2 Optional Later Improvements

Possible later improvements:

- Textured LineRenderer
- Repeated road sprites along spline
- Spline-based road mesh generation
- Route width by category
- Animated route highlight
- Dangerous segment indication

Do not implement spline mesh generation in the first iteration unless explicitly required.

---

## 10. Caravan Map Marker

The Caravan marker must use time-based progress.

Recommended data source:

```text
activeRouteId
tradeStartUtcTick
expectedTradeEndUtcTick
TradeProgressState
```

Progress calculation:

```csharp
public static float CalculateProgress(
    long currentTick,
    long startTick,
    long expectedEndTick)
{
    if (expectedEndTick <= startTick)
    {
        return 1f;
    }

    double elapsed = currentTick - startTick;
    double duration = expectedEndTick - startTick;

    return Mathf.Clamp01((float)(elapsed / duration));
}
```

Data flow:

```text
Current time
→ Trade progress calculation
→ RouteVisual lookup
→ Evaluate route position
→ Move Caravan marker
```

The marker position must be reconstructed after loading.

Do not save:

- Caravan Transform position
- Spline parameter
- LineRenderer points

---

## 11. Caravan Direction

For a 3D Caravan model:

- Use route tangent direction.
- Rotate the model toward movement direction.
- Keep the model upright based on the map plane.

For a Sprite Caravan:

- Use horizontal flip if only left/right orientation is required.
- Optionally rotate the sprite to follow the route tangent.

Example:

```csharp
spriteRenderer.flipX = tangent.x < 0f;
```

Spline tangent evaluation may vary depending on the installed Unity Splines package version. Confirm the exact API before implementation.

---

## 12. WorldMapPresenter Responsibilities

Recommended responsibilities:

- Build `townId -> TownWorldView` lookup.
- Build `routeId -> RouteVisual` lookup.
- Read shared map data.
- Read current trade progress.
- Display active route.
- Display selected route.
- Position the Caravan marker.
- Refresh states after load or scene entry.
- Handle state changes without modifying core trade state directly.

Recommended conceptual structure:

```text
WorldMapPresenter
├─ Shared game data provider
├─ Trade progress provider
├─ Town view lookup
├─ Route visual lookup
└─ Caravan marker
```

---

## 13. Recommended Provider Interface

The world map should not access save implementation details directly.

```csharp
public interface ITradeMapProgressProvider
{
    bool HasActiveTrade { get; }
    string ActiveRouteId { get; }
    float CurrentProgress { get; }
    TradeProgressState State { get; }
}
```

The provider may internally use:

- `TradeProgressCoordinator`
- `TradeProgressRecorder`
- Current save data
- Game time provider

The presentation layer must remain read-only.

---

## 14. Integration With Existing Framework

Existing project state:

```text
activeTradeId
activeRouteId
tradeStartUtcTick
expectedTradeEndUtcTick
TradeProgressState
TradeProgressCoordinator
ISharedGameDataProvider
```

Recommended connection:

```text
TradeProgressCoordinator
        │
        ├─ Active route ID
        ├─ Current progress
        └─ Current trade state
                │
                ▼
WorldMapPresenter
        │
        ├─ Route highlight
        ├─ Caravan position
        ├─ Caravan visibility
        └─ Arrival presentation
```

Do not introduce a second independent trade progress system for the world map.

---

## 15. Map Asset Production Policy

The background image should contain:

- Terrain
- Mountains
- Rivers
- Forests
- Coastlines
- Decorative regions

The background image should not permanently contain:

- Town markers
- Town names
- Trade routes
- Selection state
- Route danger state
- Caravan marker
- Route blocking icons

These must remain separate Unity objects.

---

## 16. Implementation Priority

## Phase 1: Required Baseline

Implement first:

1. `WorldMapData`
2. `TownData` and `RouteData` validation
3. 2D background map
4. Town object placement
5. Straight `RouteVisual`
6. LineRenderer route drawing
7. Active route lookup
8. Caravan marker movement using progress
9. Scene reload position reconstruction
10. Route ID duplicate validation

## Phase 2: Optional Curved Routes

After the baseline works:

1. Add `RoutePathType`
2. Add Unity Splines package only if project policy allows it
3. Add `SplineContainer`
4. Sample spline into LineRenderer
5. Move Caravan marker along spline
6. Add spline tangent-based direction
7. Confirm fallback to straight routes

## Phase 3: Polish

Later improvements:

- Curved major trade routes
- Route textures
- Route selection animation
- Route danger visualization
- Seasonal visual changes
- Multiple Caravan markers
- Map zoom and pan
- Editor validation tools

---

## 17. First Build Scope Policy

The current first build scope is already fixed.

For the first build:

- Do not expand the project into a full world map editor.
- Do not add pathfinding unless already required.
- Do not add procedural map generation.
- Do not add NavMesh-based world travel.
- Do not add chunk streaming.
- Do not add runtime road mesh generation.
- Do not add multiple-route optimization systems.

The first build should prioritize:

- UX connection
- Integration
- Stability
- Save/load recovery
- Debugging

Spline routes should only be included if they can be added without destabilizing the existing first build scope.

---

## 18. Validation Requirements

Add validation for the following:

### TownData

- Empty town ID
- Duplicate town ID
- Missing display name
- Invalid map position where applicable

### RouteData

- Empty route ID
- Duplicate route ID
- Missing start town
- Missing destination town
- Start and destination town are identical
- Negative distance
- Invalid danger rate
- Invalid toll cost

### RouteVisual

- Empty route ID
- Duplicate route ID
- Missing start or end point for straight route
- Missing spline for spline route
- RouteData without matching RouteVisual
- RouteVisual without matching RouteData

---

## 19. Prohibited Implementations

Do not implement the following:

### Do not calculate gameplay distance from Transform positions

```csharp
float distance = Vector3.Distance(
    startTown.position,
    endTown.position);
```

### Do not calculate gameplay distance from spline length

```csharp
float distance = splineLength;
```

### Do not save Caravan world position

```csharp
saveData.caravanPosition = transform.position;
```

### Do not write runtime state into ScriptableObject assets

```csharp
routeData.IsBlocked = true;
```

### Do not let visual components modify trade state directly

```text
RouteVisual
→ must not call trade start, settlement, or save APIs directly
```

---

## 20. Completion Criteria

The implementation is complete when:

- Towns are loaded from shared map data.
- Every route has a valid logical `RouteData`.
- Every visible route is matched by route ID.
- Straight routes render correctly.
- Spline routes render correctly when configured.
- Missing spline configuration falls back safely.
- Caravan position is derived from trade progress.
- Save/load restores the correct Caravan position.
- Visual route length does not affect travel duration.
- Duplicate and invalid IDs are reported.
- Existing trade progression and settlement behavior are unchanged.
- Unity Console has no unexpected errors during map entry, trade start, progress, load, arrival, and settlement transitions.

---

## 21. Final Implementation Direction

Use the following final architecture:

```text
Logical Map
TownData + RouteData + WorldMapData

Visual Map
2D Background + TownWorldView + RouteVisual

Travel Presentation
TradeProgressCoordinator
→ ITradeMapProgressProvider
→ WorldMapPresenter
→ CaravanMapMarker

Route Rendering
Straight LineRenderer by default
Spline + LineRenderer when configured
Straight fallback when spline is unavailable
```

The map system must remain data-driven, save-safe, and independent from visual path geometry.
