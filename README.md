# Iris v2.0

Contemplative flower trimming game. Thesis project built in Unity 6.0.3 with URP.

## Overview

Players tend to flowers by cutting stems, removing withered leaves, and arranging petals to achieve an ideal arrangement. The game evaluates stem length, cut angle, and part condition against an `IdealFlowerDefinition` to produce a score.

## System Architecture

```
┌────────────────────────────┐
│        Player Input        │
│ (Mouse, Scroll, Click)     │
└────────────┬───────────────┘
             │
             ▼
┌────────────────────────────┐
│  CuttingPlaneController    │
│ - Moves plane vertically   │
│ - Triggers cut input       │
│ - Gated by ScissorStation  │
└────────────┬───────────────┘
             │
             ▼
┌────────────────────────────┐
│     PlaneBehaviour /       │
│  AngleStagePlaneBehaviour  │
│ - Virtual stem cut (fast)  │
│ - DMC fallback (destructive│
└────────────┬───────────────┘
             │
             ▼
┌────────────────────────────┐
│   FlowerGameBrain          │
│ - Evaluates stem length    │
│ - Evaluates cut angle      │
│ - Tracks parts condition   │
│ - Computes score           │
└───────┬───────────┬────────┘
        │           │
        ▼           ▼
┌─────────────┐  ┌────────────────────┐
│ Gameplay HUD│  │ Debug Telemetry HUD│
│(Qualitative)│  │ (Numeric, F3 key)  │
└─────────────┘  └────────────────────┘
        │
        ▼
┌────────────────────────────┐
│   FlowerGradingUI          │
│ - Final evaluation         │
│ - Emotional framing        │
│ - End-of-session snapshot  │
└────────────────────────────┘
```

## Key Subsystems

| Subsystem | Description |
|-----------|-------------|
| **Input/Control** | Mouse-driven cutting plane, scissor equip station, angle tilt |
| **Mesh Cutting** | VirtualStemCutter (non-destructive) with DMC fallback |
| **Game Logic** | Session lifecycle, scoring brain, stem runtime tracking |
| **Physics/Joints** | XYTetherJoint custom tether, SquishMove jelly deformation |
| **Fluids/VFX** | Particle-based sap system with pooled decals |
| **UI** | Gameplay feedback HUD, grading screen, debug telemetry |
| **Audio** | Singleton AudioManager with spatial SFX |
| **Data** | ScriptableObject flower definitions for data-driven scoring |

## Creating a New Flower Level

1. Import your flower model into the scene
2. Open **Window > Iris > Flower Auto Setup**
3. Select the flower root in the hierarchy
4. Verify detected parts (stem, crown, leaves, petals)
5. Click **Setup Flower** to auto-wire all components
6. Adjust the generated `IdealFlowerDefinition` for scoring rules
7. Wire UI events (OnGameOver, OnResult) to your HUD prefabs

## Project Structure

```
Assets/
├── Editor/              # Custom inspectors and tools
│   └── FlowerAutoSetup  # Auto-wiring wizard for new flowers
├── Scripts/
│   ├── Framework/       # TimeScaleManager, VirtualStemCutter, AudioManager
│   ├── GameLogic/       # FlowerGameBrain, FlowerSessionController, scoring
│   ├── InteractionAndFeel/ # XYTetherJoint, SquishMove, GrabPull
│   ├── DynamicMeshCutter/  # Mesh cutting engine + plane behaviors
│   ├── Fluids/          # Sap particle system, decal pooling
│   ├── UI/              # HUD, grading, debug telemetry
│   └── Tags/            # Marker components (StemPieceMarker, etc.)
├── ScriptableObjects/   # Flower definitions, scoring rules
└── Scenes/              # Game scenes
```

## Development

See [LONGTERM_PLAN.md](LONGTERM_PLAN.md) for the project roadmap and remaining work items.

See [CODEBASE_QUALITY_ASSESSMENT.md](CODEBASE_QUALITY_ASSESSMENT.md) for the full technical audit.
