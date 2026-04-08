using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Gymageddon.Entities
{
    /// <summary>
    /// Base class for all units (characters, trainers, enemies).
    /// Provides health, death handling and a procedural health bar.
    /// </summary>
    public abstract class Unit : MonoBehaviour
    {
        private const float DAMAGE_POPUP_DURATION = 0.55f;
        private const float DAMAGE_POPUP_RISE_SPEED = 0.7f;
        private const float DAMAGE_POPUP_VERTICAL_OFFSET = 0.42f;
        private const int DAMAGE_POPUP_FONT_SIZE = 42;
        private const float DAMAGE_POPUP_CHARACTER_SIZE = 0.04f;
        private static readonly Color DAMAGE_POPUP_ENEMY_COLOR = Color.white;
        private static readonly Color DAMAGE_POPUP_ALLY_COLOR = new Color(1f, 0.2f, 0.2f, 1f);
        private static Font _damagePopupFont;

        public int MaxHealth    { get; protected set; }
        public int CurrentHealth{ get; protected set; }
        public bool IsDead      { get; private set; }

        // Runtime health bar (world-space Canvas)
        private Image _healthBarFill;

        // ── Setup ─────────────────────────────────────────────────────
        protected virtual void InitHealth(int max)
        {
            MaxHealth     = max;
            CurrentHealth = max;
            IsDead        = false;
            CreateHealthBar();
        }

        // ── Damage / Healing ──────────────────────────────────────────
        public virtual void TakeDamage(int amount)
        {
            if (IsDead) return;
            if (amount <= 0) return;

            int before = CurrentHealth;
            CurrentHealth -= amount;
            CurrentHealth  = Mathf.Max(CurrentHealth, 0);
            int appliedDamage = before - CurrentHealth;
            if (appliedDamage > 0)
                ShowDamagePopup(appliedDamage);
            UpdateHealthBar();
            if (CurrentHealth <= 0) Die();
        }

        public virtual void Heal(int amount)
        {
            if (IsDead) return;
            CurrentHealth += amount;
            CurrentHealth  = Mathf.Min(CurrentHealth, MaxHealth);
            UpdateHealthBar();
        }

        protected virtual void Die()
        {
            IsDead = true;
            OnDied();
            Destroy(gameObject);
        }

        protected abstract void OnDied();

        // ── Health bar (procedural) ───────────────────────────────────
        private void CreateHealthBar()
        {
            // World-space canvas as a child
            GameObject canvasGO = new GameObject("HealthBarCanvas");
            canvasGO.transform.SetParent(transform, false);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGO.transform.localPosition = new Vector3(0f, 0.65f, -0.1f);
            canvasGO.transform.localScale    = new Vector3(0.01f, 0.01f, 1f);

            // Background
            GameObject bgGO  = new GameObject("BG");
            bgGO.transform.SetParent(canvasGO.transform, false);
            Image bg = bgGO.AddComponent<Image>();
            bg.color = Color.black;
            bg.rectTransform.sizeDelta = new Vector2(60f, 8f);

            // Fill
            GameObject fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(canvasGO.transform, false);
            _healthBarFill = fillGO.AddComponent<Image>();
            _healthBarFill.color = new Color(0.2f, 0.8f, 0.2f);
            _healthBarFill.rectTransform.sizeDelta = new Vector2(60f, 8f);
            _healthBarFill.rectTransform.pivot     = new Vector2(0f, 0.5f);
            _healthBarFill.rectTransform.anchoredPosition = new Vector2(-30f, 0f);
        }

        private void UpdateHealthBar()
        {
            if (_healthBarFill == null) return;
            float ratio = (float)CurrentHealth / MaxHealth;
            _healthBarFill.rectTransform.sizeDelta = new Vector2(60f * ratio, 8f);
            _healthBarFill.color = Color.Lerp(Color.red, new Color(0.2f, 0.8f, 0.2f), ratio);
        }

        private void ShowDamagePopup(int damage)
        {
            if (damage <= 0) return;

            GameObject popup = new GameObject("DamagePopup");
            popup.transform.position = transform.position + new Vector3(0f, DAMAGE_POPUP_VERTICAL_OFFSET, 0f);

            TextMesh text = popup.AddComponent<TextMesh>();
            text.text = damage.ToString();
            text.fontSize = DAMAGE_POPUP_FONT_SIZE;
            text.characterSize = DAMAGE_POPUP_CHARACTER_SIZE;
            text.alignment = TextAlignment.Center;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = this is Enemy ? DAMAGE_POPUP_ENEMY_COLOR : DAMAGE_POPUP_ALLY_COLOR;

            if (_damagePopupFont == null)
                _damagePopupFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            if (_damagePopupFont != null)
            {
                text.font = _damagePopupFont;
                MeshRenderer mr = popup.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.material = _damagePopupFont.material;
                    mr.sortingOrder = 20;
                }
            }

            // Ensure cleanup even if this Unit gets destroyed before coroutine completion.
            Destroy(popup, DAMAGE_POPUP_DURATION + 0.1f);
            StartCoroutine(DamagePopupRoutine(popup, text, text.color));
        }

        private IEnumerator DamagePopupRoutine(GameObject popup, TextMesh text, Color startColor)
        {
            float t = 0f;
            while (t < DAMAGE_POPUP_DURATION && popup != null && text != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / DAMAGE_POPUP_DURATION);
                popup.transform.position += Vector3.up * (DAMAGE_POPUP_RISE_SPEED * Time.deltaTime);
                Color c = startColor;
                c.a = 1f - k;
                text.color = c;
                yield return null;
            }

            if (popup != null)
                Destroy(popup);
        }
    }
}
