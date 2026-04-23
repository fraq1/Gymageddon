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
            character.transform.SetParent(CharacterSlot, true);
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
            trainer.transform.SetParent(TrainerSlot, true);
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
                if (OccupyingTrainer != null)
                    OccupyingTrainer.RemoveBuffFrom(OccupyingCharacter);
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

        public Character DetachCharacterForMove()
        {
            if (OccupyingCharacter == null) return null;
            Character character = OccupyingCharacter;
            if (OccupyingTrainer != null)
                OccupyingTrainer.RemoveBuffFrom(character);
            character.OnRemoved();
            character.transform.SetParent(null, true);
            OccupyingCharacter = null;
            return character;
        }

        public Trainer DetachTrainerForMove()
        {
            if (OccupyingTrainer == null) return null;
            Trainer trainer = OccupyingTrainer;
            if (OccupyingCharacter != null)
                trainer.RemoveBuffFrom(OccupyingCharacter);
            trainer.OnRemoved();
            trainer.transform.SetParent(null, true);
            OccupyingTrainer = null;
            return trainer;
        }

        public bool AttachMovedCharacter(Character character)
        {
            if (character == null || OccupyingCharacter != null) return false;
            OccupyingCharacter = character;
            character.transform.SetParent(CharacterSlot, true);
            character.transform.localPosition = Vector3.zero;
            character.OnPlaced(this);
            if (OccupyingTrainer != null)
                OccupyingTrainer.ApplyBuffTo(character);
            GameEvents.RaiseCharacterPlaced(LaneIndex, character);
            return true;
        }

        public bool AttachMovedTrainer(Trainer trainer)
        {
            if (trainer == null || OccupyingTrainer != null) return false;
            OccupyingTrainer = trainer;
            trainer.transform.SetParent(TrainerSlot, true);
            trainer.transform.localPosition = Vector3.zero;
            trainer.OnPlaced(this);
            if (OccupyingCharacter != null)
                trainer.ApplyBuffTo(OccupyingCharacter);
            GameEvents.RaiseTrainerPlaced(LaneIndex, trainer);
            return true;
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

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.6f);
            Gizmos.DrawWireCube(transform.position, new Vector3(9f, 2.2f, 0.02f));

            if (CharacterSlot != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(CharacterSlot.position, 0.18f);
                Gizmos.DrawLine(CharacterSlot.position + Vector3.up * 0.25f, CharacterSlot.position - Vector3.up * 0.25f);
            }

            if (TrainerSlot != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(TrainerSlot.position, 0.18f);
                Gizmos.DrawLine(TrainerSlot.position + Vector3.up * 0.25f, TrainerSlot.position - Vector3.up * 0.25f);
            }
        }
#endif
    }
}
