using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Gymageddon.Core;
using Gymageddon.Data;
using Gymageddon.Managers;

namespace Gymageddon.UI
{
    /// <summary>
    /// Attach to each card UI element in the preparation panel.
    /// Handles drag-and-drop: the card follows the cursor as a ghost image,
    /// and on release the unit is placed on whichever lane is under the cursor.
    /// </summary>
    public class CardDragHandler : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private UnitCard    _card;
        private Transform   _canvasRoot;
        private CanvasGroup _canvasGroup;
        private GameObject  _ghost;
        private bool        _placed;
        private Camera      _camera;

        /// <summary>Must be called right after the GO is created.</summary>
        public void Init(UnitCard card, Transform canvasRoot)
        {
            _card       = card;
            _canvasRoot = canvasRoot;
        }

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _camera = Camera.main;
        }

        // ── Drag events ───────────────────────────────────────────────
        public void OnBeginDrag(PointerEventData eventData)
        {
            _placed = false;

            // Dim the original card while dragging
            _canvasGroup.alpha          = 0.4f;
            _canvasGroup.blocksRaycasts = false; // let raycasts pass through

            // Build a ghost card that follows the cursor
            _ghost = new GameObject("CardGhost");
            _ghost.transform.SetParent(_canvasRoot, false);

            Image ghostImg = _ghost.AddComponent<Image>();
            Image myImg    = GetComponent<Image>();
            if (myImg != null) ghostImg.color = myImg.color;

            RectTransform ghostRT = _ghost.GetComponent<RectTransform>();
            ghostRT.sizeDelta = GetComponent<RectTransform>().sizeDelta;
            ghostRT.anchorMin = Vector2.zero;
            ghostRT.anchorMax = Vector2.zero;
            ghostRT.pivot     = new Vector2(0.5f, 0.5f);
            ghostRT.position  = eventData.position;

            // Bring ghost above other UI
            Canvas ghostCanvas = _ghost.AddComponent<Canvas>();
            ghostCanvas.overrideSorting = true;
            ghostCanvas.sortingOrder    = 100;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_ghost != null)
                _ghost.transform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // Restore original card appearance
            _canvasGroup.alpha          = 1f;
            _canvasGroup.blocksRaycasts = true;

            if (_ghost != null)
            {
                Destroy(_ghost);
                _ghost = null;
            }

            if (_placed) return;

            if (_camera == null) return; // no main camera — cannot convert to world space

            // Convert screen drop position to world space (camera at z = -10)
            Vector3 screenPt = new Vector3(eventData.position.x, eventData.position.y,
                Mathf.Abs(_camera.transform.position.z));
            Vector3 worldPt  = _camera.ScreenToWorldPoint(screenPt);
            worldPt.z = 0f;

            // Find a lane collider under the drop point
            Collider2D hit = Physics2D.OverlapPoint(worldPt);
            if (hit != null)
            {
                Lane lane = hit.GetComponentInParent<Lane>();
                if (lane != null && TryPlaceOnLane(lane))
                {
                    _placed = true;
                    gameObject.SetActive(false); // consume card
                }
            }
        }

        // ── Helpers ───────────────────────────────────────────────────
        private bool TryPlaceOnLane(Lane lane)
        {
            PlacementManager pm = PlacementManager.Instance;
            if (pm == null) return false;

            return _card.IsCharacter
                ? pm.TryPlaceCharacter(lane.LaneIndex, _card.CharacterData)
                : pm.TryPlaceTrainer  (lane.LaneIndex, _card.TrainerData);
        }
    }
}
