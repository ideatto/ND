# Deferred Work C Handoff — Product InGame Village Placement UI Integration

## 1. Purpose

Integrate the completed village building placement persistence system into the actual product `InGame` UI after modification permission has been granted for the required scene and prefab assets.

This work is intentionally deferred because permission is currently unavailable for:

```text
InGame.unity
MainUICanvas.prefab
and potentially related product UI assets
```

Do not begin implementation using this document alone.

At the time this work resumes, first re-investigate the actual repository and Unity project state. The current findings are historical context only and may have become stale.

---

## 2. Required Workflow

Follow this sequence:

```text
1. Confirm asset modification permission
2. Create or switch to the intended task branch
3. Cursor performs a fresh read-only investigation
4. ChatGPT reviews the investigation result and makes policy decisions
5. ChatGPT writes a current Codex implementation handoff
6. Codex implements the approved integration and minimum tests
7. ChatGPT reviews the implementation result
8. Cursor performs full product runtime verification
9. ChatGPT issues the final verdict
```

Do not skip the fresh Cursor investigation.

Do not let Cursor implement production changes during the investigation phase.

---

## 3. Historical Context — Reverify Everything

At the time Work B was completed, the following state was reported:

```text
Product InGame/MainUICanvas:
- Village view was an inline RawImage.
- BuildingPlacementController was not attached.
- Product pointer drag and rotation were therefore unavailable.

Greybox/test path:
- VillageView.prefab contained BuildingPlacementController.
- Work B was verified using a temporary runtime instance of that prefab.

Village_Home:
- Loaded additively from InGame.
- VillageCamera rendered to RT_Village.
- Building placement SaveData capture, rollback, and restoration were implemented.

Work B verification:
- Framework command passed.
- Drag, rotation, combined drag+rotation, rollback, and reload passed.
- Saved placement priority and invalid-placement fallback passed.
- Product MainUICanvas integration remained blocked.
```

These facts must not be assumed to remain true.

Reinspect all relevant files, scenes, prefabs, scripts, references, and runtime behavior at the time this work resumes.

---

## 4. Prerequisites

Before investigation or implementation, confirm:

```text
Permission to modify InGame.unity
Permission to modify MainUICanvas.prefab
Permission to modify any nested VillageView prefab
Permission to modify product UI scripts if required
Permission to modify Village_Home only if later evidence proves it necessary
```

Also confirm whether Work A and Work B are already merged into the current base branch.

Required Work A contract:

```csharp
public bool hasPlacement;
public int gridCellX;
public int gridCellZ;
public int yawStep;
```

Required Work B runtime capabilities:

```text
FrameworkRoot.Instance.BuildingPlacement
BuildingPlacementController drag/rotation transaction
Save failure rollback
VillageBuildingRegistry placement restoration
saved-placement-first registration
```

If these APIs or contracts have changed, document the new current form rather than forcing the historical API.

---

# Phase 1 — Cursor Fresh Investigation

## 5. Investigation Objective

Determine the exact current integration required to expose village building placement through the actual product `InGame` UI.

The investigation must answer:

```text
What product UI object currently displays the village?
How does pointer input reach that view?
What camera and RenderTexture are currently used?
Is BuildingPlacementController already present?
Can the existing VillageView prefab be reused safely?
Which scene or prefab must be modified?
Which references must be assigned?
What input conflicts exist?
What product UX is intended for selection, dragging, and rotation?
```

This phase is read-only.

---

## 6. Repository and Branch State

Record:

```text
Current branch
HEAD
Base branch relationship
Unity version
Working tree state
Relevant Work A / Work B commits
Whether Work B APIs still exist
Whether later changes overlap the candidate files
```

Inspect recent history for changes involving:

```text
InGame
MainUICanvas
VillageView
Village_Home
BuildingPlacementController
VillageBuildingRegistry
RenderTexture
VillageCamera
HomeView
WorldMap
pointer input
```

Identify merged or unmerged branches that may already contain related product UI work.

---

## 7. Reinspect Scene and Prefab Ownership

Read the current ownership documents and inspect repository history.

Determine the current owner and modification permission for:

```text
Assets/_Project/07.Scenes/04_InGame/InGame.unity
Assets/_Project/07.Scenes/04_InGame/Village_Home.unity
Assets/_Project/08.Prefabs/UI/Maps/MainUICanvas.prefab
Assets/_Project/08.Prefabs/Village/VillageView.prefab
any nested product village UI prefab
```

Report:

| Asset | Current owner | Permission confirmed | Concurrent work risk | Edit likely |
| ----- | ------------- | -------------------: | -------------------: | ----------: |

If ownership or permission is unclear, do not proceed to implementation.

---

## 8. Reconstruct the Current Product Rendering Flow

Trace the actual current flow from scene load to village rendering.

Expected historical shape:

```text
Boot
→ Title
→ Loading
→ InGame
→ MainUICanvas village RawImage
→ RT_Village
→ VillageCamera
→ additive Village_Home
```

Reverify:

```text
InGame scene path
MainUICanvas prefab source
Village view object hierarchy
RawImage texture
RenderTexture asset
VillageCamera targetTexture
Village_Home load mechanism
additive scene timing
camera activation rules
panel visibility rules
```

Document the exact current chain.

Use scene and prefab YAML, script references, and runtime Inspector evidence.

---

## 9. Compare Product and Greybox Village Views

Inspect the current product village UI and every existing Greybox/test village view.

Compare:

```text
components
RectTransform
RawImage
raycastTarget
BuildingPlacementController
serialized references
camera reference
grid reference
registry reference
rotation controls
selection controls
input blockers
CanvasGroup
sorting order
nested masks
aspect ratio behavior
```

Produce a table:

| Feature | Product view | Greybox/test view | Reusable directly | Adaptation required |
| ------- | ------------ | ----------------- | ----------------: | ------------------: |

Determine whether the safest integration is:

### Option A — Attach the controller to the existing product RawImage

Use when:

* the current product hierarchy should remain unchanged;
* all required references can be assigned;
* the controller does not require Greybox-only child objects.

### Option B — Replace or nest with the existing VillageView prefab

Use when:

* the prefab already contains the complete input component structure;
* replacement does not break layout or existing UI references;
* duplicated RawImages or RenderTextures can be avoided.

### Option C — Extract a shared controller component/configuration

Use only when:

* product and Greybox structures differ materially;
* a small shared runtime layer reduces duplication;
* it does not become an unnecessary UI refactor.

Do not select an option until current evidence is available.

---

## 10. Inspect BuildingPlacementController Requirements

Reinspect the current controller implementation.

List every serialized and runtime dependency:

```text
RawImage or RectTransform
VillageCamera
RenderTexture
VillageGrid
VillageBuildingRegistry
selection state
rotation buttons
pointer event interfaces
raycasting method
camera pan/zoom dependencies
NPC or world interaction references
FrameworkRoot placement command
```

For each dependency, report:

| Dependency | How currently resolved | Product equivalent exists | Can resolve dynamically | Requires asset reference |
| ---------- | ---------------------- | ------------------------: | ----------------------: | -----------------------: |

Determine whether the controller initializes before or after `Village_Home` additive loading.

Identify how it behaves when:

```text
VillageCamera is temporarily unavailable
VillageGrid is temporarily unavailable
VillageBuildingRegistry is temporarily unavailable
FrameworkRoot is unavailable
Village_Home unloads
InGame reloads
```

The implementation must not depend on fragile one-frame timing.

---

## 11. Pointer-to-World Coordinate Investigation

Verify the exact coordinate-conversion path used by the current controller.

Trace:

```text
screen pointer position
→ RawImage local position
→ normalized UV
→ RenderTexture coordinates
→ camera ray
→ village world plane or collider
→ VillageGrid cell
```

Confirm:

```text
Canvas render mode
Canvas scaler settings
RawImage aspect ratio
RenderTexture dimensions
camera projection
camera viewport rect
screen resolution dependence
panel animation or scaling
masking and clipping
```

Test or inspect behavior at representative resolutions and aspect ratios.

Determine whether the historical Greybox conversion works unchanged in the product UI.

Check for:

* Y-axis UV inversion;
* letterboxing;
* cropped RawImage regions;
* scaled RectTransform mismatch;
* clicks outside the actual village texture;
* world ray passing through unintended colliders.

---

## 12. Input Conflict Investigation

Inspect product UI event flow for conflicts with:

```text
World Map
SlidePanel
camera pan
camera zoom
scroll views
building list
popup panels
Home button
settlement UI
other RawImages
CanvasGroup blocksRaycasts
GraphicRaycaster
EventSystem
Input System modules
```

For each pointer action, determine the intended priority:

| Input               | Village placement | Existing product action  | Conflict |
| ------------------- | ----------------- | ------------------------ | -------- |
| Left press          | select/drag       | camera or panel action   |          |
| Left drag           | move building     | map/panel drag           |          |
| Pointer up          | commit            | other UI click           |          |
| Mouse wheel         | rotate or zoom    | camera zoom/scroll       |          |
| Rotate-left button  | rotate            | current button function  |          |
| Rotate-right button | rotate            | current button function  |          |
| Right click         | possible cancel   | current product behavior |          |
| Touch drag          | mobile placement  | existing touch behavior  |          |

Do not assume mouse-wheel rotation should remain enabled in the product.

Report whether placement needs an explicit edit mode to prevent accidental movement.

---

## 13. Product UX Investigation

Determine the current intended UX from code, UI assets, planning documents, and owner direction.

Answer:

```text
Can buildings always be moved?
Is there a dedicated edit/layout mode?
How is a building selected?
How is selection highlighted?
How does the user rotate?
How does the user cancel an uncommitted move?
Is placement available on mouse only or also touch?
Should camera pan/zoom remain active while moving a building?
Can locked/unbuilt buildings be selected?
Can background/orphan PlaceableBuilding objects be moved?
```

Do not invent UX policy from the Greybox implementation.

If the product policy is unresolved, identify the exact decisions required before Codex implementation.

---

## 14. Rotation UI Investigation

Inspect whether the current product UI contains:

```text
RotateLeft button
RotateRight button
placement edit toolbar
selected-building panel
mouse-wheel instruction
touch rotation control
```

Determine which rotation entry points should be enabled.

Historical Work B supports:

```text
OnScroll
RotateLeft
RotateRight
```

The product integration may expose only a subset.

Report the recommended product input contract based on current UI and owner policy.

---

## 15. Additive Scene Timing and Lifetime

Trace the exact current sequence:

```text
InGame loaded
MainUICanvas initialized
VillageView enabled
Village_Home additive load requested
Village_Home loaded
VillageCamera created
VillageGrid created
VillageBuildingRegistry Start
placement restoration
product controller dependency resolution
```

Identify race conditions.

Determine the safest initialization model:

### Serialized scene references

Usually invalid across separate additive scene assets unless resolved at runtime.

### Runtime lookup after scene load

Potentially acceptable if:

* lookup is scoped;
* retries are bounded;
* unload is handled;
* object identity is deterministic.

### Event-driven binding

Preferred when an existing additive-scene-ready event or registry exists.

Do not create a large new scene-service architecture for this integration unless current code requires it.

---

## 16. Orphan PlaceableBuilding Investigation

Work B stress verification reported warnings involving objects such as:

```text
Windmill
MountTown
HarborVillage
```

Reinspect the current product scene.

Determine:

```text
which objects have PlaceableBuilding
which objects are registered village buildings
which are background or town markers
whether the product controller can select them
whether they should participate in occupancy
whether they need an explicit exclusion flag
```

Report whether this must be fixed as part of Work C or filed separately.

Do not expand Work C unless these objects interfere with actual product placement.

---

## 17. Current Work B Compatibility

Reverify that the current product branch still satisfies:

```text
placement Save is issued only on confirmed commit
drag update does not Save
combined drag and rotation Saves once
Save failure rolls back Transform and occupancy
load restoration occurs before default registration
later polling does not relocate saved buildings
```

Identify any later modifications that invalidate these assumptions.

If Work B has diverged, document the exact required adaptation before UI wiring.

---

## 18. Required Runtime Investigation

When safe and without saving assets, run the actual product path:

```text
Boot
→ Title
→ Continue or New Game
→ Loading
→ InGame
→ Village_Home additive load
→ open the village/home view
```

Verify:

```text
which GameObject receives pointer events
whether a placement controller exists
whether the RawImage blocks or receives raycasts
whether VillageCamera and Grid can be resolved
whether any current drag/rotation behavior already exists
whether Console errors or warnings occur
```

Do not add components or wire references during the investigation.

If runtime inspection requires temporary objects, create them only at runtime and ensure scenes remain not dirty.

---

## 19. Investigation Output Requirements

Return a report with this exact structure.

# Work C — Product InGame Village Placement UI Fresh Investigation Result

## 1. Final Verdict

```text
Current product placement availability:
Work A availability:
Work B availability:
MainUICanvas integration status:
Permission status:
Recommended integration option:
Implementation readiness:
Blocking decisions:
```

## 2. Environment

```text
Branch:
HEAD:
Base:
Unity version:
Working tree before:
Working tree after:
Runtime path tested:
```

## 3. Current Product Flow

Show the exact current flow:

```text
Boot → Title → Loading → InGame → product village view → RenderTexture → Village_Home
```

## 4. Asset and Ownership Matrix

| Asset/file | Current role | Owner | Permission | Concurrent risk | Edit required |
| ---------- | ------------ | ----- | ---------: | --------------: | ------------: |

## 5. Product vs Greybox Comparison

| Concern | Product | Greybox/test | Difference | Impact |
| ------- | ------- | ------------ | ---------- | ------ |

## 6. Controller Dependency Audit

| Dependency | Current resolution | Product availability | Required integration |
| ---------- | ------------------ | -------------------- | -------------------- |

## 7. Pointer Coordinate Path

Document the exact screen-to-world conversion and any aspect-ratio or scaling risks.

## 8. Input Conflict Matrix

| Input | Current product behavior | Proposed placement behavior | Conflict | Decision required |
| ----- | ------------------------ | --------------------------- | -------- | ----------------: |

## 9. Additive Scene Timing

Show the exact initialization order and recommended binding point.

## 10. Rotation and Selection UX

Document current UI support and unresolved product decisions.

## 11. Work B Compatibility

Report whether current Work B APIs and invariants remain valid.

## 12. Orphan PlaceableBuilding Findings

List product-relevant objects and whether exclusion is necessary.

## 13. Candidate Integration Options

For each option, document:

```text
files/assets changed
advantages
risks
merge-conflict exposure
testability
recommendation
```

## 14. Recommended Implementation Scope

Separate:

```text
required
optional
deferred
explicitly excluded
```

## 15. Expected Changed Files

| File/asset | Reason | Owner | Permission confirmed |
| ---------- | ------ | ----- | -------------------: |

## 16. Required Policy Decisions

List only decisions that cannot be resolved from current code or assets.

## 17. Suggested Codex Work Breakdown

Provide a minimal implementation sequence.

## 18. Suggested Cursor Verification Matrix

Include full product runtime testing.

## 19. Final Recommendation

Choose one:

```text
READY — ChatGPT may write the Codex implementation handoff
CONDITIONAL — resolve listed decisions or permissions first
BLOCKED — explain exact blockers
```

---

# Phase 2 — Policy Review by ChatGPT

## 20. Mandatory Review Before Implementation

After the fresh Cursor report is returned, ChatGPT must review and decide:

```text
integration option
assets allowed to change
whether an explicit placement-edit mode is required
selection behavior
rotation input
camera/input conflict policy
touch/mobile scope
initialization and additive-scene binding
orphan object exclusion
fallback behavior
completion criteria
```

Do not reuse an old Codex handoff without updating it from the new investigation.

---

# Phase 3 — Future Codex Implementation Scope

## 21. Expected High-Level Goal

After approval, the implementation should make the existing Work B placement system usable from the actual product `InGame` village view.

Likely target behavior:

```text
Product InGame village view receives pointer input
→ controller resolves current Village_Home camera/grid/registry
→ valid building selection and drag/rotation use Work B transactions
→ Save occurs only on confirmed placement
→ Title/Continue restores the saved layout
```

This is not final implementation policy. The fresh investigation and ChatGPT review control the final scope.

---

## 22. Likely Non-Goals

Unless the future investigation changes the requirement, keep these excluded:

```text
SaveData schema changes
SaveData version increase
stable buildingId migration
multiple same-type buildings
free-angle rotation
building Y-position persistence
grid redesign
building economy changes
construction UX redesign
large UI visual redesign
unrelated Village_Home scene changes
```

---

# Phase 4 — Future Product Runtime Verification

## 23. Minimum Final Verification

After implementation, Cursor should test through the actual product flow:

```text
Boot
→ Title
→ Continue or New Game
→ Loading
→ InGame
→ open product village view
→ select building
→ drag
→ rotate
→ save
→ return to Title
→ Continue
→ verify restored position and rotation
```

Minimum cases:

```text
product drag success
product rotation success
combined drag and rotation
Save exactly once per commit
invalid placement rejection
Save failure rollback
panel and map input conflicts
camera pan/zoom behavior
old save hasPlacement=false
level and placement coexistence
later registration stability
different resolutions/aspect ratios
scene/prefab cleanliness
Console Error count 0
```

---

## 24. Deferred Status Summary

At the time this handoff was written:

```text
Work A:
Complete and verified.

Work B:
Conditional PASS.
Framework and Greybox/runtime placement persistence passed.
Product MainUICanvas input remained unwired.

Work C:
Deferred because InGame and MainUICanvas modification permission was unavailable.
```

When resuming, treat this summary only as historical context and verify the current repository state from scratch.
