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
        private const float REPOSITION_SELECTION_SCALE = 1.08f;
        private const float HELD_DRAG_START_THRESHOLD = 8f;
        private const float HELD_DRAG_START_THRESHOLD_SQ = HELD_DRAG_START_THRESHOLD * HELD_DRAG_START_THRESHOLD;

        public static PlacementManager Instance { get; private set; }

        private GameBoard _board;
        private ResourceManager _resources;

        // Currently selected data (only one can be "held" at a time)
        private CharacterData _selectedCharacter;
        private TrainerData   _selectedTrainer;
        private Character _selectedPlacedCharacter;
        private Trainer _selectedPlacedTrainer;
        private int _selectedPlacedFromLane = -1;
        private Vector3 _selectedPlacedBaseScale = Vector3.one;
        private bool _heldPlacedUnitDragActive;
        private Vector2 _heldPlacedUnitDragStartMouse;

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
            ClearMovedUnitSelection();
            Debug.Log($"[PlacementManager] Selected character: {data.characterName}");
        }

        public void SelectTrainerToPlace(TrainerData data)
        {
            _selectedTrainer   = data;
            _selectedCharacter = null;
            ClearMovedUnitSelection();
            Debug.Log($"[PlacementManager] Selected trainer: {data.trainerName}");
        }

        public void ClearSelection()
        {
            _selectedCharacter = null;
            _selectedTrainer   = null;
            ClearMovedUnitSelection();
        }

        // ── Input (called every frame by Update) ──────────────────────
        private void Update()
        {
            GameState? state = GameManager.Instance?.CurrentState;
            if (state != GameState.Playing && state != GameState.Preparing) return;

            if (state == GameState.Preparing)
                HandleHeldPlacedUnitDragInput();

            if (!Input.GetMouseButtonDown(0)) return;
            if (!TryGetLaneAndHitAtScreenPosition(Input.mousePosition, out Lane lane, out Collider2D selectedCollider))
                return;

            if (_selectedCharacter != null)
            {
                bool placed = TryPlaceCharacter(lane.LaneIndex, _selectedCharacter);
                if (!placed && state == GameState.Preparing)
                {
                    Character clickedCharacter = selectedCollider.GetComponentInParent<Character>();
                    if (clickedCharacter != null && lane.OccupyingCharacter == clickedCharacter)
                    {
                        ArmPlacedCharacterForMove(lane, clickedCharacter);
                        BeginHeldPlacedUnitDragCandidate();
                    }
                }
                return;
            }

            if (_selectedTrainer != null)
            {
                bool placed = TryPlaceTrainer(lane.LaneIndex, _selectedTrainer);
                if (!placed && state == GameState.Preparing)
                {
                    Trainer clickedTrainer = selectedCollider.GetComponentInParent<Trainer>();
                    if (clickedTrainer != null && lane.OccupyingTrainer == clickedTrainer)
                    {
                        ArmPlacedTrainerForMove(lane, clickedTrainer);
                        BeginHeldPlacedUnitDragCandidate();
                    }
                }
                return;
            }

            if (state == GameState.Preparing)
            {
                if (_selectedPlacedCharacter != null || _selectedPlacedTrainer != null)
                {
                    TryRelocateSelectedToLane(lane.LaneIndex);
                    return;
                }

                Character clickedCharacter = selectedCollider.GetComponentInParent<Character>();
                if (clickedCharacter != null && lane.OccupyingCharacter == clickedCharacter)
                {
                    ArmPlacedCharacterForMove(lane, clickedCharacter);
                    BeginHeldPlacedUnitDragCandidate();
                    return;
                }

                Trainer clickedTrainer = selectedCollider.GetComponentInParent<Trainer>();
                if (clickedTrainer != null && lane.OccupyingTrainer == clickedTrainer)
                {
                    ArmPlacedTrainerForMove(lane, clickedTrainer);
                    BeginHeldPlacedUnitDragCandidate();
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
            Lane lane = _board.GetLane(laneIndex);
            if (lane == null) return false;

            if (!lane.IsCharacterSlotEmpty)
            {
                Character existingCharacter = lane.OccupyingCharacter;
                if (existingCharacter != null &&
                    existingCharacter.TryEvolveWith(data))
                {
                    Debug.Log($"[PlacementManager] {existingCharacter.CharacterName} evolved in lane {laneIndex + 1}.");
                    ClearSelection();
                    return true;
                }

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

        private void ArmPlacedCharacterForMove(Lane lane, Character character)
        {
            ClearSelection();
            _selectedPlacedCharacter = character;
            _selectedPlacedFromLane = lane.LaneIndex;
            _selectedPlacedBaseScale = character.transform.localScale;
            character.transform.localScale = _selectedPlacedBaseScale * REPOSITION_SELECTION_SCALE;
            Debug.Log($"[PlacementManager] Armed fighter from lane {lane.LaneIndex + 1} for reposition.");
        }

        private void ArmPlacedTrainerForMove(Lane lane, Trainer trainer)
        {
            ClearSelection();
            _selectedPlacedTrainer = trainer;
            _selectedPlacedFromLane = lane.LaneIndex;
            _selectedPlacedBaseScale = trainer.transform.localScale;
            trainer.transform.localScale = _selectedPlacedBaseScale * REPOSITION_SELECTION_SCALE;
            Debug.Log($"[PlacementManager] Armed trainer from lane {lane.LaneIndex + 1} for reposition.");
        }

        private void TryRelocateSelectedToLane(int targetLaneIndex)
        {
            if (_selectedPlacedCharacter != null)
            {
                bool success = _board.MoveCharacter(_selectedPlacedFromLane, targetLaneIndex)
                    || _board.SwapCharacters(_selectedPlacedFromLane, targetLaneIndex);
                Debug.Log(success
                    ? $"[PlacementManager] Fighter moved to lane {targetLaneIndex + 1}."
                    : $"[PlacementManager] Cannot move fighter to lane {targetLaneIndex + 1}.");
                ClearMovedUnitSelection();
                return;
            }

            if (_selectedPlacedTrainer != null)
            {
                bool success = _board.MoveTrainer(_selectedPlacedFromLane, targetLaneIndex)
                    || _board.SwapTrainers(_selectedPlacedFromLane, targetLaneIndex);
                Debug.Log(success
                    ? $"[PlacementManager] Trainer moved to lane {targetLaneIndex + 1}."
                    : $"[PlacementManager] Cannot move trainer to lane {targetLaneIndex + 1}.");
                ClearMovedUnitSelection();
            }
        }

        private void ClearMovedUnitSelection()
        {
            if (_selectedPlacedCharacter != null)
                _selectedPlacedCharacter.transform.localScale = _selectedPlacedBaseScale;
            if (_selectedPlacedTrainer != null)
                _selectedPlacedTrainer.transform.localScale = _selectedPlacedBaseScale;

            _selectedPlacedCharacter = null;
            _selectedPlacedTrainer = null;
            _selectedPlacedFromLane = -1;
            _selectedPlacedBaseScale = Vector3.one;
            _heldPlacedUnitDragActive = false;
            _heldPlacedUnitDragStartMouse = Vector2.zero;
        }

        private void BeginHeldPlacedUnitDragCandidate()
        {
            _heldPlacedUnitDragActive = false;
            _heldPlacedUnitDragStartMouse = Input.mousePosition;
        }

        private void HandleHeldPlacedUnitDragInput()
        {
            if (_selectedPlacedCharacter == null && _selectedPlacedTrainer == null)
            {
                _heldPlacedUnitDragActive = false;
                return;
            }

            if (Input.GetMouseButton(0) && !_heldPlacedUnitDragActive)
            {
                Vector2 delta = (Vector2)Input.mousePosition - _heldPlacedUnitDragStartMouse;
                if (delta.sqrMagnitude >= HELD_DRAG_START_THRESHOLD_SQ)
                    _heldPlacedUnitDragActive = true;
            }

            if (!Input.GetMouseButtonUp(0)) return;

            if (!_heldPlacedUnitDragActive) return;

            _heldPlacedUnitDragActive = false;
            if (TryGetLaneAtScreenPosition(Input.mousePosition, out Lane lane))
                TryRelocateSelectedToLane(lane.LaneIndex);
            else
                ClearMovedUnitSelection();
        }

        private bool TryGetLaneAtScreenPosition(Vector2 screenPosition, out Lane lane)
        {
            lane = null;
            if (Camera.main == null) return false;

            Vector3 worldPoint = Camera.main.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, Mathf.Abs(Camera.main.transform.position.z)));
            worldPoint.z = 0f;
            Collider2D[] hits = Physics2D.OverlapPointAll(worldPoint);
            if (hits == null || hits.Length == 0) return false;

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] == null) continue;
                Character hitCharacter = hits[i].GetComponentInParent<Character>();
                if (hitCharacter != null && TryGetLaneForCharacter(hitCharacter, out lane))
                    return true;

                Trainer hitTrainer = hits[i].GetComponentInParent<Trainer>();
                if (hitTrainer != null && TryGetLaneForTrainer(hitTrainer, out lane))
                    return true;

                Lane hitLane = hits[i].GetComponentInParent<Lane>();
                if (hitLane == null) continue;
                lane = hitLane;
                return true;
            }

            return false;
        }

        private bool TryGetLaneAndHitAtScreenPosition(Vector2 screenPosition, out Lane lane, out Collider2D selectedCollider)
        {
            lane = null;
            selectedCollider = null;
            if (Camera.main == null) return false;

            Vector3 worldPoint = Camera.main.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, Mathf.Abs(Camera.main.transform.position.z)));
            worldPoint.z = 0f;
            Collider2D[] hits = Physics2D.OverlapPointAll(worldPoint);
            if (hits == null || hits.Length == 0) return false;

            selectedCollider = hits[0];
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] != null &&
                    (hits[i].GetComponentInParent<Character>() != null ||
                     hits[i].GetComponentInParent<Trainer>() != null))
                {
                    selectedCollider = hits[i];
                    break;
                }
            }

            if (selectedCollider == null) return false;

            Character selectedCharacter = selectedCollider.GetComponentInParent<Character>();
            if (selectedCharacter != null && TryGetLaneForCharacter(selectedCharacter, out lane))
                return true;

            Trainer selectedTrainer = selectedCollider.GetComponentInParent<Trainer>();
            if (selectedTrainer != null && TryGetLaneForTrainer(selectedTrainer, out lane))
                return true;

            lane = selectedCollider.GetComponentInParent<Lane>();
            return lane != null;
        }

        private bool TryGetLaneForCharacter(Character character, out Lane lane)
        {
            lane = null;
            if (character == null || _lanes == null) return false;
            int laneIndex = character.LaneIndex;
            if (laneIndex < 0 || laneIndex >= _lanes.Length) return false;
            lane = _lanes[laneIndex];
            return lane != null;
        }

        private bool TryGetLaneForTrainer(Trainer trainer, out Lane lane)
        {
            lane = null;
            if (trainer == null || _lanes == null) return false;
            int laneIndex = trainer.LaneIndex;
            if (laneIndex < 0 || laneIndex >= _lanes.Length) return false;
            lane = _lanes[laneIndex];
            return lane != null;
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
