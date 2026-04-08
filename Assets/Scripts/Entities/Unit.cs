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
            CurrentHealth -= amount;
            CurrentHealth  = Mathf.Max(CurrentHealth, 0);
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
    }
}
