# Gymageddon — Implementation Plan

## What Has Been Implemented (Code)

### Architecture
| Script | Purpose |
|--------|---------|
| `Data/CharacterData.cs` | ScriptableObject defining fighter stats (HP, damage, speed, cost, colour) |
| `Data/TrainerData.cs` | ScriptableObject defining trainer effect, energy cost, colour |
| `Data/EnemyData.cs` | ScriptableObject defining enemy stats and energy reward |
| `Data/WaveData.cs` | ScriptableObject describing an enemy wave (groups, delays, counts) |
| `Core/GameEvents.cs` | Static event bus (energy changed, placement, enemy reached base, etc.) |
| `Core/Lane.cs` | Single lane: enforces **1 character + 1 trainer** rule, manages slots |
| `Core/GameBoard.cs` | Manages all **5 lanes**, exposes placement API |
| `Core/GameManager.cs` | Game-state machine (Playing → Victory / Defeat), wires sub-systems |
| `Entities/Unit.cs` | Base class: health, procedural health bar, death handling |
| `Entities/Character.cs` | Gym fighter: attacks nearest enemy in same lane, applies trainer buffs |
| `Entities/Trainer.cs` | Support equipment: buffs character in same lane, or generates energy |
| `Entities/Enemy.cs` | Advances left along its lane, attacks blocking characters |
| `Managers/ResourceManager.cs` | Energy resource: passive regen + kill rewards |
| `Managers/WaveManager.cs` | Spawns enemy waves with configurable groups and delays |
| `Managers/PlacementManager.cs` | Handles mouse clicks to select and place units |
| `UI/GameUI.cs` | Builds HUD (energy counter, wave indicator, unit buttons, game-over overlay) |
| `Bootstrap/GameBootstrap.cs` | **Attach to one empty GameObject** — builds the entire scene procedurally |

### How to Run (no art required)
1. Open `Assets/Scenes/SampleScene` in the Unity Editor.
2. Create a new empty **GameObject** (right-click Hierarchy → Create Empty), name it `GameBootstrap`.
3. Drag `Assets/Scripts/Bootstrap/GameBootstrap.cs` onto it.
4. Press **Play**.

The Bootstrap script will:
- Configure the camera.
- Create 5 coloured lanes with labelled Trainer (T) and Character (C) slots.
- Create three default fighters (Boxer, Weightlifter, Cardio Runner) and three trainers (Weight Rack, Treadmill, Protein Bar).
- Wire up all managers and start three enemy waves.
- Build the UI bar at the bottom of the screen with clickable unit buttons.

**Controls:**
- Click a unit button in the bottom bar to select it.
- Click a lane's T or C area on screen to place the unit.
- Enemies automatically enter from the right.

---

## What Needs to Be Done (Plan)

### 1. Art Assets

#### 1.1 Character Sprites
Each fighter needs at minimum three animation states: **Idle**, **Attack**, **Death**.

| Character | Style hint |
|-----------|-----------|
| **Boxer** | Humanoid in red boxing gloves, athletic build |
| **Weightlifter** | Broad shoulders, barbell |
| **Cardio Runner** | Lean figure, running shoes |

**Tools recommended:** Aseprite (pixel art), or any spritesheet tool.

**Steps:**
1. Create a 64×64 px spritesheet per character with at least 4 frames per animation.
2. Import to Unity (`Assets/Sprites/Characters/`).
3. In the Sprite Editor, slice the sheet into individual frames.
4. Create an **Animator Controller** (`Assets/Animations/Characters/<name>.controller`).
5. Add Idle, Attack, Death animation clips.
6. In `Character.cs → ApplyVisual()`, replace `SpriteRenderer.color` with the imported sprite.

#### 1.2 Trainer Sprites
Each trainer needs an **Idle** and optionally a **Active** (glowing / spinning) animation.

| Trainer | Style hint |
|---------|-----------|
| **Weight Rack** | Metallic rack with weights |
| **Treadmill** | Belt moving |
| **Protein Bar** | Box with shaker bottle |

#### 1.3 Enemy Sprites
| Enemy | Style hint |
|-------|-----------|
| **Couch Potato** | Round figure in pajamas |
| **Fast Food Fan** | Figure holding burger and soda |
| **Fitness Skeptic** | Smug figure with arms crossed |

#### 1.4 Background / Tiles
- Create a repeating gym floor tile (wooden planks or rubber floor) for each lane background.
- Optional: add gym equipment decoration in the mid-ground (parallax layer).
- Import as a Tilemap into a Tilemap layer.

---

### 2. Animator Integration

For each entity class, replace the procedural coloured square with a proper Animator:

```csharp
// In Character.cs (and Enemy.cs)
Animator _animator;

private void ApplyVisual(CharacterData data)
{
    SpriteRenderer sr = GetComponent<SpriteRenderer>();
    sr.sprite = data.idleSprite;          // add Sprite field to CharacterData

    _animator = GetComponent<Animator>();
    if (_animator == null) _animator = gameObject.AddComponent<Animator>();
    _animator.runtimeAnimatorController = data.animatorController; // add field to CharacterData
}

// When attacking:
_animator.SetTrigger("Attack");

// On death:
_animator.SetTrigger("Death");
```

Add `public Sprite idleSprite` and `public RuntimeAnimatorController animatorController` to each `*Data` ScriptableObject.

---

### 3. Prefabs

Convert each unit type into a proper **Prefab**:

1. Create a new **GameObject** in the Hierarchy.
2. Add `SpriteRenderer`, `Animator`, `BoxCollider2D`, `Character` (or `Trainer`/`Enemy`) component.
3. Drag the Animator Controller onto the Animator component.
4. Drag the GameObject from Hierarchy into `Assets/Prefabs/Characters/` to create the Prefab.
5. In `PlacementManager.cs`, replace `CreateUnitGameObject()` with `Instantiate(data.prefab)`.
6. Add `public GameObject prefab` field to each `*Data` ScriptableObject.

---

### 4. Audio

**Required clips:**
- Boxer punch, Weightlifter grunt, Runner footsteps (attack sounds)
- Enemy approach footstep loop
- UI button click
- Coin/energy collect sound
- Victory fanfare, Defeat sting

**Steps:**
1. Import `.ogg` / `.wav` into `Assets/Audio/`.
2. Create an `AudioManager` singleton with a pooled `AudioSource[]`.
3. Add `public AudioClip attackClip` to entity data ScriptableObjects.
4. Play clip from `Character.cs` / `Enemy.cs` in the attack coroutine.

---

### 5. Particle Effects

Add visual feedback:
- **Hit sparks** — played when an enemy takes damage (red particles).
- **Level-up burst** — played when a trainer buffs a character.
- **Death pop** — played when a unit dies.

**Steps:**
1. Create Particle Systems in the editor and save as Prefabs in `Assets/VFX/`.
2. Spawn them via `Instantiate(hitVFXPrefab, hitPosition, Quaternion.identity)`.
3. Auto-destroy after ~1 second.

---

### 6. Tile Map / Scene Layout

Replace the procedural lane rendering in `GameBootstrap.cs` with a proper Tilemap:

1. Add two **Tilemap** layers to the scene: `Ground` (z=0) and `Decorations` (z=-1).
2. Paint the 5 lanes using the Tile Palette in the Tile Map Editor.
3. Mark Trainer column cells with a custom `TrainerTile` (green tint), Character column with a `CharacterTile` (blue tint).
4. In `GameBootstrap`, remove the `CreateColoredQuad` calls and reference the Tilemap directly.

---

### 7. UI Polish

Current UI uses legacy `UnityEngine.UI.Text`. Upgrade to **TextMeshPro**:

1. Install TMP via Package Manager (usually already included).
2. In `GameUI.cs`, replace `using UnityEngine.UI` Text with `TMPro.TextMeshProUGUI`.
3. Use a custom font (a free gym/sport font works well — e.g., **Oswald** from Google Fonts).
4. Add icons (small sprites) next to energy / wave counters for polish.

---

### 8. Main Menu & Scene Management

Add a Main Menu scene:

1. Create `Assets/Scenes/MainMenu.unity`.
2. Add a Canvas with:
   - Game logo (TextMeshPro or sprite).
   - **Play** button → loads `SampleScene`.
   - **Quit** button → `Application.Quit()`.
3. Add both scenes to **File → Build Settings**.
4. In `GameManager.cs`, on Victory / Defeat show an overlay with a **Restart** button that calls `SceneManager.LoadScene("SampleScene")`.

---

### 9. ScriptableObject Assets for Designer Tweaking

To avoid hard-coded values in `GameBootstrap.cs`:

1. Right-click `Assets/Data/` → **Create → Gymageddon → Character Data** to create a `BoxerData.asset`.
2. Fill in the Inspector fields (name, HP, damage, cost, sprites, animator, etc.).
3. Create a `GameConfig.asset` ScriptableObject listing all characters, trainers, and waves for the level.
4. In `GameBootstrap.cs`, accept a `GameConfig` reference via `[SerializeField]` and remove `CreateDefaultData()`.

---

### 10. Build & Publish

1. Open **File → Build Settings**.
2. Add `SampleScene` (and `MainMenu` if created).
3. Select target platform (PC, WebGL, Android, etc.).
4. Click **Build** and choose output folder.
5. For WebGL: upload the output to **itch.io** or GitHub Pages.

---

## Priority Order

| Priority | Task |
|----------|------|
| 🔴 High | Art assets (characters, enemies, trainers) — game looks like coloured squares without them |
| 🔴 High | Animator integration (idle / attack / death) |
| 🟡 Medium | Prefabs + ScriptableObject assets (designer-friendly) |
| 🟡 Medium | Audio |
| 🟢 Low | Particle effects |
| 🟢 Low | Tilemap background |
| 🟢 Low | Main Menu scene |
| 🟢 Low | TextMeshPro upgrade |
| ⚪ Optional | WebGL build + itch.io publish |
