using UnityEngine;
using Gymageddon.Entities;

namespace Gymageddon.Core
{
    /// <summary>
    /// Represents a single horizontal lane.
    /// Enforces the rule: maximum 1 Character and 1 Trainer per lane.
    /// </summary>
    public class Lane : MonoBehaviour
    {
        public int LaneIndex { get; private set; }

        // World-space anchor points where units are placed
        public Transform CharacterSlot { get; private set; }
        public Transform TrainerSlot   { get; private set; }

        // Currently occupying units (null = empty)
        public Character OccupyingCharacter { get; private set; }
        public Trainer   OccupyingTrainer   { get; private set; }

        // ── Queries ───────────────────────────────────────────────────
        public bool IsCharacterSlotEmpty => OccupyingCharacter == null;
        public bool IsTrainerSlotEmpty   => OccupyingTrainer   == null;

        // ── Initialisation ────────────────────────────────────────────
        public void Init(int index, Transform characterSlot, Transform trainerSlot)
        {
            LaneIndex      = index;
            CharacterSlot  = characterSlot;
            TrainerSlot    = trainerSlot;
        }

        // ── Placement ─────────────────────────────────────────────────
        /// <summary>
        /// Places a character in this lane. Returns false if the slot is already occupied.
        /// </summary>
        public bool PlaceCharacter(Character character)
        {
            if (!IsCharacterSlotEmpty)
            {
                Debug.LogWarning($"[Lane {LaneIndex}] Character slot already occupied.");
                return false;
            }

            OccupyingCharacter = character;
            character.transform.SetParent(CharacterSlot, false);
            character.transform.localPosition = Vector3.zero;
            character.OnPlaced(this);

            // Apply any existing trainer buff
            if (OccupyingTrainer != null)
                OccupyingTrainer.ApplyBuffTo(OccupyingCharacter);

            GameEvents.RaiseCharacterPlaced(LaneIndex, character);
            return true;
        }

        /// <summary>
        /// Places a trainer in this lane. Returns false if the slot is already occupied.
        /// </summary>
        public bool PlaceTrainer(Trainer trainer)
        {
            if (!IsTrainerSlotEmpty)
            {
                Debug.LogWarning($"[Lane {LaneIndex}] Trainer slot already occupied.");
                return false;
            }

            OccupyingTrainer = trainer;
            trainer.transform.SetParent(TrainerSlot, false);
            trainer.transform.localPosition = Vector3.zero;
            trainer.OnPlaced(this);

            // Apply buff to character already in this lane (if any)
            if (OccupyingCharacter != null)
                trainer.ApplyBuffTo(OccupyingCharacter);

            GameEvents.RaiseTrainerPlaced(LaneIndex, trainer);
            return true;
        }

        // ── Removal ───────────────────────────────────────────────────
        public void RemoveCharacter()
        {
            if (OccupyingCharacter != null)
            {
                OccupyingCharacter.OnRemoved();
                Destroy(OccupyingCharacter.gameObject);
                OccupyingCharacter = null;
            }
        }

        public void RemoveTrainer()
        {
            if (OccupyingTrainer != null)
            {
                // Remove buff from character before destroying trainer
                if (OccupyingCharacter != null)
                    OccupyingTrainer.RemoveBuffFrom(OccupyingCharacter);

                OccupyingTrainer.OnRemoved();
                Destroy(OccupyingTrainer.gameObject);
                OccupyingTrainer = null;
            }
        }

        // ── Visual highlight (used by PlacementManager) ───────────────
        private SpriteRenderer _laneBackground;

        /// <summary>Call once from GameBootstrap after the background SpriteRenderer is created.</summary>
        public void SetBackgroundRenderer(SpriteRenderer sr) => _laneBackground = sr;

        public void SetHighlight(bool active)
        {
            if (_laneBackground == null) return;
            _laneBackground.color = active
                ? new Color(1f, 1f, 0.6f)   // yellow highlight
                : _baseColor;
        }

        private Color _baseColor;
        public void SetBaseColor(Color c) { _baseColor = c; }
    }
}
