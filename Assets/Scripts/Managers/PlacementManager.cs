using UnityEngine;
using UnityEngine.InputSystem;
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
        private const float LANE_PICK_FALLBACK_RADIUS = 0.08f;

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
        private static Camera _cachedFallbackCamera;

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

            Vector2 pointerPosition = GetPointerScreenPosition();
            if (!WasPrimaryPointerPressedThisFrame()) return;
            if (!TryGetLaneAndHitAtScreenPosition(pointerPosition, out Lane lane, out Collider2D selectedCollider))
                return;

            Debug.Log($"[PlacementManager] MouseDown screen={pointerPosition} lane={(lane != null ? lane.LaneIndex + 1 : -1)} collider={DescribeCollider(selectedCollider)} state={state} selectedChar={_selectedCharacter != null} selectedTrainer={_selectedTrainer != null} armedChar={_selectedPlacedCharacter != null} armedTrainer={_selectedPlacedTrainer != null}");

            if (_selectedCharacter != null)
            {
                bool placed = TryPlaceCharacter(lane.LaneIndex, _selectedCharacter);
                if (!placed && state == GameState.Preparing)
                {
                    ResolveUnitUnderPointer(lane, selectedCollider, pointerPosition,
                        out Character clickedCharacter, out Trainer clickedTrainer);

                    if (clickedCharacter != null && lane.OccupyingCharacter == clickedCharacter)
                    {
                        ArmPlacedCharacterForMove(lane, clickedCharacter);
                        BeginHeldPlacedUnitDragCandidate();
                        return;
                    }

                    if (clickedTrainer != null && lane.OccupyingTrainer == clickedTrainer)
                    {
                        ArmPlacedTrainerForMove(lane, clickedTrainer);
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
                    ResolveUnitUnderPointer(lane, selectedCollider, pointerPosition,
                        out Character clickedCharacter, out Trainer clickedTrainer);

                    if (clickedTrainer != null && lane.OccupyingTrainer == clickedTrainer)
                    {
                        ArmPlacedTrainerForMove(lane, clickedTrainer);
                        BeginHeldPlacedUnitDragCandidate();
                        return;
                    }

                    if (clickedCharacter != null && lane.OccupyingCharacter == clickedCharacter)
                    {
                        ArmPlacedCharacterForMove(lane, clickedCharacter);
                        BeginHeldPlacedUnitDragCandidate();
                    }
                }
                return;
            }

            if (state == GameState.Preparing)
            {
                if (_selectedPlacedCharacter != null || _selectedPlacedTrainer != null)
                {
                    ResolveUnitUnderPointer(lane, selectedCollider, pointerPosition,
                        out Character armedClickedCharacter, out Trainer armedClickedTrainer);

                    if (lane.LaneIndex == _selectedPlacedFromLane)
                    {
                        if (_selectedPlacedCharacter != null && armedClickedCharacter == _selectedPlacedCharacter)
                        {
                            BeginHeldPlacedUnitDragCandidate();
                            return;
                        }

                        if (_selectedPlacedTrainer != null && armedClickedTrainer == _selectedPlacedTrainer)
                        {
                            BeginHeldPlacedUnitDragCandidate();
                            return;
                        }

                        if (armedClickedCharacter != null)
                        {
                            ArmPlacedCharacterForMove(lane, armedClickedCharacter);
                            BeginHeldPlacedUnitDragCandidate();
                            return;
                        }

                        if (armedClickedTrainer != null)
                        {
                            ArmPlacedTrainerForMove(lane, armedClickedTrainer);
                            BeginHeldPlacedUnitDragCandidate();
                            return;
                        }

                        return;
                    }

                    TryRelocateSelectedToLane(lane.LaneIndex);
                    return;
                }

                Character clickedCharacter = selectedCollider.GetComponentInParent<Character>();
                if (clickedCharacter == null)
                    clickedCharacter = lane.OccupyingCharacter;

                if (clickedCharacter != null && lane.OccupyingCharacter == clickedCharacter)
                {
                    ArmPlacedCharacterForMove(lane, clickedCharacter);
                    BeginHeldPlacedUnitDragCandidate();
                    return;
                }

                Trainer clickedTrainer = selectedCollider.GetComponentInParent<Trainer>();
                if (clickedTrainer == null)
                    clickedTrainer = lane.OccupyingTrainer;

                if (clickedTrainer != null && lane.OccupyingTrainer == clickedTrainer)
                {
                    ArmPlacedTrainerForMove(lane, clickedTrainer);
                    BeginHeldPlacedUnitDragCandidate();
                }
            }
        }

        private void ResolveUnitUnderPointer(Lane lane, Collider2D selectedCollider, Vector2 pointerPosition,
            out Character clickedCharacter, out Trainer clickedTrainer)
        {
            clickedCharacter = null;
            clickedTrainer = null;

            if (selectedCollider != null)
            {
                clickedCharacter = selectedCollider.GetComponentInParent<Character>();
                clickedTrainer = selectedCollider.GetComponentInParent<Trainer>();
                if (clickedCharacter != null || clickedTrainer != null)
                    return;
            }

            if (lane == null)
                return;

            if (lane.OccupyingCharacter != null && lane.OccupyingTrainer == null)
            {
                clickedCharacter = lane.OccupyingCharacter;
                return;
            }

            if (lane.OccupyingTrainer != null && lane.OccupyingCharacter == null)
            {
                clickedTrainer = lane.OccupyingTrainer;
                return;
            }

            if (!TryGetWorldPointFromScreen(pointerPosition, out Vector3 worldPoint))
                return;

            float trainerX = lane.TrainerSlot != null ? lane.TrainerSlot.position.x : float.MinValue;
            float characterX = lane.CharacterSlot != null ? lane.CharacterSlot.position.x : float.MaxValue;
            float dividerX = (trainerX + characterX) * 0.5f;

            if (worldPoint.x <= dividerX)
            {
                if (lane.OccupyingTrainer != null)
                    clickedTrainer = lane.OccupyingTrainer;
                else if (lane.OccupyingCharacter != null)
                    clickedCharacter = lane.OccupyingCharacter;
            }
            else
            {
                if (lane.OccupyingCharacter != null)
                    clickedCharacter = lane.OccupyingCharacter;
                else if (lane.OccupyingTrainer != null)
                    clickedTrainer = lane.OccupyingTrainer;
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

            GameObject go = CreateCharacterModelGameObject(data);
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

            GameObject go = CreateTrainerModelGameObject(data);
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
            _heldPlacedUnitDragStartMouse = GetPointerScreenPosition();
            Debug.Log($"[PlacementManager] Drag candidate armed at {_heldPlacedUnitDragStartMouse} from lane {_selectedPlacedFromLane + 1}");
        }

        private void HandleHeldPlacedUnitDragInput()
        {
            if (_selectedPlacedCharacter == null && _selectedPlacedTrainer == null)
            {
                _heldPlacedUnitDragActive = false;
                return;
            }

            if (IsPrimaryPointerPressed() && !_heldPlacedUnitDragActive)
            {
                Vector2 delta = GetPointerScreenPosition() - _heldPlacedUnitDragStartMouse;
                if (delta.sqrMagnitude >= HELD_DRAG_START_THRESHOLD_SQ)
                {
                    _heldPlacedUnitDragActive = true;
                    Debug.Log($"[PlacementManager] Drag started after delta={delta} from lane {_selectedPlacedFromLane + 1}");
                }
            }

            if (!WasPrimaryPointerReleasedThisFrame()) return;

            bool wasDragging = _heldPlacedUnitDragActive;
            _heldPlacedUnitDragActive = false;

            Vector2 pointerPosition = GetPointerScreenPosition();
            if (!TryGetLaneAtScreenPosition(pointerPosition, out Lane lane))
            {
                // Released outside any lane — cancel selection.
                Debug.Log($"[PlacementManager] Drag released outside lane at {pointerPosition}, clearing selection.");
                ClearMovedUnitSelection();
                return;
            }

            Debug.Log($"[PlacementManager] Drag released over lane {lane.LaneIndex + 1}, wasDragging={wasDragging}, selectedFrom={_selectedPlacedFromLane + 1}");

            // Move when a real drag occurred OR when released on a different lane
            // (catches short drags to adjacent lanes without requiring the threshold).
            // Releasing on the same lane without dragging keeps the unit armed so the
            // player can still complete the move with a second click on the target.
            if (wasDragging || lane.LaneIndex != _selectedPlacedFromLane)
                TryRelocateSelectedToLane(lane.LaneIndex);
        }

        private bool TryGetLaneAtScreenPosition(Vector2 screenPosition, out Lane lane)
        {
            lane = null;
            if (!TryGetWorldPointFromScreen(screenPosition, out Vector3 worldPoint)) return false;

            Collider2D[] hits = Physics2D.OverlapPointAll(worldPoint);
            if (hits == null || hits.Length == 0)
            {
                hits = Physics2D.OverlapCircleAll(worldPoint, LANE_PICK_FALLBACK_RADIUS);
                if (hits == null || hits.Length == 0) return false;
            }

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
            if (!TryGetWorldPointFromScreen(screenPosition, out Vector3 worldPoint)) return false;

            Collider2D[] hits = Physics2D.OverlapPointAll(worldPoint);
            if (hits == null || hits.Length == 0)
            {
                hits = Physics2D.OverlapCircleAll(worldPoint, LANE_PICK_FALLBACK_RADIUS);
                if (hits == null || hits.Length == 0) return false;
            }

            selectedCollider = hits[0];
            float bestDistance = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null) continue;

                Character hitCharacter = hit.GetComponentInParent<Character>();
                Trainer hitTrainer = hit.GetComponentInParent<Trainer>();
                if (hitCharacter == null && hitTrainer == null)
                    continue;

                Transform ownerTransform = hitCharacter != null ? hitCharacter.transform : hitTrainer.transform;
                float distance = Vector2.Distance(worldPoint, ownerTransform.position);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    selectedCollider = hit;
                }
            }

            Debug.Log($"[PlacementManager] HitTest screen={screenPosition} world={worldPoint} hits={hits.Length} selected={DescribeCollider(selectedCollider)}");

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

        private static string DescribeCollider(Collider2D collider)
        {
            if (collider == null) return "null";

            string owner = collider.GetComponentInParent<Character>() != null
                ? $"Character:{collider.GetComponentInParent<Character>().name}"
                : collider.GetComponentInParent<Trainer>() != null
                    ? $"Trainer:{collider.GetComponentInParent<Trainer>().name}"
                    : collider.GetComponentInParent<Lane>() != null
                        ? $"Lane:{collider.GetComponentInParent<Lane>().LaneIndex + 1}"
                        : "NoOwner";

            return $"{collider.name} parent={(collider.transform.parent != null ? collider.transform.parent.name : "none")} owner={owner}";
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

        private static bool TryGetWorldPointFromScreen(Vector2 screenPosition, out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;
            Camera cam = ResolveCamera();
            if (cam == null) return false;

            worldPoint = cam.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, Mathf.Abs(cam.transform.position.z)));
            worldPoint.z = 0f;
            return true;
        }

        private static Vector2 GetPointerScreenPosition()
        {
            return Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;
        }

        private static bool WasPrimaryPointerPressedThisFrame()
        {
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        }

        private static bool IsPrimaryPointerPressed()
        {
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
        }

        private static bool WasPrimaryPointerReleasedThisFrame()
        {
            return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
        }

        private static Camera ResolveCamera()
        {
            Camera cam = Camera.main;
            if (cam != null) return cam;

            if (_cachedFallbackCamera == null || !_cachedFallbackCamera.isActiveAndEnabled)
                _cachedFallbackCamera = FindAnyObjectByType<Camera>();

            return _cachedFallbackCamera;
        }

        // ── Helpers ───────────────────────────────────────────────────
        private GameObject CreateCharacterModelGameObject(CharacterData data)
        {
            GameObject root = new GameObject("CharacterModel");
            Color bodyColor = data != null ? data.bodyColor : Color.white;
            string typeName = data != null ? data.characterName.ToLowerInvariant() : string.Empty;
            Color shadow = Color.Lerp(bodyColor, Color.black, 0.35f);
            Color skin = new Color(0.97f, 0.84f, 0.68f);

            if (typeName.Contains("runner") || typeName.Contains("cardio") || typeName.Contains("tempo"))
            {
                CreateModelPart(root.transform, "Torso", new Vector3(0f, 0.05f, 0f), new Vector3(0.24f, 0.30f, 1f), bodyColor, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "Head", new Vector3(0f, 0.34f, 0f), new Vector3(0.18f, 0.18f, 1f), skin, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "LeftArm", new Vector3(-0.18f, 0.10f, 0f), new Vector3(0.08f, 0.24f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "RightArm", new Vector3(0.18f, 0.10f, 0f), new Vector3(0.08f, 0.24f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "LeftLeg", new Vector3(-0.07f, -0.30f, 0f), new Vector3(0.08f, 0.34f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "RightLeg", new Vector3(0.07f, -0.30f, 0f), new Vector3(0.08f, 0.34f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "Headband", new Vector3(0f, 0.41f, -0.01f), new Vector3(0.22f, 0.06f, 1f), Color.white * 0.9f, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "SpeedStripeL", new Vector3(-0.30f, 0.02f, -0.01f), new Vector3(0.10f, 0.04f, 1f), Color.white * 0.75f, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "SpeedStripeR", new Vector3(0.30f, -0.06f, -0.01f), new Vector3(0.10f, 0.04f, 1f), Color.white * 0.55f, DETAIL_MODEL_SORTING_ORDER);
            }
            else if (typeName.Contains("weight") || typeName.Contains("lifter") || typeName.Contains("power"))
            {
                CreateModelPart(root.transform, "Torso", new Vector3(0f, 0.00f, 0f), new Vector3(0.36f, 0.40f, 1f), bodyColor, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "Head", new Vector3(0f, 0.38f, 0f), new Vector3(0.22f, 0.22f, 1f), skin, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "LeftArm", new Vector3(-0.28f, 0.06f, 0f), new Vector3(0.12f, 0.30f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "RightArm", new Vector3(0.28f, 0.06f, 0f), new Vector3(0.12f, 0.30f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "LeftLeg", new Vector3(-0.11f, -0.31f, 0f), new Vector3(0.12f, 0.34f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "RightLeg", new Vector3(0.11f, -0.31f, 0f), new Vector3(0.12f, 0.34f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "Barbell", new Vector3(0f, 0.42f, -0.01f), new Vector3(0.62f, 0.08f, 1f), Color.white * 0.8f, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "PlateL", new Vector3(-0.32f, 0.42f, -0.01f), new Vector3(0.12f, 0.18f, 1f), bodyColor * 0.7f, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "PlateR", new Vector3(0.32f, 0.42f, -0.01f), new Vector3(0.12f, 0.18f, 1f), bodyColor * 0.7f, DETAIL_MODEL_SORTING_ORDER);
            }
            else if (typeName.Contains("shadow") || typeName.Contains("ghost") || typeName.Contains("ninja"))
            {
                CreateModelPart(root.transform, "Torso", new Vector3(0f, 0.03f, 0f), new Vector3(0.28f, 0.32f, 1f), bodyColor, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "Head", new Vector3(0f, 0.35f, 0f), new Vector3(0.18f, 0.18f, 1f), skin, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "LeftArm", new Vector3(-0.20f, 0.06f, 0f), new Vector3(0.10f, 0.26f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "RightArm", new Vector3(0.20f, 0.06f, 0f), new Vector3(0.10f, 0.26f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "LeftLeg", new Vector3(-0.08f, -0.30f, 0f), new Vector3(0.10f, 0.32f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "RightLeg", new Vector3(0.08f, -0.30f, 0f), new Vector3(0.10f, 0.32f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "Mask", new Vector3(0f, 0.34f, -0.01f), new Vector3(0.22f, 0.08f, 1f), Color.black * 0.85f, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "Cape", new Vector3(0f, -0.02f, -0.02f), new Vector3(0.42f, 0.26f, 1f), bodyColor * 0.6f, DETAIL_MODEL_SORTING_ORDER);
            }
            else if (typeName.Contains("flex") || typeName.Contains("yoga") || typeName.Contains("stretch"))
            {
                CreateModelPart(root.transform, "Torso", new Vector3(0f, 0.02f, 0f), new Vector3(0.28f, 0.30f, 1f), bodyColor, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "Head", new Vector3(0f, 0.35f, 0f), new Vector3(0.20f, 0.20f, 1f), skin, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "LeftArm", new Vector3(-0.25f, 0.16f, 0f), new Vector3(0.08f, 0.22f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "RightArm", new Vector3(0.25f, 0.16f, 0f), new Vector3(0.08f, 0.22f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "LeftLeg", new Vector3(-0.10f, -0.28f, 0f), new Vector3(0.10f, 0.30f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "RightLeg", new Vector3(0.10f, -0.28f, 0f), new Vector3(0.10f, 0.30f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "Mat", new Vector3(0f, -0.40f, -0.01f), new Vector3(0.56f, 0.08f, 1f), Color.white * 0.8f, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "Aura", new Vector3(0f, 0.10f, -0.02f), new Vector3(0.42f, 0.42f, 1f), bodyColor * 0.45f, DETAIL_MODEL_SORTING_ORDER);
            }
            else
            {
                CreateModelPart(root.transform, "Torso", new Vector3(0f, 0.02f, 0f), new Vector3(0.30f, 0.34f, 1f), bodyColor, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "Head", new Vector3(0f, 0.33f, 0f), new Vector3(0.20f, 0.20f, 1f), skin, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "LeftArm", new Vector3(-0.23f, 0.06f, 0f), new Vector3(0.11f, 0.28f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "RightArm", new Vector3(0.23f, 0.06f, 0f), new Vector3(0.11f, 0.28f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "LeftLeg", new Vector3(-0.09f, -0.30f, 0f), new Vector3(0.11f, 0.32f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "RightLeg", new Vector3(0.09f, -0.30f, 0f), new Vector3(0.11f, 0.32f, 1f), shadow, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "ChestStripe", new Vector3(0f, 0.09f, -0.01f), new Vector3(0.20f, 0.08f, 1f), Color.white * 0.9f, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "GloveL", new Vector3(-0.28f, -0.02f, -0.01f), new Vector3(0.12f, 0.12f, 1f), bodyColor * 0.55f, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "GloveR", new Vector3(0.28f, -0.02f, -0.01f), new Vector3(0.12f, 0.12f, 1f), bodyColor * 0.55f, DETAIL_MODEL_SORTING_ORDER);
            }

            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.offset = Vector2.zero;

            return root;
        }

        private GameObject CreateTrainerModelGameObject(TrainerData data)
        {
            GameObject root = new GameObject("TrainerModel");
            Color accentColor = data != null ? data.bodyColor : Color.white;
            string typeName = data != null ? data.trainerName.ToLowerInvariant() : string.Empty;
            Color frame = new Color(0.25f, 0.28f, 0.33f);
            Color darkFrame = new Color(0.16f, 0.18f, 0.22f);

            if (typeName.Contains("sprint") || typeName.Contains("track"))
            {
                CreateModelPart(root.transform, "Base", new Vector3(0f, -0.24f, 0f), new Vector3(0.74f, 0.12f, 1f), darkFrame, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "Belt", new Vector3(0f, -0.02f, -0.01f), new Vector3(0.54f, 0.16f, 1f), accentColor, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "HandleL", new Vector3(-0.30f, 0.18f, 0f), new Vector3(0.08f, 0.38f, 1f), frame, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "HandleR", new Vector3(0.30f, 0.18f, 0f), new Vector3(0.08f, 0.38f, 1f), frame, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "Console", new Vector3(0f, 0.28f, -0.01f), new Vector3(0.24f, 0.12f, 1f), Color.white * 0.9f, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "MotionL", new Vector3(-0.40f, 0.02f, -0.01f), new Vector3(0.12f, 0.04f, 1f), Color.white * 0.6f, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "MotionR", new Vector3(0.40f, -0.06f, -0.01f), new Vector3(0.12f, 0.04f, 1f), Color.white * 0.35f, DETAIL_MODEL_SORTING_ORDER);
            }
            else if (typeName.Contains("recovery") || typeName.Contains("mat"))
            {
                CreateModelPart(root.transform, "Mat", new Vector3(0f, -0.12f, 0f), new Vector3(0.70f, 0.18f, 1f), accentColor, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "Bolster", new Vector3(0f, 0.02f, -0.01f), new Vector3(0.36f, 0.08f, 1f), frame, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "Bottle", new Vector3(0.26f, 0.16f, -0.01f), new Vector3(0.12f, 0.22f, 1f), accentColor * 0.75f, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "Cap", new Vector3(0.26f, 0.29f, -0.01f), new Vector3(0.16f, 0.05f, 1f), Color.white * 0.9f, DETAIL_MODEL_SORTING_ORDER);
            }
            else if (typeName.Contains("hydration") || typeName.Contains("cooler"))
            {
                CreateModelPart(root.transform, "Body", new Vector3(0f, -0.02f, 0f), new Vector3(0.48f, 0.44f, 1f), accentColor, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "Door", new Vector3(0f, -0.03f, -0.01f), new Vector3(0.28f, 0.24f, 1f), Color.white * 0.85f, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "Handle", new Vector3(0.12f, 0.11f, -0.01f), new Vector3(0.05f, 0.12f, 1f), frame, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "Bottle", new Vector3(-0.25f, 0.18f, -0.01f), new Vector3(0.12f, 0.22f, 1f), Color.white, DETAIL_MODEL_SORTING_ORDER);
            }
            else if (typeName.Contains("tempo") || typeName.Contains("bands"))
            {
                CreateModelPart(root.transform, "AnchorL", new Vector3(-0.22f, -0.10f, 0f), new Vector3(0.10f, 0.34f, 1f), frame, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "AnchorR", new Vector3(0.22f, -0.10f, 0f), new Vector3(0.10f, 0.34f, 1f), frame, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "BandTop", new Vector3(0f, 0.14f, -0.01f), new Vector3(0.48f, 0.06f, 1f), accentColor, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "BandBottom", new Vector3(0f, -0.04f, -0.01f), new Vector3(0.48f, 0.06f, 1f), accentColor * 0.85f, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "Center", new Vector3(0f, 0.02f, -0.02f), new Vector3(0.18f, 0.18f, 1f), Color.white * 0.85f, DETAIL_MODEL_SORTING_ORDER);
            }
            else
            {
                CreateModelPart(root.transform, "Platform", new Vector3(0f, -0.28f, 0f), new Vector3(0.72f, 0.14f, 1f), darkFrame, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "LeftPillar", new Vector3(-0.24f, -0.02f, 0f), new Vector3(0.12f, 0.48f, 1f), frame, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "RightPillar", new Vector3(0.24f, -0.02f, 0f), new Vector3(0.12f, 0.48f, 1f), frame, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "TopBar", new Vector3(0f, 0.20f, 0f), new Vector3(0.60f, 0.10f, 1f), frame, BASE_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "Bench", new Vector3(0f, -0.06f, -0.01f), new Vector3(0.42f, 0.14f, 1f), accentColor, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "WeightLeft", new Vector3(-0.34f, 0.20f, -0.01f), new Vector3(0.10f, 0.18f, 1f), accentColor * 0.8f, DETAIL_MODEL_SORTING_ORDER);
                CreateModelPart(root.transform, "WeightRight", new Vector3(0.34f, 0.20f, -0.01f), new Vector3(0.10f, 0.18f, 1f), accentColor * 0.8f, DETAIL_MODEL_SORTING_ORDER);
            }

            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.offset = Vector2.zero;

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
