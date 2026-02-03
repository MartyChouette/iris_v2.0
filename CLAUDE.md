# CLAUDE.md — Project Context for Claude Code

## What Is This Project

Iris v2.0 is a contemplative flower-trimming game (thesis project). Unity 6.0.3, URP, C#. Players cut stems with scissors, evaluated against ideal rules for score and "days alive."

A secondary **newspaper personals dating minigame** is in progress: player looks down at a desk, circles a personal ad with a Sharpie, calls the date, waits on a countdown timer.

## Build & Run

- **Engine:** Unity 6.0.3 with Universal Render Pipeline
- **Input:** New Input System (`com.unity.inputsystem 1.16.0`)
- **Camera:** Cinemachine 3.1.2
- **No CLI build** — open in Unity Editor, play in-editor

## Editor Tools

| Menu Path | Script | What It Does |
|-----------|--------|--------------|
| Window > Iris > Flower Auto Setup | `Assets/Editor/FlowerAutoSetup.cs` | Auto-wires flower components from a model |
| Window > Iris > Build Camera Test Scene | `Assets/Editor/CameraTestSceneBuilder.cs` | Generates Cinemachine camera test room |
| Window > Iris > Build Newspaper Dating Scene | `Assets/Editor/NewspaperDatingSceneBuilder.cs` | Generates newspaper dating desk scene |

## Code Conventions

- **No namespace** on most scripts. Exceptions: `Iris.Camera`, `DynamicMeshCutter`
- **Private fields:** `_camelCase`. **Static fields:** `s_camelCase`
- **Singletons:** Scene-scoped pattern (no DontDestroyOnLoad) — see `HorrorCameraManager`. Exception: `AudioManager` uses DontDestroyOnLoad
- **ScriptableObjects:** `[CreateAssetMenu]`, `[Header]`/`[Tooltip]` on all fields
- **Input:** Inline `InputAction` fallback when scene builder can't wire InputActionReferences (see `SimpleTestCharacter.cs`)
- **TMP text updates:** Use `TMP_Text.SetText()` with format args to avoid string allocation
- **Audio:** Always go through `AudioManager.Instance.PlaySFX(clip)` with null guards on both Instance and clip
- **Debug logs:** Prefixed `[ClassName]` — e.g. `[NewspaperManager]`
- **Editor scene builders:** Follow `CameraTestSceneBuilder.cs` pattern — `NewScene(EmptyScene)`, `CreateBox()` helper, URP Lit materials, `AssetDatabase.IsValidFolder()` checks

## Key Singletons & Managers

| Class | Scope | Location |
|-------|-------|----------|
| `AudioManager` | Persistent (DDoL) | `Assets/Scripts/Framework/AudioManager.cs` |
| `TimeScaleManager` | Static utility | `Assets/Scripts/Framework/TimeScaleManager.cs` |
| `HorrorCameraManager` | Scene-scoped | `Assets/Scripts/Camera/HorrorCameraManager.cs` |
| `NewspaperManager` | Scene-scoped | `Assets/Scripts/Dating/NewspaperManager.cs` |

## Script Directory Map

| Directory | Purpose |
|-----------|---------|
| `Scripts/Framework/` | Core systems: AudioManager, TimeScaleManager, CuttingPlaneController, VirtualStemCutter, ScissorStation |
| `Scripts/GameLogic/` | Scoring brain, session lifecycle, flower definitions, stem/part runtime |
| `Scripts/InteractionAndFeel/` | Physics interactions: XYTetherJoint, SquishMove, JellyMesh, GrabPull |
| `Scripts/Fluids/` | Sap particles, decal pooling |
| `Scripts/UI/` | HUD, grading screen, debug telemetry |
| `Scripts/Camera/` | HorrorCameraManager, CameraZoneTrigger, SimpleTestCharacter |
| `Scripts/DynamicMeshCutter/` | Mesh cutting engine (DMC) |
| `Scripts/Tags/` | Marker components (StemPieceMarker, LeafAttachmentMarker, etc.) |
| `Scripts/Dating/` | Newspaper personals minigame: DatePersonalDefinition, PersonalListing, MarkerController, NewspaperManager |
| `Scripts/Prototype_LivingRoom_Scripts/` | Legacy living room prototype (not active) |

## Dating Minigame Architecture

```
MarkerController (mouse → raycast → newspaper surface)
       │ click on listing
       ▼
PersonalListing (Available → BeingCircled → Circled)
       │ circle completes
       ▼
NewspaperManager (Browsing → Calling → Waiting → DateArrived)
       │ timer hits 0
       ▼
UnityEvent OnDateArrived
```

- `DatePersonalDefinition` — ScriptableObject per character (name, ad text, arrival time)
- Circle drawn via LineRenderer with Perlin-noise wobble for hand-drawn look
- "Newspaper" physics layer for marker raycasting

## Roadmap Reference

See `LONGTERM_PLAN.md` for full backlog. Key remaining items:
- Memory profiling (historical leak concern)
- Input gating over UI (movement, not just cuts)
- Legacy Obi Fluid cleanup
- Material instance auditing (MaterialPropertyBlock)
- Unit tests for scoring logic
