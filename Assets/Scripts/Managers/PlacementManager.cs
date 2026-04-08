using UnityEngine;
using Gymageddon.Core;
using Gymageddon.Data;
using Gymageddon.Entities;

namespace Gymageddon.Managers
{
    /// <summary>
    /// Handles player input for selecting and placing Characters / Trainers.
    ///
    /// Workflow:
    ///   1. Player clicks a unit button in the UI  → SelectCharacterToPlace / SelectTrainerToPlace
    ///   2. Player clicks a lane on the game board → TryPlaceSelected
    ///   3. The manager checks slot availability, then spawns the unit
    /// </summary>
    public class PlacementManager : MonoBehaviour
    {
        private const int BASE_MODEL_SORTING_ORDER = 2;
        private const int DETAIL_MODEL_SORTING_ORDER = 3;

        public static PlacementManager Instance { get; private set; }

        private GameBoard _board;
        private ResourceManager _resources;

        // Currently selected data (only one can be "held" at a time)
        private CharacterData _selectedCharacter;
        private TrainerData   _selectedTrainer;

        // Lane Y positions and lane GameObjects (set by GameBootstrap)
        private Lane[] _lanes;

        // ── Lifecycle ─────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ── Injection ─────────────────────────────────────────────────
        public void Init(GameBoard board, ResourceManager resources, Lane[] lanes)
        {
            _board     = board;
            _resources = resources;
            _lanes     = lanes;
        }

        // ── Selection ─────────────────────────────────────────────────
        public void SelectCharacterToPlace(CharacterData data)
        {
            _selectedCharacter = data;
            _selectedTrainer   = null;
            Debug.Log($"[PlacementManager] Selected character: {data.characterName}");
        }

        public void SelectTrainerToPlace(TrainerData data)
        {
            _selectedTrainer   = data;
            _selectedCharacter = null;
            Debug.Log($"[PlacementManager] Selected trainer: {data.trainerName}");
        }

        public void ClearSelection()
        {
            _selectedCharacter = null;
            _selectedTrainer   = null;
        }

        // ── Input (called every frame by Update) ──────────────────────
        private void Update()
        {
            GameState? state = GameManager.Instance?.CurrentState;
            if (state != GameState.Playing && state != GameState.Preparing) return;
            if (_selectedCharacter == null && _selectedTrainer == null) return;

            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit2D hit = Physics2D.GetRayIntersection(ray);

                if (hit.collider != null)
                {
                    Lane lane = hit.collider.GetComponentInParent<Lane>();
                    if (lane != null)
                        TryPlaceSelected(lane.LaneIndex);
                }
            }
        }

        // ── Placement ─────────────────────────────────────────────────
        public void TryPlaceSelected(int laneIndex)
        {
            if (_selectedCharacter != null)
                TryPlaceCharacter(laneIndex, _selectedCharacter);
            else if (_selectedTrainer != null)
                TryPlaceTrainer(laneIndex, _selectedTrainer);
        }

        /// <summary>
        /// Places a character card on the specified lane. Returns true on success.
        /// Can be called directly by drag-and-drop handlers.
        /// </summary>
        public bool TryPlaceCharacter(int laneIndex, CharacterData data)
        {
            if (!_board.CanPlaceCharacter(laneIndex))
            {
                Debug.Log($"[PlacementManager] Lane {laneIndex} already has a character.");
                return false;
            }

            GameObject go = CreateCharacterModelGameObject(data.bodyColor);
            Character ch  = go.AddComponent<Character>();
            ch.Init(data);

            _board.PlaceCharacter(laneIndex, ch);
            ClearSelection();
            return true;
        }

        /// <summary>
        /// Places a trainer card on the specified lane. Returns true on success.
        /// Can be called directly by drag-and-drop handlers.
        /// </summary>
        public bool TryPlaceTrainer(int laneIndex, TrainerData data)
        {
            if (!_board.CanPlaceTrainer(laneIndex))
            {
                Debug.Log($"[PlacementManager] Lane {laneIndex} already has a trainer.");
                return false;
            }

            GameObject go = CreateTrainerModelGameObject(data.bodyColor);
            Trainer t     = go.AddComponent<Trainer>();
            t.Init(data);

            _board.PlaceTrainer(laneIndex, t);
            ClearSelection();
            return true;
        }

        // ── Helpers ───────────────────────────────────────────────────
        private GameObject CreateCharacterModelGameObject(Color bodyColor)
        {
            GameObject root = new GameObject("CharacterModel");
            Color shadow = Color.Lerp(bodyColor, Color.black, 0.35f);
            Color skin = new Color(0.97f, 0.84f, 0.68f);

            CreateModelPart(root.transform, "Torso", new Vector3(0f, 0.02f, 0f), new Vector3(0.30f, 0.34f, 1f), bodyColor, BASE_MODEL_SORTING_ORDER);
            CreateModelPart(root.transform, "Head", new Vector3(0f, 0.33f, 0f), new Vector3(0.20f, 0.20f, 1f), skin, DETAIL_MODEL_SORTING_ORDER);
            CreateModelPart(root.transform, "LeftArm", new Vector3(-0.23f, 0.06f, 0f), new Vector3(0.11f, 0.28f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
            CreateModelPart(root.transform, "RightArm", new Vector3(0.23f, 0.06f, 0f), new Vector3(0.11f, 0.28f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
            CreateModelPart(root.transform, "LeftLeg", new Vector3(-0.09f, -0.30f, 0f), new Vector3(0.11f, 0.32f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
            CreateModelPart(root.transform, "RightLeg", new Vector3(0.09f, -0.30f, 0f), new Vector3(0.11f, 0.32f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
            CreateModelPart(root.transform, "ChestStripe", new Vector3(0f, 0.09f, -0.01f), new Vector3(0.20f, 0.08f, 1f), Color.white * 0.9f, DETAIL_MODEL_SORTING_ORDER);

            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.62f, 0.92f);
            collider.offset = new Vector2(0f, 0.03f);

            return root;
        }

        private GameObject CreateTrainerModelGameObject(Color accentColor)
        {
            GameObject root = new GameObject("TrainerModel");
            Color frame = new Color(0.25f, 0.28f, 0.33f);
            Color darkFrame = new Color(0.16f, 0.18f, 0.22f);

            CreateModelPart(root.transform, "Platform", new Vector3(0f, -0.28f, 0f), new Vector3(0.72f, 0.14f, 1f), darkFrame, BASE_MODEL_SORTING_ORDER);
            CreateModelPart(root.transform, "LeftPillar", new Vector3(-0.24f, -0.02f, 0f), new Vector3(0.12f, 0.48f, 1f), frame, BASE_MODEL_SORTING_ORDER);
            CreateModelPart(root.transform, "RightPillar", new Vector3(0.24f, -0.02f, 0f), new Vector3(0.12f, 0.48f, 1f), frame, BASE_MODEL_SORTING_ORDER);
            CreateModelPart(root.transform, "TopBar", new Vector3(0f, 0.20f, 0f), new Vector3(0.60f, 0.10f, 1f), frame, BASE_MODEL_SORTING_ORDER);
            CreateModelPart(root.transform, "Bench", new Vector3(0f, -0.06f, -0.01f), new Vector3(0.42f, 0.14f, 1f), accentColor, DETAIL_MODEL_SORTING_ORDER);
            CreateModelPart(root.transform, "WeightLeft", new Vector3(-0.34f, 0.20f, -0.01f), new Vector3(0.10f, 0.18f, 1f), accentColor * 0.8f, DETAIL_MODEL_SORTING_ORDER);
            CreateModelPart(root.transform, "WeightRight", new Vector3(0.34f, 0.20f, -0.01f), new Vector3(0.10f, 0.18f, 1f), accentColor * 0.8f, DETAIL_MODEL_SORTING_ORDER);

            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.82f, 0.78f);
            collider.offset = new Vector2(0f, -0.04f);

            return root;
        }

        private void CreateModelPart(Transform parent, string name, Vector3 localPosition,
            Vector3 localScale, Color color, int sortingOrder)
        {
            GameObject part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            SpriteRenderer sr = part.AddComponent<SpriteRenderer>();
            sr.sprite = CreateColoredSprite(color);
            sr.sortingOrder = sortingOrder;
        }

        private Sprite CreateColoredSprite(Color color)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
