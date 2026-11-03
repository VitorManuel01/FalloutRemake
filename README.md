# FalloutRemake — Fallout 2 Remake in Godot 4 .NET / C#

**English** | [Português (Brasil)](README.pt-BR.md)

[![.NET 8](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![Godot Engine](https://img.shields.io/badge/Godot-4.7%2B%20.NET-478cbf.svg)](https://godotengine.org/)
[![Synthetic Tests](https://img.shields.io/badge/Tests-481%20passing-brightgreen.svg)]()
[![Development Mode](https://img.shields.io/badge/Mode-Vibe%20Coding%20%2B%20Multi--Agent-blueviolet.svg)]()
[![AI Harnesses](https://img.shields.io/badge/Harnesses-Codex%20%7C%20Claude%20%7C%20Antigravity-orange.svg)]()
[![Parity Reference](https://img.shields.io/badge/Parity-Fallout%202%20CE%20(e97087b)-yellow.svg)](https://github.com/alexbatalov/fallout2-ce)

A personal remake of **Fallout 2** built with **Godot 4 .NET / C#**, featuring a 3D isometric presentation with lighting and material fidelity inspired by *Wasteland 3*, while strictly preserving original binary structures, opcodes, and mathematical rules via the **Fallout 2 Community Engine (CE)**.

---

## ⚡ Vibe Coding with Serious Engineering: The Parallel Multi-Agent Workflow

While this project is developed under the **"Vibe Coding"** paradigm (intent-driven development authored massively by AI agents), it strictly rejects fragile "prompt-and-pray" habits. Instead, it implements an **enterprise-grade multi-agent engineering workflow with distinct AI harnesses operating concurrently in parallel**:

```text
                               ┌──────────────────────────────────────────────────────────┐
                               │               SHARED MULTI-AGENT MEMORY                  │
                               │  • ai-memory MCP Server (historical decisions & facts)   │
                               │  • Canonical Docs (AGENTS.md, PROJECT_STATUS.md)         │
                               └────────────────────────────┬─────────────────────────────┘
                                                            │
                 ┌──────────────────────────────────────────┼──────────────────────────────────────────┐
                 │                                          │                                          │
                 ▼                                          ▼                                          ▼
   ┌───────────────────────────┐              ┌───────────────────────────┐              ┌───────────────────────────┐
   │       CODEX HARNESS       │              │       CLAUDE HARNESS      │              │    ANTIGRAVITY HARNESS    │
   │      (OpenAI Codex)       │              │     (Anthropic Claude)    │              │      (Google Gemini)      │
   ├───────────────────────────┤              ├───────────────────────────┤              ├───────────────────────────┤
   │ Worktree:                 │              │ Worktree:                 │              │ Worktree:                 │
   │ D:\Dev\FalloutRemakeCodex │              │ D:\Dev\FalloutRemakeClaude│              │ D:\Dev\FalloutRemakeArgon │
   │ Branch: agent/codex-work  │              │ Branch: agent/claude-work │              │ Branch: agent/argon-work  │
   │ Focus: Domain logic, RPG  │              │ Focus: Combat core,       │              │ Focus: Asset pipeline,    │
   │ rules, dialogs & session  │              │ called shots & combat AI  │              │ conversions & automation  │
   └─────────────┬─────────────┘              └─────────────┬─────────────┘              └─────────────┬─────────────┘
                 │                                          │                                          │
                 └──────────────────────────────────────────┼──────────────────────────────────────────┘
                                                            │
                                                            ▼
                                              ┌───────────────────────────┐
                                              │    INTEGRATION WORKTREE   │
                                              │ (D:\Dev\FalloutRemake...  │
                                              │       Integracao)         │
                                              ├───────────────────────────┤
                                              │ Quality Gate:             │
                                              │ 481 Synthetic Tests       │
                                              │ Pure C# Console (~1.4s)   │
                                              └─────────────┬─────────────┘
                                                            │
                                                            ▼
                                              ┌───────────────────────────┐
                                              │     MAIN BRANCH (PROD)    │
                                              │    (D:\Dev\FalloutRemake) │
                                              └───────────────────────────┘
```

### Core Workflow Pillars:

1. **Physical Isolation via Git Worktrees:**
   - Each AI harness operates in a dedicated, isolated filesystem directory (`D:\Dev\FalloutRemakeCodex`, `FalloutRemakeClaude`, `FalloutRemakeArgon`), all connected to the same underlying Git repository.
   - This prevents file locking collisions, avoids dirty working-tree overwrites, and allows multiple models to develop, run tests, and compile concurrently.

2. **Cross-Vendor Shared Memory (MCP + Living Documentation):**
   - Independent AI models from different providers (OpenAI, Anthropic, Google) share architectural decisions and rules via a **vector memory MCP server (`ai-memory`)** and mandatory canonical project files ([`AGENTS.md`](AGENTS.md) and [`docs/PROJECT_STATUS.md`](docs/PROJECT_STATUS.md)).
   - Prior to editing code, agents inspect past decisions and established contracts, eliminating hallucinations and context loss across sessions.

3. **Ultra-Fast Pure C# Quality Gates (.NET 8):**
   - Domain subsystems (`Import`, `Geometry`, `Runtime`) are pure C# completely decoupled from the Godot engine. The console test suite [`tools/TempleInspector`](tools/TempleInspector/TempleInspector.csproj) executes **481 synthetic test groups in ~1.4 seconds**.
   - Project protocol mandates that tests run before and after every code change. If a single test fails, the modification is rejected immediately.

4. **Non-Intrusive Desktop Execution:**
   - Visual test execution uses [`tools/Run-QuietGodot.ps1`](tools/Run-QuietGodot.ps1), which spawns Godot inside an isolated Windows virtual desktop. Automated tests and screenshot generation run without stealing focus, moving the mouse cursor, or disrupting the developer's active workspace.

5. **Interchangeability & Mutual Verification:**
   - No AI model has a monopoly on any task. Models are assigned dynamically by availability, rate limits, or context suitability. Furthermore, agents must verify each other's work through test evidence rather than blind trust.

### 🎨 Hybrid 3D Asset Creation Pipeline (Multi-Tier Cost & Quality):

To rebuild the isometric environment in 3D while preserving the 1998 artistic identity and optimizing AI inference costs, the project employs a 3-stage multi-model asset pipeline:

```text
  [ Original 2D Sprites ] (Classic Fallout 2 .FRM files)
             │
             ▼
  [ Stage 1: Generative Image AI (Google / Imagen) ]
  • Analyzes original sprites and outputs high-fidelity concept art in chat
  • Establishes orthographic perspectives, anatomical/architectural measurements
             │
             ▼
  [ Stage 2: Rapid Low-Cost Prototyping (Gemini Flash via Antigravity) ]
  • Fast, economical AI generates initial 3D mesh drafts, collision bounds & tools
  • Maps bounding boxes, coordinates, and preliminary layout structures
             │
             ▼
  [ Stage 3: Aesthetic Refinement & Final Topology (Anthropic Claude) ]
  • Rigorous artistic review of proportions, mesh topology, and PBR materials
  • Hardens geometry into the final production .GLB asset ready for Godot
```

1. **Stage 1 — Concept & Scale Specifications (Image Generation AI / Google):** Using the original Fallout 2 2D sprites and renders, the image generation AI produces high-resolution turnaround concept sheets with metric scale and orthographic references directly in the chat session.
2. **Stage 2 — Rapid Prototyping (Gemini Flash via Antigravity):** A fast, cost-effective model (**Gemini Flash**) leverages those specifications in the Antigravity harness to build raw 3D meshes, collision geometry, scene nodes, and pipeline automation scripts.
3. **Stage 3 — Refinement & Production Asset (Anthropic Claude):** **Claude** performs high-precision aesthetic and structural refinement: verifying proportions against classic engine sprites, tuning PBR shaders/materials, and exporting the final production-ready `.glb` model.

---

## 1. Architectural Overview

The codebase enforces strict separation between **domain logic (pure decoupled .NET 8 C#)** and the **presentation layer (Godot 4)**:

```text
src/
├── Import/    (29 files) -> Pure C# (.NET 8). Decodes classic DAT, MAP, PRO, INT, GAM, MSG, FRM, GCD, and AI files.
├── Geometry/  (11 files) -> Pure C# (.NET 8). 40,000-hex grid math, BFS, CE pathfinder, line-of-fire, and wall occlusion.
├── Runtime/   (65 files) -> Pure C# (.NET 8). State machine (WorldSession), CE turn-based combat, progression, medicine, and v1-v13 checkpoints.
└── Preview/   (114 files)-> Godot 4 Presentation. TemplePreview, viewport adapters, combat HUD, modal windows, and in-engine test runners.
```

The entire core game logic, combat resolver, container inventory, and checkpoint persistence can be executed and verified via [`tools/TempleInspector`](tools/TempleInspector/TempleInspector.csproj) **in ~1.4 seconds** with zero graphical dependencies.

---

## 📸 Gameplay & In-Engine Captures (NVIDIA RTX)

All captures below were rendered at high resolution (1440x900) directly from Godot using Direct3D 12 on an NVIDIA GeForce RTX GPU:

| Turn-Based Combat & Called Shots | Creature Encounter (Giant Ants) |
| :---: | :---: |
| ![Called Shots](docs/images/combat-called-shot.png) | ![Combat Encounter](docs/images/combat-encounter.png) |
| *Called Shots targeting window for 8 body parts with AP costs and hit chances* | *Line-of-sight and initiative-driven creature combat inside the Temple of Trials* |

| Critical Hits & Authentic Damage | 3D Modular Temple of Trials Architecture |
| :---: | :---: |
| ![Critical Hit](docs/images/combat-critical.png) | ![Modular Kit](docs/images/temple-architecture-kit.png) |
| *Authentic CE critical hit with Knockdown flag, armor bypass, and combat log* | *Modular stone architecture kit and dynamic torch lighting inside the Temple* |

| SPECIAL Character Creation | Hexagonal Grid & Pathfinding |
| :---: | :---: |
| ![Character Creation](docs/images/character-creation-special.png) | ![Hex Navigation](docs/images/hex-grid-navigation.png) |
| *SPECIAL attribute editor with 40 distribution points, Traits, and Tag Skills* | *40,000-hex grid, CE BFS pathfinder, and visual reachability radius* |

| Door Lockpicking | Container Inventory & Carry Weight |
| :---: | :---: |
| ![Lockpick](docs/images/lockpick-door.png) | ![Inventory Loot](docs/images/inventory-loot.png) |
| *Second Temple door with deterministic Lockpick adapter and 25 XP reward* | *Container loot window with Strength-based carry limits and Take All button* |

| Exterior Environment & PBR Assets | Character Progression & Perks |
| :---: | :---: |
| ![Exterior](docs/images/nature-exterior.png) | ![Perks](docs/images/perks-progression.png) |
| *Temple exterior area with PBR rock and burnt tree models (Assets/Nature)* | *Classic Perk table browser with level prerequisites and integrated effects* |

---

## 2. Implemented Subsystems

### ⚔️ Turn-Based Tactical Combat (Fallout 2 CE Parity)
- **Pure C# Combat Engine:** Implemented in [`src/Runtime/Combat/`](src/Runtime/Combat/) (`CombatSession.cs`, `DamageCalculator.cs`, `CriticalRules.cs`, `CombatAi.cs`, `CombatInitiative.cs`).
- **Action Points (AP) & Initiative:** Deterministic turn order calculated by Sequence and Perception; AP costs for movement, attacks, reloading, and accessing the inventory mid-fight (4 AP).
- **Damage Calculations:** Exact application of *Damage Threshold* (DT) and *Damage Resistance* (DR) derived from classic Fallout 2 weapon and armor prototypes.
- **Targeted Shots (Called Shots):** Dedicated aiming interface ([`CalledShotWindow.cs`](src/Preview/CalledShotWindow.cs)) targeting Head, Eyes, Torso, Right Arm, Left Arm, Right Leg, Left Leg, and Groin, with accurate accuracy penalties and AP modifiers.
- **Authentic Critical Tables:** Over 66 KB of authentic table data in [`CriticalRules.cs`](src/Runtime/Combat/CriticalRules.cs), mirroring the critical hit and failure tables of `fallout2.exe`.
- **Firearms & Ballistics:** Magazine loads, world and combat reloading, burst spray dispersion (`_compute_spray` and `_shoot_along_path`), and weapon perks (Long Range, Scope Range, Accurate, Penetrate, Fast Reload).
- **Melee, Unarmed & Thrown Weapons:** Switching between punch and kick with unarmed bonus damage; thrown spear mechanics consuming weapon from hand and depositing the item onto the target hex.
- **Dual Hand Slots & Armor:** Full support for Fallout 2's dual active hand slots and equipped armor slots granting Armor Class (AC), stat bonuses, and damage resistances.
- **Creature Combat AI:** AI decision trees based on Community Engine `combat_ai` and original `ai.txt` packets.
- **Temple of Trials Encounters:** Integrated fights with Giant Ants and Radscorpions with accumulating poison on the game clock ([`PoisonRules.cs`](src/Runtime/Combat/PoisonRules.cs)).
- **Cameron's Challenge:** Three verified paths: diplomatic Speech persuasion, an unarmed duel until yield, or lethal combat with Temple Key recovery from Cameron's body.

### 📜 Dialogue, Quests & Arroyo Transition
- **Arroyo Elder Dialogue:** Full dialogue tree in [`src/Runtime/ArroyoElderConsultation.cs`](src/Runtime/ArroyoElderConsultation.cs) covering initial conversation (`Node001`), repeated talks (`Node004`), and GECK quest acceptance and reminder branches (`Node015`, `Node007`, `Node016`).
- **Quest Rewards & Flags:** Atomically awards 152 Caps (`PID 41`) and Vic's Flask (`PID 267`), updating global variables (`GVAR_TALKED_TO_ELDER`, `GVAR_DEN_VIC_KNOWN`) and local script states in checkpoints.
- **Area Transition & Karma:** Exit through Temple Door 100 with transition to `maps/arvillag.map`, 600 XP award (or 300 XP if Cameron was slain), and Karma/Reputation tracking.

### 👤 Character Creation, Stats, Progression & Survival
- **Character Creation:** 40 SPECIAL attribute points, selection of up to 2 Traits and 3 Tag Skills.
- **Original Premades:** Direct parsing of original `.gcd` character files (`Narg`, `Mingan`, and `Chitsa`).
- **Level Progression:** XP accumulation, level ups, and progressive skill point distribution using classic editor rules.
- **Perks & Traits:** Integrated effects for *Lifegiver*, *Educated*, *Tag!*, *Mutate!*, *Here and Now*, *Swift Learner*, *One Hander*, and *Jinxed*.
- **Medicine & Healing:** Usable *First Aid* and *Doctor* skills with medical kits and 3-uses/day limits; Healing Powder and Pip-Boy natural rest (*Pipboy Rest* with safe map checks).

### 📦 Inventory, Carry Weight & Item Management
- **Strict Carry Weight:** Weight capacity calculated via Strength ($25 + \text{Strength} \times 25\text{ lb}$), validated atomically before moving any stack.
- **Fast Container Transfer:** Atomically verified *Take All* and *Return All* preserving item origins and preventing item duplication.
- **Temple Plastic Explosives:** Timed explosive placement (10 to 180 seconds), detonating on the game clock to destroy the barrier obstacle and open passage to elevation 2.
- **Lockpicking & Doors:** Deterministic Lockpick session adapter granting 25 one-time XP; physical grid-blocking doors.

### 💾 Checkpoints & Session Persistence (v1 to v13)
- **Multi-Map Atomic Serialization:** JSON-based persistence preserving doors, containers, placed items, inventory bag, combat state, weapon loads, perks, and Elder dialogue progress (`SavedElderState`).
- **Backward Compatibility:** Automatic migration pipeline for legacy saves (`v1` through `v12`) to the consolidated `v13` schema.

### 🗺️ Native Binary Parsing & Hexagonal Grid
- **Direct Asset Extraction:** Reads classic game formats directly without unpacking (`master.dat`, `critter.dat`, `*.map`, `*.pro`, `*.int`, `*.frm`, `*.msg`, `*.gam`).
- **Faithful 40,000-Hex Grid:** 3D/Hex coordinate conversion, CE BFS pathfinder respecting `NO_BLOCK`, `MULTIHEX`, and elevations 0–2.
- **3D Presentation:** Modular stone architecture kit for the Temple of Trials, PBR nature models (rocks, burnt trees), and automatic wall occlusion.

---

## 3. How to Run

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Godot Engine 4 .NET (C#)](https://godotengine.org/download) (version 4.3 or 4.7 Mono)
- A classic **Fallout 2** installation (to supply `master.dat` and `critter.dat`). *No proprietary assets are bundled in this repository.*

### Build & Synthetic Tests (.NET CLI)

From the project root:

```powershell
# 1. Build the Godot C# project and tooling
dotnet build 'Fallout Remake.csproj'

# 2. Run the 481 synthetic test groups (~1.4s)
dotnet run --project tools/TempleInspector -- --self-test
```

### Data Compliance & Original Binary Verification

To inspect map structure and execute byte-by-byte reconstruction tests:

```powershell
$dataDir = "C:\Path\To\Fallout2"

# Inspect map integrity and object structures
dotnet run --project tools/TempleInspector -- --data-dir $dataDir

# Generate firearm rules compliance report
dotnet run --project tools/TempleInspector -- --data-dir $dataDir --firearms-report

# Generate Arroyo Elder dialogue report
dotnet run --project tools/TempleInspector -- --data-dir $dataDir --elder-report
```

### Launch Godot Interactive Presentation

1. Open `project.godot` with **Godot Engine (.NET)**.
2. Press **F5** (or click Play).
3. On first startup, point the file picker to your Fallout 2 directory containing `master.dat` and `critter.dat`. Settings are cached locally in `.local/`.

### 🎥 Record Gameplay with NVIDIA Tools (NVIDIA App / ShadowPlay)

The project supports 60 FPS hardware-accelerated video recording via NVENC on NVIDIA GPUs:

1. Launch the interactive game scene:
   ```powershell
   & "D:\Downloads\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe" --path .
   ```
2. Use standard NVIDIA overlay shortcuts:
   - **`Alt + F9`**: Start or stop recording MP4 video.
   - **`Alt + Z`**: Open the NVIDIA App overlay to configure resolution, bit rate, and microphone settings.
3. Recorded clips are saved automatically to your NVIDIA recordings folder (`%USERPROFILE%\Videos\NVIDIA\Desktop\`).

---

## 4. Key Controls (`TemplePreview`)

| Key / Action | Function |
| :--- | :--- |
| **WASD** / Right Mouse Button | Pan isometric free camera |
| **Q / E** | Rotate camera view |
| **Mouse Wheel** | Zoom in / Zoom out |
| **C** | Center camera and track player character |
| **M** | Toggle Walk mode (click ground to pathfind along hexes) |
| **U** | Interact with door (Open / Close) |
| **L** | Pick locked door (Lockpick) |
| **J** | Loot container (*Take All* / Specific quantity transfer) |
| **B** | Open Player Bag (Inventory and carry weight management) |
| **I** | Inspect original nested container inventory |
| **K** | **Save Checkpoint** to local slot |
| **R** | **Load Checkpoint** from local slot |
| **V** | Toggle automatic Wall Occlusion cutaway |
| **G** | Show / Hide hexagonal navigation grid |
| **O** | Toggle original 2D floor texture overlay |
| **Enter** | Confirm area or elevation transition traversal |
| **Esc** | Stop walking on current step / Clear selection |

---

## 5. Development & AI Protocol

All contributors and autonomous agents must follow the engineering guidelines set out in [`AGENTS.md`](AGENTS.md):

1. **Mandatory Test Verification:** The 481-test synthetic suite (`tools/TempleInspector --self-test`) must be run and pass before and after any C# edit.
2. **Non-Intrusive Environment:** Godot runs for automated visual tests must use `--headless` or the isolated desktop runner [`tools/Run-QuietGodot.ps1`](tools/Run-QuietGodot.ps1).
3. **Asset & Binary Preservation:** Never alter original Fallout 2 PIDs, FIDs, SIDs, MSG text files, or classic data archives.

---

## 6. License & Acknowledgments

- **Source Code:** Distributed under the [MIT License](LICENSE).
- **Fallout 2:** Registered trademark of Bethesda Softworks LLC / ZeniMax Media. This is a non-commercial educational reverse-engineering study.
- **Fallout 2 Community Engine (CE):** Special thanks to Alex Batalov's [fallout2-ce](https://github.com/alexbatalov/fallout2-ce) project for reference algorithms, opcodes, and data structures.
