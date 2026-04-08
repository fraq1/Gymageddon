using System.Collections;
using UnityEngine;
using Gymageddon.Core;
using Gymageddon.Data;

namespace Gymageddon.Entities
{
    /// <summary>
    /// Support equipment placed in a lane to buff the lane's Character.
    /// </summary>
    public class Trainer : Unit
    {
        public TrainerData Data { get; private set; }
        public string TrainerName => Data ? Data.trainerName : "Unknown";

        private Lane _lane;
        private Coroutine _energyRegenRoutine;

        // ── Setup ─────────────────────────────────────────────────────
        public void Init(TrainerData data)
        {
            Data = data;
            InitHealth(50); // trainers have a fixed small health pool
            ApplyVisual(data.bodyColor);
        }

        public void OnPlaced(Lane lane)
        {
            _lane = lane;

            if (Data.effectType == TrainerEffectType.EnergyRegen && Data.energyRegenPerSecond > 0f)
                _energyRegenRoutine = StartCoroutine(EnergyRegenRoutine());
        }

        public void OnRemoved() => StopAllCoroutines();

        // ── Buff application / removal ────────────────────────────────
        public void ApplyBuffTo(Character character)
        {
            if (character == null || Data == null) return;
            switch (Data.effectType)
            {
                case TrainerEffectType.DamageBoost:
                    character.ApplyDamageBoost(Data.effectValue);
                    break;
                case TrainerEffectType.AttackSpeedBoost:
                    character.ApplySpeedBoost(Data.effectValue);
                    break;
                case TrainerEffectType.HealthBoost:
                    character.ApplyHealthBoost(Data.effectValue);
                    break;
                case TrainerEffectType.EnergyRegen:
                    // Energy handled via EnergyRegenRoutine; no direct character buff
                    break;
            }
        }

        public void RemoveBuffFrom(Character character)
        {
            if (character == null || Data == null) return;
            switch (Data.effectType)
            {
                case TrainerEffectType.DamageBoost:  character.RemoveDamageBoost(); break;
                case TrainerEffectType.AttackSpeedBoost: character.RemoveSpeedBoost(); break;
                case TrainerEffectType.HealthBoost:  character.RemoveHealthBoost(); break;
            }
        }

        // ── Energy regen loop ─────────────────────────────────────────
        private IEnumerator EnergyRegenRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);
                if (Managers.ResourceManager.Instance != null)
                    Managers.ResourceManager.Instance.AddEnergy(Mathf.RoundToInt(Data.energyRegenPerSecond));
            }
        }

        // ── Death ─────────────────────────────────────────────────────
        protected override void OnDied()
        {
            StopAllCoroutines();
            _lane?.RemoveTrainer();
            Debug.Log($"[Trainer] {TrainerName} in lane {_lane?.LaneIndex} has been destroyed.");
        }

        // ── Visual ────────────────────────────────────────────────────
        private void ApplyVisual(Color color)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = color;
        }
    }
}
