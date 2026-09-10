# Project Overview
- Game Title: Friendslop - The Game
- High-Level Concept: An ambient city/park life simulation featuring autonomous NPCs navigating the environment, wandering, socializing, ordering food, and relaxing on benches.
- Players: Single player / Multi-agent environment
- Inspiration / Reference Games: Animal Crossing, The Sims, ambient city simulators
- Tone / Art Direction: Cartoon low-poly stylized city
- Target Platform: PC (StandaloneWindows64)
- Screen Orientation / Resolution: Landscape 1920x1080
- Render Pipeline: Universal Render Pipeline (URP)

# Game Mechanics
## Core Gameplay Loop
Autonomous NPCs wander through zones in the park/city, select stations (Food Stands, Benches) or socialize with other NPCs. When an NPC chooses to interact with a bench:
1. The NPC finds an available bench station via `StationRegistry`.
2. It reserves an open seat slot on that bench and pathfinds to the seat position using `NAVAgentController` / `NavMeshAgent`.
3. Upon reaching the seat, the NPC aligns its rotation to face the seat's facing direction.
4. `BenchManager.OnNPCEnter` triggers the NPC's sitting state, selecting a random sitting animation from the 5 standard sitting clips.
5. The NPC remains seated for the randomized occupation duration.
6. When the duration elapses, `BenchManager.OnNPCExit` returns the NPC to standing/idle, releases the reserved seat slot, and the NPC selects its next action in the loop.

## Controls and Input Methods
The system is simulation-driven and autonomous:
- Navigation: Driven by `NavMeshAgent` destination waypoints.
- State Machine & Station Interactivity: Controlled by `NPCActionController`, `NPCAnimationController`, and `BenchManager`.

# UI
This feature is focused on NPC world interaction and environmental animation; in-game HUD is unaffected. Debug gizmos / console logs can visually trace station registrations and occupancy.

# Key Asset & Context
- Scripts:
  - `Assets/_Scripts/System/NPCActions/BenchManager.cs`: Handles station entry/exit hooks for bench seats and triggers sitting states on occupants.
  - `Assets/_Scripts/System/NPCActions/NPCAnimationController.cs`: Manages dynamic `AnimatorOverrideController` swaps for idle, walk, talk, and sitting animations.
  - `Assets/_Scripts/System/NPCActions/NPCActionController.cs`: Coordinates NPC decision loop, state transitions, and station entry/exit calls.
- Animator Controller:
  - `Assets/_Animations/Controller/NPC_Locomotion.controller`: Base controller updated with `IsSitting` bool parameter and `Sit` state transitions (Idle <-> Sit).
- Animation Assets (5 Standard Sitting Clips):
  - `Assets/_Animations/NPC/Sitting/Standard/Sit.anim`
  - `Assets/_Animations/NPC/Sitting/Standard/Sit 2.anim`
  - `Assets/_Animations/NPC/Sitting/Standard/Sit 3.anim`
  - `Assets/_Animations/NPC/Sitting/Standard/Sit 4.anim`
  - `Assets/_Animations/NPC/Sitting/Standard/Sit 5.anim`
- Bench Prefabs:
  - `Assets/CartoonLowPolyCity/Prefabs/Props/Bench_01.prefab` (Park bench with backrest)
  - `Assets/CartoonLowPolyCity/Prefabs/Props/Bench_02.prefab` (Backless bench)
  - `Assets/CartoonLowPolyCity/Prefabs/Props/Bench_03.prefab` (Picnic table bench)
- NPC Prefabs:
  - All 9 NPC prefabs under `Assets/_Prefabs/Game Entities/NPC/`.

# Implementation Steps

### Step 1: Update `NPC_Locomotion.controller` with Sitting State
- **Description**: Add `IsSitting` (Bool) parameter to `NPC_Locomotion.controller`. Create a `Sit` state with `Sit.anim` as placeholder motion. Add transitions between `Idle` and `Sit` (`Idle -> Sit` when `IsSitting == true` without exit time; `Sit -> Idle` when `IsSitting == false` without exit time).
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

### Step 2: Extend `NPCAnimationController.cs` for Sitting Animations
- **Description**:
  - Add serialized fields: `List<AnimationClip> _sittingAnimations = new();` and `string _sittingClipName = "Sit";`.
  - Add hash `IsSittingParam = Animator.StringToHash("IsSitting");`.
  - Add public method `StartSitting()`: Selects a random clip from `_sittingAnimations`, assigns it to `_overrideController[_sittingClipName]`, and sets `_animator.SetBool(IsSittingParam, true)`.
  - Add public method `StopSitting()`: Sets `_animator.SetBool(IsSittingParam, false)`.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

### Step 3: Update `NPCActionController.cs` & `BenchManager.cs`
- **Description**:
  - In `NPCActionController.cs`: Add convenience methods `StartSitting()` and `StopSitting()` delegating to `_animationController`. Ensure `OnDisable()` cleans up sitting state if disabled while seated.
  - In `BenchManager.cs`: In `OnNPCEnter`, call `npc.StartSitting()` (and safely null-check `GetSeatAnimator(slot)?.SetBool(...)`). In `OnNPCExit`, call `npc.StopSitting()` (and safely null-check `GetSeatAnimator(slot)?.SetBool(...)`).
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: No

### Step 4: Configure Bench Prefabs and Scene Instances
- **Description**:
  - Add `BenchManager` component and child seat slot transforms to `Bench_01.prefab`, `Bench_02.prefab`, and `Bench_03.prefab`:
    - `Bench_01`: 2 seat slots (`Slot_0` at `(-0.45, 0, 0)`, `Slot_1` at `(0.45, 0, 0)`), facing `(0, 0, 0)`.
    - `Bench_02`: 2 seat slots (`Slot_0` at `(-0.5, 0, 0)`, `Slot_1` at `(0.5, 0, 0)`), facing `(0, 0, 0)`.
    - `Bench_03`: 4 seat slots (`Slot_0` at `(-0.5, 0, -0.85)`, `Slot_1` at `(0.5, 0, -0.85)` facing `(0, 0, 0)`; `Slot_2` at `(-0.5, 0, 0.85)`, `Slot_3` at `(0.5, 0, 0.85)` facing `(0, 180, 0)`).
  - Assign `_slots` array on `BenchManager` on each prefab.
  - Ensure all 35 bench instances in `MainScene.unity` reflect the updated prefab configuration with `BenchManager` and slots properly assigned.
- **Assigned role**: developer
- **Dependencies**: Step 3
- **Parallelizable**: No

### Step 5: Assign Sitting Animations to all NPC Prefabs
- **Description**:
  - For all 9 NPC prefabs in `Assets/_Prefabs/Game Entities/NPC/`, assign the 5 sitting animation clips (`Sit.anim`, `Sit 2.anim`, `Sit 3.anim`, `Sit 4.anim`, `Sit 5.anim`) to the `_sittingAnimations` list on their `NPCAnimationController` component.
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: Yes

# Verification & Testing
1. **Scene Bench Verification**: Verify via script that all 35 bench objects in `MainScene` have `BenchManager` attached, non-empty `_slots` arrays, and register with `StationRegistry.Register`.
2. **NPC Prefab Verification**: Verify that all 9 NPC prefabs have 5 sitting clips populated in `_sittingAnimations` and proper references to `NPC_Locomotion.controller`.
3. **Animator Controller Verification**: Verify the `NPC_Locomotion.controller` transitions and parameters (`IsSitting` boolean and `Sit` state).
4. **Runtime Simulation Test**: Run a play-mode simulation test:
   - Spawn NPCs or let `NPCSpawner` spawn NPCs.
   - Observe NPCs picking `NPCActionType.Bench`, navigating to bench slots, sitting down with varied animations from the 5 clips, sitting for their occupation duration, and cleanly transitioning back to walking/idling when exiting the bench.
