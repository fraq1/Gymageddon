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
        public int LaneIndex => _lane != null ? _lane.LaneIndex : -1;

        private Lane _lane;

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
        }

        public void OnRemoved()
        {
            StopAllCoroutines();
            _lane = null;
        }

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
