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
    ///   3. The manager checks slot availability and energy, then spawns the unit
    /// </summary>
    public class PlacementManager : MonoBehaviour
    {
        public static PlacementManager Instance { get; private set; }

        private GameBoard _board;
        private ResourceManager _resources;

        // Currently selected data (only one can be "held" at a time)
        private CharacterData _selectedCharacter;
        private TrainerData   _selectedTrainer;

        // Lane Y positions and lane GameObjects (set by GameBootstrap)
        private Lane[] _lanes;

        // Sprite template for spawning units
        private Sprite _unitSprite;

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
            _unitSprite = CreateColoredSprite(Color.white); // default; overridden per unit
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
            {
                TryPlaceCharacter(laneIndex, _selectedCharacter);
            }
            else if (_selectedTrainer != null)
            {
                TryPlaceTrainer(laneIndex, _selectedTrainer);
            }
        }

        private void TryPlaceCharacter(int laneIndex, CharacterData data)
        {
            if (!_board.CanPlaceCharacter(laneIndex))
            {
                Debug.Log($"[PlacementManager] Lane {laneIndex} already has a character.");
                return;
            }
            if (!_resources.CanAfford(data.energyCost))
            {
                Debug.Log($"[PlacementManager] Not enough energy ({_resources.CurrentEnergy}/{data.energyCost}).");
                return;
            }

            _resources.SpendEnergy(data.energyCost);

            GameObject go = CreateUnitGameObject(data.bodyColor, new Vector3(0.7f, 0.7f, 1f));
            Character ch  = go.AddComponent<Character>();
            ch.Init(data);

            _board.PlaceCharacter(laneIndex, ch);
            ClearSelection();
        }

        private void TryPlaceTrainer(int laneIndex, TrainerData data)
        {
            if (!_board.CanPlaceTrainer(laneIndex))
            {
                Debug.Log($"[PlacementManager] Lane {laneIndex} already has a trainer.");
                return;
            }
            if (!_resources.CanAfford(data.energyCost))
            {
                Debug.Log($"[PlacementManager] Not enough energy ({_resources.CurrentEnergy}/{data.energyCost}).");
                return;
            }

            _resources.SpendEnergy(data.energyCost);

            GameObject go = CreateUnitGameObject(data.bodyColor, new Vector3(0.6f, 0.6f, 1f));
            Trainer t     = go.AddComponent<Trainer>();
            t.Init(data);

            _board.PlaceTrainer(laneIndex, t);
            ClearSelection();
        }

        // ── Helpers ───────────────────────────────────────────────────
        private GameObject CreateUnitGameObject(Color color, Vector3 scale)
        {
            GameObject go = new GameObject("Unit");
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateColoredSprite(color);
            sr.sortingOrder = 2;
            go.transform.localScale = scale;
            // Add a collider so raycasting works
            go.AddComponent<BoxCollider2D>();
            return go;
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
