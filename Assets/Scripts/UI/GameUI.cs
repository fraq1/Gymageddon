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
    ///   - Selection bar (bottom) with character and trainer buttons
    ///   - Game-over / victory overlay
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        // Injected by GameBootstrap
        private List<CharacterData> _characterOptions = new List<CharacterData>();
        private List<TrainerData>   _trainerOptions   = new List<TrainerData>();

        // UI elements
        private Text _energyText;
        private Text _waveText;
        private Text _overlayText;
        private GameObject _overlayPanel;

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

        // ── Event subscriptions ───────────────────────────────────────
        private void SubscribeEvents()
        {
            GameEvents.OnEnergyChanged     += UpdateEnergy;
            GameEvents.OnWaveStarted       += UpdateWave;
            GameEvents.OnGameStateChanged  += HandleGameState;
        }

        private void UnsubscribeEvents()
        {
            GameEvents.OnEnergyChanged     -= UpdateEnergy;
            GameEvents.OnWaveStarted       -= UpdateWave;
            GameEvents.OnGameStateChanged  -= HandleGameState;
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
                case GameState.Victory:
                    ShowOverlay("🏆 VICTORY!\nAll waves defeated!", new Color(0.1f, 0.7f, 0.1f, 0.85f));
                    break;
                case GameState.Defeat:
                    ShowOverlay("💀 DEFEAT!\nEnemies reached your base!", new Color(0.7f, 0.1f, 0.1f, 0.85f));
                    break;
            }
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
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
                gameObject.AddComponent<CanvasScaler>();
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // ── Top HUD ────────────────────────────────────────────────
            GameObject hud = CreatePanel("HUD", canvas.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -40f), new Vector2(0f, 0f),
                new Color(0f, 0f, 0f, 0.6f));

            _energyText = CreateText("EnergyText", hud.transform, "⚡ 150",
                TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(10f, 0f), new Vector2(200f, 40f), 22);

            _waveText = CreateText("WaveText", hud.transform, "Wave 1/3",
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f), new Vector2(200f, 40f), 22);

            // ── Bottom Selection Bar ───────────────────────────────────
            GameObject bar = CreatePanel("SelectionBar", canvas.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 90f),
                new Color(0.1f, 0.1f, 0.1f, 0.85f));

            CreateText("Label_Chars", bar.transform, "FIGHTERS",
                TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(8f, 0f), new Vector2(90f, 40f), 14, Color.yellow);

            float btnX = 105f;
            foreach (CharacterData cd in _characterOptions)
            {
                CharacterData captured = cd;
                CreateUnitButton(bar.transform, cd.characterName, cd.bodyColor,
                    $"{cd.energyCost}⚡", btnX,
                    () => PlacementManager.Instance?.SelectCharacterToPlace(captured));
                btnX += 90f;
            }

            CreateText("Label_Trainers", bar.transform, "TRAINERS",
                TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(btnX + 5f, 0f), new Vector2(90f, 40f), 14, Color.cyan);

            btnX += 100f;
            foreach (TrainerData td in _trainerOptions)
            {
                TrainerData captured = td;
                CreateUnitButton(bar.transform, td.trainerName, td.bodyColor,
                    $"{td.energyCost}⚡", btnX,
                    () => PlacementManager.Instance?.SelectTrainerToPlace(captured));
                btnX += 90f;
            }

            // ── Game-Over Overlay ──────────────────────────────────────
            _overlayPanel = CreatePanel("Overlay", canvas.transform,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                new Color(0.1f, 0.1f, 0.1f, 0.85f));

            _overlayText = CreateText("OverlayText", _overlayPanel.transform,
                "", TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(600f, 200f), 36, Color.white);

            _overlayPanel.SetActive(false);
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

            // Button label (name)
            CreateText("Name", go.transform, label, TextAnchor.UpperCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 10f), new Vector2(80f, 35f), 11, Color.white);

            // Cost label
            CreateText("Cost", go.transform, costLabel, TextAnchor.LowerCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -10f), new Vector2(80f, 25f), 13, Color.yellow);
        }
    }
}
