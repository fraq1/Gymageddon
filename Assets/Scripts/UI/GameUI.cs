using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Gymageddon.Core;
using Gymageddon.Data;
using Gymageddon.Managers;

namespace Gymageddon.UI
{
    /// <summary>
    /// Builds and drives all in-game UI elements at runtime:
    ///   - Energy counter (top-left)
    ///   - Wave indicator (top-center)
    ///   - Preparation panel (card selection + timer + Start Wave button)
    ///   - Selection bar (bottom) — visible during Playing state for mid-wave purchases
    ///   - Game-over / victory overlay
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        // Injected by GameBootstrap
        private List<CharacterData> _characterOptions = new List<CharacterData>();
        private List<TrainerData>   _trainerOptions   = new List<TrainerData>();

        // HUD elements
        private Text _energyText;
        private Text _waveText;
        private Text _overlayText;
        private GameObject _overlayPanel;

        // Selection bar (mid-wave purchases)
        private GameObject _selectionBar;

        // Preparation phase UI
        private GameObject _preparationPanel;
        private Text        _prepTimerText;
        private Text        _prepWaveText;
        private Transform   _cardsContainer;
        private float       _prepTimeRemaining;
        private bool        _inPreparation;

        // Canvas root (needed by CardDragHandler for ghost parenting)
        private Canvas _canvas;

        // ── Injection ─────────────────────────────────────────────────
        public void SetUnitOptions(List<CharacterData> chars, List<TrainerData> trainers)
        {
            _characterOptions = chars;
            _trainerOptions   = trainers;
        }

        // ── Lifecycle ─────────────────────────────────────────────────
        private void Start()
        {
            BuildUI();
            SubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void Update()
        {
            if (!_inPreparation) return;

            _prepTimeRemaining -= Time.deltaTime;
            if (_prepTimerText)
                _prepTimerText.text = $"⏱ {Mathf.CeilToInt(Mathf.Max(0f, _prepTimeRemaining))}s";

            if (_prepTimeRemaining <= 0f)
            {
                EndPreparation();
                // EndPreparation sets _inPreparation = false, so no further
                // processing occurs on subsequent frames.
            }
        }

        // ── Event subscriptions ───────────────────────────────────────
        private void SubscribeEvents()
        {
            GameEvents.OnEnergyChanged          += UpdateEnergy;
            GameEvents.OnWaveStarted            += UpdateWave;
            GameEvents.OnGameStateChanged       += HandleGameState;
            GameEvents.OnPreparationPhaseStarted += ShowPreparationPanel;
        }

        private void UnsubscribeEvents()
        {
            GameEvents.OnEnergyChanged          -= UpdateEnergy;
            GameEvents.OnWaveStarted            -= UpdateWave;
            GameEvents.OnGameStateChanged       -= HandleGameState;
            GameEvents.OnPreparationPhaseStarted -= ShowPreparationPanel;
        }

        // ── Event handlers ────────────────────────────────────────────
        private void UpdateEnergy(int amount)
        {
            if (_energyText) _energyText.text = $"⚡ {amount}";
        }

        private void UpdateWave(int wave, int total)
        {
            if (_waveText) _waveText.text = $"Wave {wave}/{total}";
        }

        private void HandleGameState(GameState state)
        {
            switch (state)
            {
                case GameState.Playing:
                    // Hide preparation, show selection bar for mid-wave purchases
                    if (_preparationPanel) _preparationPanel.SetActive(false);
                    if (_selectionBar)     _selectionBar.SetActive(true);
                    break;

                case GameState.Preparing:
                    // Selection bar hidden during preparation (cards used instead)
                    if (_selectionBar) _selectionBar.SetActive(false);
                    break;

                case GameState.Victory:
                    ShowOverlay("🏆 VICTORY!\nAll waves defeated!", new Color(0.1f, 0.7f, 0.1f, 0.85f));
                    break;

                case GameState.Defeat:
                    ShowOverlay("💀 DEFEAT!\nEnemies reached your base!", new Color(0.7f, 0.1f, 0.1f, 0.85f));
                    break;
            }
        }

        // ── Preparation panel ─────────────────────────────────────────
        private void ShowPreparationPanel(int waveNumber, int totalWaves,
            List<UnitCard> cards, float timeLimit)
        {
            _inPreparation     = true;
            _prepTimeRemaining = timeLimit;

            if (_prepWaveText)
                _prepWaveText.text = $"Wave {waveNumber}/{totalWaves} — Place Your Units!";
            if (_prepTimerText)
                _prepTimerText.text = $"⏱ {Mathf.CeilToInt(timeLimit)}s";

            // Rebuild card buttons
            if (_cardsContainer != null)
            {
                foreach (Transform child in _cardsContainer)
                    Destroy(child.gameObject);

                float totalW  = cards.Count * 110f - 10f; // 10px gap
                float startX  = -totalW * 0.5f + 50f;
                for (int i = 0; i < cards.Count; i++)
                    CreateDraggableCard(_cardsContainer, cards[i], startX + i * 110f);
            }

            if (_preparationPanel) _preparationPanel.SetActive(true);
        }

        private void EndPreparation()
        {
            if (!_inPreparation) return;
            _inPreparation = false;
            if (_preparationPanel) _preparationPanel.SetActive(false);
            GameManager.Instance?.EndPreparationPhase();
        }

        private void ShowOverlay(string message, Color bgColor)
        {
            if (_overlayPanel == null) return;
            _overlayPanel.SetActive(true);
            _overlayPanel.GetComponent<Image>().color = bgColor;
            if (_overlayText) _overlayText.text = message;
        }

        // ── UI Construction ───────────────────────────────────────────
        private void BuildUI()
        {
            // ── Root Canvas ────────────────────────────────────────────
            _canvas = GetComponent<Canvas>();
            if (_canvas == null)
            {
                _canvas = gameObject.AddComponent<Canvas>();
                _canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 10;
                gameObject.AddComponent<CanvasScaler>();
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // ── Top HUD ────────────────────────────────────────────────
            GameObject hud = CreatePanel("HUD", _canvas.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -40f), new Vector2(0f, 0f),
                new Color(0f, 0f, 0f, 0.6f));

            _energyText = CreateText("EnergyText", hud.transform, "⚡ 150",
                TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(10f, 0f), new Vector2(200f, 40f), 22);

            _waveText = CreateText("WaveText", hud.transform, "Wave 1/3",
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f), new Vector2(200f, 40f), 22);

            // ── Bottom Selection Bar (mid-wave purchases) ──────────────
            _selectionBar = CreatePanel("SelectionBar", _canvas.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 90f),
                new Color(0.1f, 0.1f, 0.1f, 0.85f));

            CreateText("Label_Chars", _selectionBar.transform, "FIGHTERS",
                TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(8f, 0f), new Vector2(90f, 40f), 14, Color.yellow);

            float btnX = 105f;
            foreach (CharacterData cd in _characterOptions)
            {
                CharacterData captured = cd;
                CreateUnitButton(_selectionBar.transform, cd.characterName, cd.bodyColor,
                    $"{cd.energyCost}⚡", btnX,
                    () => PlacementManager.Instance?.SelectCharacterToPlace(captured));
                btnX += 90f;
            }

            CreateText("Label_Trainers", _selectionBar.transform, "TRAINERS",
                TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(btnX + 5f, 0f), new Vector2(90f, 40f), 14, Color.cyan);

            btnX += 100f;
            foreach (TrainerData td in _trainerOptions)
            {
                TrainerData captured = td;
                CreateUnitButton(_selectionBar.transform, td.trainerName, td.bodyColor,
                    $"{td.energyCost}⚡", btnX,
                    () => PlacementManager.Instance?.SelectTrainerToPlace(captured));
                btnX += 90f;
            }

            // Selection bar starts hidden; shown when wave Playing begins
            _selectionBar.SetActive(false);

            // ── Preparation Panel ──────────────────────────────────────
            BuildPreparationPanel(_canvas.transform);

            // ── Game-Over Overlay ──────────────────────────────────────
            _overlayPanel = CreatePanel("Overlay", _canvas.transform,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                new Color(0.1f, 0.1f, 0.1f, 0.85f));

            _overlayText = CreateText("OverlayText", _overlayPanel.transform,
                "", TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(600f, 200f), 36, Color.white);

            _overlayPanel.SetActive(false);
        }

        private void BuildPreparationPanel(Transform canvasTransform)
        {
            // Semi-transparent bottom panel (taller than selection bar)
            _preparationPanel = CreatePanel("PreparationPanel", canvasTransform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 160f),
                new Color(0.05f, 0.05f, 0.15f, 0.92f));

            // ── Top row: wave name | timer | start button ──────────────
            _prepWaveText = CreateText("PrepWaveText", _preparationPanel.transform,
                "Wave 1/3 — Place Your Units!",
                TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(12f, -22f), new Vector2(400f, 36f), 18, Color.white);

            _prepTimerText = CreateText("PrepTimer", _preparationPanel.transform,
                "⏱ 30s",
                TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -22f), new Vector2(120f, 36f), 20,
                new Color(1f, 0.85f, 0.2f));

            // "Start Wave!" button (top-right of panel)
            GameObject startBtn = new GameObject("StartWaveBtn");
            startBtn.transform.SetParent(_preparationPanel.transform, false);
            Image btnImg = startBtn.AddComponent<Image>();
            btnImg.color = new Color(0.15f, 0.6f, 0.15f, 1f);
            Button btn = startBtn.AddComponent<Button>();
            btn.onClick.AddListener(EndPreparation);

            RectTransform btnRT = startBtn.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(1f, 1f);
            btnRT.anchorMax = new Vector2(1f, 1f);
            btnRT.anchoredPosition = new Vector2(-75f, -22f);
            btnRT.sizeDelta = new Vector2(140f, 36f);

            CreateText("StartBtnLabel", startBtn.transform, "▶ Start Wave!",
                TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(135f, 36f), 16, Color.white);

            // ── Divider line ──────────────────────────────────────────
            CreatePanel("Divider", _preparationPanel.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -45f), new Vector2(0f, -43f),
                new Color(0.4f, 0.4f, 0.4f, 0.6f));

            // ── Cards container (centred horizontally) ────────────────
            GameObject container = new GameObject("CardsContainer");
            container.transform.SetParent(_preparationPanel.transform, false);
            RectTransform crt = container.AddComponent<RectTransform>();
            crt.anchorMin        = new Vector2(0.5f, 0f);
            crt.anchorMax        = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(0f, -10f);
            crt.sizeDelta        = new Vector2(400f, 0f);
            _cardsContainer      = container.transform;

            _preparationPanel.SetActive(false);
        }

        /// <summary>Creates a draggable card inside the cards container.</summary>
        private void CreateDraggableCard(Transform parent, UnitCard card, float xPos)
        {
            GameObject go = new GameObject($"Card_{card.Name}");
            go.transform.SetParent(parent, false);

            Image img = go.AddComponent<Image>();
            img.color = card.CardColor * 0.85f;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(xPos, 0f);
            rt.sizeDelta        = new Vector2(100f, 100f);

            // Type badge
            CreateText("Type", go.transform, card.TypeLabel,
                TextAnchor.UpperCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 35f), new Vector2(95f, 24f), 11,
                new Color(1f, 1f, 0.6f));

            // Unit name
            CreateText("Name", go.transform, card.Name,
                TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 5f), new Vector2(95f, 36f), 12, Color.white);

            // Cost
            CreateText("Cost", go.transform, $"{card.Cost}⚡",
                TextAnchor.LowerCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -30f), new Vector2(95f, 28f), 14, Color.yellow);

            // Drag hint
            CreateText("Hint", go.transform, "drag →",
                TextAnchor.LowerCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -44f), new Vector2(95f, 18f), 9,
                new Color(0.8f, 0.8f, 0.8f, 0.7f));

            // Drag handler component
            CardDragHandler drag = go.AddComponent<CardDragHandler>();
            drag.Init(card, _canvas.transform);
        }

        // ── UI Helpers ────────────────────────────────────────────────
        private GameObject CreatePanel(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax,
            Color bgColor)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = bgColor;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return go;
        }

        private Text CreateText(string name, Transform parent, string content,
            TextAnchor align,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos, Vector2 size,
            int fontSize, Color? color = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text t = go.AddComponent<Text>();
            t.text      = content;
            t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize  = fontSize;
            t.alignment = align;
            t.color     = color ?? Color.white;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return t;
        }

        private void CreateUnitButton(Transform parent, string label, Color btnColor,
            string costLabel, float xPos, System.Action onClick)
        {
            GameObject go = new GameObject($"Btn_{label}");
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = btnColor * 0.85f;
            Button btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(xPos, 0f);
            rt.sizeDelta = new Vector2(85f, 70f);

            CreateText("Name", go.transform, label, TextAnchor.UpperCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 10f), new Vector2(80f, 35f), 11, Color.white);

            CreateText("Cost", go.transform, costLabel, TextAnchor.LowerCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -10f), new Vector2(80f, 25f), 13, Color.yellow);
        }
    }
}

