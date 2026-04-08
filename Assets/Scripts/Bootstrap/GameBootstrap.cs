using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Gymageddon.Core;
using Gymageddon.Data;
using Gymageddon.Entities;
using Gymageddon.Managers;
using Gymageddon.UI;

namespace Gymageddon.Bootstrap
{
    /// <summary>
    /// Attach this script to ONE empty GameObject in the SampleScene.
    /// On Start it procedurally builds the entire Gymageddon game:
    ///   • 5 horizontal lanes (coloured strips), each with a Character slot and a Trainer slot
    ///   • All manager objects (ResourceManager, WaveManager, PlacementManager, GameManager)
    ///   • A fully functional UI (energy bar, wave display, unit buttons, overlay)
    ///
    /// No additional prefabs or assets are required — all visuals are procedurally generated
    /// from coloured sprites at runtime.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        // ─── Layout constants ──────────────────────────────────────────────────
        private const float CAM_ORTHO_SIZE  = 5.5f;   // camera half-height in world units
        private const float BOARD_LEFT      = -9f;    // x: left edge of board (player base)
        private const float BOARD_RIGHT     =  9f;    // x: right edge of board (enemy spawn)
        private const float TRAINER_X       = -7.5f;  // x: centre of trainer slot column
        private const float CHARACTER_X     = -6f;    // x: centre of character slot column
        private const float SLOT_COLUMN_W   =  1.2f;  // width of a slot column
        private const float LANE_HEIGHT     =  1.8f;  // height of one lane
        private const float FIRST_LANE_Y    =  3.6f;  // y centre of top lane
        private const float LEFT_WALL_X     = -8.5f;  // x boundary — enemy reaching here triggers base damage
        private const float ENEMY_SPAWN_X   =  8.5f;  // x where enemies are spawned

        // ─── Palette ───────────────────────────────────────────────────────────
        private static readonly Color[] LANE_COLORS =
        {
            new Color(0.18f, 0.22f, 0.30f),
            new Color(0.16f, 0.25f, 0.22f),
            new Color(0.22f, 0.18f, 0.28f),
            new Color(0.18f, 0.24f, 0.28f),
            new Color(0.24f, 0.20f, 0.18f),
        };
        private static readonly Color TRAINER_SLOT_COLOR    = new Color(0.15f, 0.40f, 0.20f, 0.5f);
        private static readonly Color CHARACTER_SLOT_COLOR  = new Color(0.15f, 0.30f, 0.55f, 0.5f);
        private static readonly Color ENEMY_ZONE_COLOR      = new Color(0.30f, 0.10f, 0.10f, 0.6f);
        private static readonly Color DIVIDER_COLOR         = new Color(0.05f, 0.05f, 0.05f, 1f);
        private static readonly Color BASE_WALL_COLOR       = new Color(0.10f, 0.20f, 0.50f, 1f);

        // ─── Lifecycle ─────────────────────────────────────────────────────────
        private void Awake()
        {
            ConfigureCamera();
        }

        private void Start()
        {
            // ── 0. EventSystem (required for UI drag-and-drop events) ──
            EnsureEventSystem();

            // ── 1. Data ────────────────────────────────────────────────
            var (chars, trainers, waves) = CreateDefaultData();

            // ── 2. Managers ────────────────────────────────────────────
            ResourceManager resMgr  = CreateManager<ResourceManager>("ResourceManager");
            WaveManager     waveMgr = CreateManager<WaveManager>("WaveManager");
            PlacementManager plcMgr = CreateManager<PlacementManager>("PlacementManager");
            GameBoard        board  = CreateManager<GameBoard>("GameBoard");

            // ── 3. Board & lanes ───────────────────────────────────────
            float[] laneYPositions = new float[GameBoard.LANE_COUNT];
            Lane[]  lanes          = new Lane[GameBoard.LANE_COUNT];

            CreateBoardBackground();

            for (int i = 0; i < GameBoard.LANE_COUNT; i++)
            {
                float y = FIRST_LANE_Y - i * LANE_HEIGHT;
                laneYPositions[i] = y;
                lanes[i] = CreateLane(i, y, LANE_COLORS[i]);
                board.RegisterLane(lanes[i]);
            }

            CreateBoardDecorations();

            // ── 4. Wire managers ───────────────────────────────────────
            plcMgr.Init(board, resMgr, lanes);

            foreach (WaveData wd in waves) waveMgr.AddWave(wd);
            waveMgr.SetLaneYPositions(laneYPositions);
            waveMgr.SetCardPool(chars, trainers);

            // ── 5. GameManager ─────────────────────────────────────────
            GameManager gm = CreateManager<GameManager>("GameManager");
            gm.Board     = board;
            gm.Resources = resMgr;
            gm.Waves     = waveMgr;
            gm.Placement = plcMgr;

            // ── 6. UI ──────────────────────────────────────────────────
            GameObject uiGO = new GameObject("GameUI");
            GameUI ui = uiGO.AddComponent<GameUI>();
            ui.SetUnitOptions(chars, trainers);
        }

        // ─── EventSystem ───────────────────────────────────────────────────────
        /// <summary>
        /// Ensures an EventSystem exists. Required for UI drag events.
        /// </summary>
        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;

            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }

        // ─── Camera ────────────────────────────────────────────────────────────
        private void ConfigureCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            cam.orthographic     = true;
            cam.orthographicSize = CAM_ORTHO_SIZE;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.backgroundColor  = new Color(0.05f, 0.05f, 0.10f);
            cam.clearFlags       = CameraClearFlags.SolidColor;
        }

        // ─── Board background ──────────────────────────────────────────────────
        private void CreateBoardBackground()
        {
            // Enemy spawn zone (right red strip)
            float boardH = GameBoard.LANE_COUNT * LANE_HEIGHT;
            float midY   = FIRST_LANE_Y - (GameBoard.LANE_COUNT - 1) * 0.5f * LANE_HEIGHT;
            float zoneW  = BOARD_RIGHT - CHARACTER_X - SLOT_COLUMN_W * 0.5f;

            CreateColoredQuad("EnemyZone",
                new Vector3((BOARD_RIGHT + CHARACTER_X + SLOT_COLUMN_W * 0.5f) * 0.5f, midY),
                new Vector3(zoneW, boardH), ENEMY_ZONE_COLOR, -1);
        }

        private void CreateBoardDecorations()
        {
            float boardH = GameBoard.LANE_COUNT * LANE_HEIGHT;
            float midY   = FIRST_LANE_Y - (GameBoard.LANE_COUNT - 1) * 0.5f * LANE_HEIGHT;

            // Player base wall
            CreateColoredQuad("BaseWall",
                new Vector3(BOARD_LEFT, midY),
                new Vector3(0.3f, boardH + 0.2f), BASE_WALL_COLOR, 1);

            // Dividers between lanes
            for (int i = 0; i <= GameBoard.LANE_COUNT; i++)
            {
                float y = FIRST_LANE_Y + LANE_HEIGHT * 0.5f - i * LANE_HEIGHT;
                CreateColoredQuad($"Divider_{i}",
                    new Vector3(0f, y),
                    new Vector3(BOARD_RIGHT - BOARD_LEFT, 0.05f),
                    DIVIDER_COLOR, 1);
            }

            // Column header labels (legend)
            CreateWorldLabel("T", new Vector3(TRAINER_X, FIRST_LANE_Y + LANE_HEIGHT * 0.6f),
                new Color(0.3f, 0.9f, 0.4f));
            CreateWorldLabel("C", new Vector3(CHARACTER_X, FIRST_LANE_Y + LANE_HEIGHT * 0.6f),
                new Color(0.3f, 0.5f, 0.9f));
            CreateWorldLabel("ENEMIES →", new Vector3(2f, FIRST_LANE_Y + LANE_HEIGHT * 0.6f),
                new Color(0.9f, 0.3f, 0.3f), 0.3f);
        }

        // ─── Lane construction ─────────────────────────────────────────────────
        private Lane CreateLane(int index, float yCenter, Color laneColor)
        {
            float laneWidth   = BOARD_RIGHT - BOARD_LEFT;
            float laneCenterX = (BOARD_RIGHT + BOARD_LEFT) * 0.5f;

            // ── Lane root (NO scale — children scale independently) ──
            GameObject laneGO = new GameObject($"Lane_{index}");
            laneGO.transform.position = new Vector3(laneCenterX, yCenter, 0f);

            // ── Background child (scaled to full lane size) ──
            GameObject bgGO = new GameObject("Background");
            bgGO.transform.SetParent(laneGO.transform, false);
            bgGO.transform.localPosition = Vector3.zero;
            bgGO.transform.localScale    = new Vector3(laneWidth, LANE_HEIGHT, 1f);
            SpriteRenderer bgSR = bgGO.AddComponent<SpriteRenderer>();
            bgSR.sprite       = CreateColoredSprite(laneColor);
            bgSR.sortingOrder = 0;

            // ── Full-lane clickable collider on root (world-unit size, no parent scale) ──
            BoxCollider2D col = laneGO.AddComponent<BoxCollider2D>();
            col.size = new Vector2(laneWidth, LANE_HEIGHT);

            // Lane index label (far left — world space, no parent scale issues)
            CreateWorldLabel($"{index + 1}",
                new Vector3(BOARD_LEFT + 0.4f, yCenter), Color.gray * 0.8f, 0.25f);

            // ── Slot indicators (worldPositionStays=true so their scale is independent) ──
            Transform trainerSlot = CreateSlotIndicator(
                $"TrainerSlot_{index}", laneGO.transform,
                new Vector3(TRAINER_X, yCenter), TRAINER_SLOT_COLOR, "T");

            Transform charSlot = CreateSlotIndicator(
                $"CharSlot_{index}", laneGO.transform,
                new Vector3(CHARACTER_X, yCenter), CHARACTER_SLOT_COLOR, "C");

            // ── Attach Lane component ──
            Lane lane = laneGO.AddComponent<Lane>();
            lane.SetBaseColor(laneColor);
            lane.SetBackgroundRenderer(bgSR);
            lane.Init(index, charSlot, trainerSlot);

            return lane;
        }

        /// <summary>
        /// Creates a slot indicator GO as a child of <paramref name="parent"/>.
        /// Uses worldPositionStays=true so the local scale equals the world scale.
        /// </summary>
        private Transform CreateSlotIndicator(string name, Transform parent,
            Vector3 worldPos, Color color, string letter)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position   = worldPos;
            go.transform.localScale = new Vector3(SLOT_COLUMN_W, LANE_HEIGHT * 0.85f, 1f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = CreateColoredSprite(color);
            sr.sortingOrder = 1;

            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;

            CreateWorldLabel(letter, worldPos + new Vector3(0f, LANE_HEIGHT * 0.3f, -0.1f),
                Color.white * 0.6f, 0.2f);

            return go.transform;
        }

        // ─── Default game data (no assets needed) ──────────────────────────────
        private (List<CharacterData>, List<TrainerData>, List<WaveData>) CreateDefaultData()
        {
            // ── Characters ─────────────────────────────────────────────
            var chars = new List<CharacterData>
            {
                MakeCharacter("Boxer",         100, 20, 1.0f, 3f, 100, new Color(0.29f, 0.56f, 0.89f)),
                MakeCharacter("Weightlifter",  200, 40, 0.5f, 2f, 150, new Color(0.55f, 0.27f, 0.07f)),
                MakeCharacter("Cardio Runner", 70,  12, 1.8f, 4f, 75,  new Color(0.90f, 0.60f, 0.10f)),
            };

            // ── Trainers ───────────────────────────────────────────────
            var trainers = new List<TrainerData>
            {
                MakeTrainer("Weight Rack",     TrainerEffectType.DamageBoost,      0.30f, 0f,   75,  new Color(0.36f, 0.72f, 0.36f)),
                MakeTrainer("Treadmill",       TrainerEffectType.EnergyRegen,      0f,    20f,  50,  new Color(0.20f, 0.70f, 0.80f)),
                MakeTrainer("Protein Bar",     TrainerEffectType.AttackSpeedBoost, 0.25f, 0f,   60,  new Color(0.85f, 0.65f, 0.20f)),
            };

            // ── Waves ──────────────────────────────────────────────────
            EnemyData couchPotato = MakeEnemy("Couch Potato",  60,  10, 0.5f, 0.8f, 25, new Color(0.85f, 0.33f, 0.31f));
            EnemyData fastFoodFan = MakeEnemy("Fast Food Fan", 80,  15, 0.8f, 1.5f, 40, new Color(0.90f, 0.55f, 0.10f));
            EnemyData skeptic     = MakeEnemy("Fitness Skept", 40,  20, 1.2f, 2.5f, 50, new Color(0.65f, 0.20f, 0.80f));

            WaveData wave1 = CreateWaveData("Wave 1 — The Lazy",   5f,
                new WaveData.EnemySpawn { enemyData = couchPotato, count = 5, spawnInterval = 3f, delayBeforeGroup = 0f });

            WaveData wave2 = CreateWaveData("Wave 2 — Junk Food",  8f,
                new WaveData.EnemySpawn { enemyData = couchPotato, count = 4, spawnInterval = 2.5f, delayBeforeGroup = 0f },
                new WaveData.EnemySpawn { enemyData = fastFoodFan, count = 3, spawnInterval = 3f,   delayBeforeGroup = 5f });

            WaveData wave3 = CreateWaveData("Wave 3 — The Final!", 10f,
                new WaveData.EnemySpawn { enemyData = fastFoodFan, count = 5, spawnInterval = 2f,   delayBeforeGroup = 0f },
                new WaveData.EnemySpawn { enemyData = skeptic,     count = 4, spawnInterval = 2.5f, delayBeforeGroup = 6f },
                new WaveData.EnemySpawn { enemyData = couchPotato, count = 3, spawnInterval = 1.5f, delayBeforeGroup = 10f });

            return (chars, trainers, new List<WaveData> { wave1, wave2, wave3 });
        }

        // ─── Data factories ────────────────────────────────────────────────────
        private CharacterData MakeCharacter(string n, int hp, int dmg, float spd,
            float range, int cost, Color col)
        {
            CharacterData d = ScriptableObject.CreateInstance<CharacterData>();
            d.characterName = n; d.maxHealth = hp; d.attackDamage = dmg;
            d.attackSpeed   = spd; d.attackRange = range;
            d.energyCost    = cost; d.bodyColor = col;
            return d;
        }

        private TrainerData MakeTrainer(string n, TrainerEffectType eff,
            float val, float regen, int cost, Color col)
        {
            TrainerData d = ScriptableObject.CreateInstance<TrainerData>();
            d.trainerName = n; d.effectType = eff; d.effectValue = val;
            d.energyRegenPerSecond = regen; d.energyCost = cost; d.bodyColor = col;
            return d;
        }

        private EnemyData MakeEnemy(string n, int hp, int dmg, float aspd,
            float mspd, int reward, Color col)
        {
            EnemyData d = ScriptableObject.CreateInstance<EnemyData>();
            d.enemyName = n; d.maxHealth = hp; d.attackDamage = dmg;
            d.attackSpeed = aspd; d.moveSpeed = mspd;
            d.energyReward = reward; d.bodyColor = col;
            return d;
        }

        private WaveData CreateWaveData(string name, float delay,
            params WaveData.EnemySpawn[] groups)
        {
            WaveData wd = ScriptableObject.CreateInstance<WaveData>();
            wd.waveName = name; wd.delayBeforeWave = delay;
            foreach (var g in groups) wd.enemyGroups.Add(g);
            return wd;
        }

        // ─── Visual helpers ────────────────────────────────────────────────────
        private T CreateManager<T>(string name) where T : Component
        {
            GameObject go = new GameObject(name);
            return go.AddComponent<T>();
        }

        private void CreateColoredQuad(string name, Vector3 worldPos,
            Vector3 scale, Color color, int sortOrder = 0)
        {
            GameObject go = new GameObject(name);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = CreateColoredSprite(color);
            sr.sortingOrder = sortOrder;
            go.transform.position   = worldPos;
            go.transform.localScale = scale;
        }

        private void CreateWorldLabel(string text, Vector3 worldPos,
            Color color, float fontSize = 0.35f)
        {
            GameObject canvasGO = new GameObject("Label_" + text);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGO.transform.position   = worldPos;
            canvasGO.transform.localScale = Vector3.one * fontSize;

            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(canvasGO.transform, false);
            var t = textGO.AddComponent<UnityEngine.UI.Text>();
            t.text      = text;
            t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize  = 12;
            t.color     = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 30f);
        }

        private static Sprite CreateColoredSprite(Color color)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }

}
