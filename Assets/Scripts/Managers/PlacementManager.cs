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

            GameObject go = CreateUnitGameObject(
                data.bodyColor,
                new Vector3(0.7f, 0.7f, 1f),
                "F",
                new Color(0.95f, 0.95f, 1f),
                new Color(0.10f, 0.18f, 0.30f, 0.9f));
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

            GameObject go = CreateUnitGameObject(
                data.bodyColor,
                new Vector3(0.6f, 0.6f, 1f),
                "T",
                new Color(0.9f, 1f, 0.9f),
                new Color(0.08f, 0.25f, 0.12f, 0.9f));
            Trainer t     = go.AddComponent<Trainer>();
            t.Init(data);

            _board.PlaceTrainer(laneIndex, t);
            ClearSelection();
            return true;
        }

        // ── Helpers ───────────────────────────────────────────────────
        private GameObject CreateUnitGameObject(Color color, Vector3 scale, string badge,
            Color badgeColor, Color outlineColor)
        {
            GameObject go = new GameObject("Unit");
            CreateOutline(go.transform, outlineColor, scale * 1.18f, 1);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateColoredSprite(color);
            sr.sortingOrder = 2;
            go.transform.localScale = scale;
            CreateBadge(go.transform, badge, badgeColor, 3);
            // Add a collider so raycasting works
            go.AddComponent<BoxCollider2D>();
            return go;
        }

        private void CreateOutline(Transform parent, Color color, Vector3 scale, int sortingOrder)
        {
            GameObject outline = new GameObject("Outline");
            outline.transform.SetParent(parent, false);
            outline.transform.localPosition = Vector3.zero;
            outline.transform.localScale = scale;

            SpriteRenderer sr = outline.AddComponent<SpriteRenderer>();
            sr.sprite = CreateColoredSprite(color);
            sr.sortingOrder = sortingOrder;
        }

        private void CreateBadge(Transform parent, string text, Color color, int sortingOrder)
        {
            GameObject badge = new GameObject($"Badge_{text}");
            badge.transform.SetParent(parent, false);
            badge.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            badge.transform.localScale = Vector3.one * 0.18f;

            TextMesh tm = badge.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.fontSize = 96;
            tm.characterSize = 0.1f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;

            MeshRenderer mr = badge.GetComponent<MeshRenderer>();
            if (mr != null)
                mr.sortingOrder = sortingOrder;
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
