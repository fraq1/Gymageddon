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

        public bool MoveCharacter(int fromLaneIndex, int toLaneIndex)
        {
            if (fromLaneIndex == toLaneIndex) return true;
            Lane from = GetLane(fromLaneIndex);
            Lane to = GetLane(toLaneIndex);
            if (from == null || to == null || from.OccupyingCharacter == null)
                return false;

            Character moving = from.OccupyingCharacter;
            Character target = to.OccupyingCharacter;

            if (target != null)
            {
                if (target.TryEvolveWith(moving.Data))
                {
                    from.RemoveCharacter();
                    return true;
                }

                if (!to.IsCharacterSlotEmpty)
                    return false;
            }

            moving = from.DetachCharacterForMove();
            if (moving == null) return false;
            if (to.AttachMovedCharacter(moving)) return true;
            from.AttachMovedCharacter(moving);
            return false;
        }

        public bool MoveTrainer(int fromLaneIndex, int toLaneIndex)
        {
            if (fromLaneIndex == toLaneIndex) return true;
            Lane from = GetLane(fromLaneIndex);
            Lane to = GetLane(toLaneIndex);
            if (from == null || to == null || from.OccupyingTrainer == null || !to.IsTrainerSlotEmpty)
                return false;

            Trainer moving = from.DetachTrainerForMove();
            if (moving == null) return false;
            if (to.AttachMovedTrainer(moving)) return true;
            from.AttachMovedTrainer(moving);
            return false;
        }

        public bool SwapCharacters(int laneAIndex, int laneBIndex)
        {
            if (laneAIndex == laneBIndex) return true;
            Lane laneA = GetLane(laneAIndex);
            Lane laneB = GetLane(laneBIndex);
            if (laneA == null || laneB == null || laneA.OccupyingCharacter == null || laneB.OccupyingCharacter == null)
                return false;

            Character a = laneA.DetachCharacterForMove();
            Character b = laneB.DetachCharacterForMove();
            bool okA = laneA.AttachMovedCharacter(b);
            bool okB = laneB.AttachMovedCharacter(a);
            return okA && okB;
        }

        public bool SwapTrainers(int laneAIndex, int laneBIndex)
        {
            if (laneAIndex == laneBIndex) return true;
            Lane laneA = GetLane(laneAIndex);
            Lane laneB = GetLane(laneBIndex);
            if (laneA == null || laneB == null || laneA.OccupyingTrainer == null || laneB.OccupyingTrainer == null)
                return false;

            Trainer a = laneA.DetachTrainerForMove();
            Trainer b = laneB.DetachTrainerForMove();
            bool okA = laneA.AttachMovedTrainer(b);
            bool okB = laneB.AttachMovedTrainer(a);
            return okA && okB;
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
