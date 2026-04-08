using UnityEngine;
using Gymageddon.Entities;

namespace Gymageddon.Core
{
    /// <summary>
    /// Manages the 5 lanes and enforces placement rules.
    /// </summary>
    public class GameBoard : MonoBehaviour
    {
        public const int LANE_COUNT = 5;

        private Lane[] _lanes = new Lane[LANE_COUNT];

        // ── Initialisation ────────────────────────────────────────────
        public void RegisterLane(Lane lane)
        {
            if (lane.LaneIndex < 0 || lane.LaneIndex >= LANE_COUNT)
            {
                Debug.LogError($"[GameBoard] Invalid lane index: {lane.LaneIndex}");
                return;
            }
            _lanes[lane.LaneIndex] = lane;
        }

        public Lane GetLane(int index)
        {
            if (index < 0 || index >= LANE_COUNT) return null;
            return _lanes[index];
        }

        // ── Placement ─────────────────────────────────────────────────
        public bool CanPlaceCharacter(int laneIndex)
        {
            Lane lane = GetLane(laneIndex);
            return lane != null && lane.IsCharacterSlotEmpty;
        }

        public bool CanPlaceTrainer(int laneIndex)
        {
            Lane lane = GetLane(laneIndex);
            return lane != null && lane.IsTrainerSlotEmpty;
        }

        public bool PlaceCharacter(int laneIndex, Character character)
        {
            Lane lane = GetLane(laneIndex);
            if (lane == null) return false;
            return lane.PlaceCharacter(character);
        }

        public bool PlaceTrainer(int laneIndex, Trainer trainer)
        {
            Lane lane = GetLane(laneIndex);
            if (lane == null) return false;
            return lane.PlaceTrainer(trainer);
        }

        // ── State queries ─────────────────────────────────────────────
        public bool AllLanesHaveCharacters()
        {
            foreach (Lane l in _lanes)
                if (l == null || l.IsCharacterSlotEmpty) return false;
            return true;
        }

        public bool AllLanesHaveTrainers()
        {
            foreach (Lane l in _lanes)
                if (l == null || l.IsTrainerSlotEmpty) return false;
            return true;
        }
    }
}
