# iris_v2.0
thesis project, from homebody prototype


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
│     PlaneBehaviour         │
│ - Performs mesh cut        │
│ - Alters Flower geometry   │
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
        │           │
        ▼           ▼
┌─────────────┐   ┌────────────────────┐
│ Gameplay HUD│   │ Debug Telemetry HUD │
│ (Qualitative)│  │ (Numeric, dev only) │
└─────────────┘   └────────────────────┘
        │
        ▼
┌────────────────────────────┐
│   FlowerGradingUI          │
│ - Final evaluation         │
│ - Emotional framing        │
│ - End-of-session snapshot  │
└────────────────────────────┘