using RimWorld;
using RimWorld.Planet;
using Verse;
using Ability = VEF.Abilities.Ability;

namespace Militarmagier
{
    /// <summary>
    /// Trades places with the target pawn.
    /// </summary>
    public class Ability_Swap : Ability
    {
        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);

            if (targets.Length == 0)
            {
                return;
            }

            Pawn target = targets[0].Pawn;
            Map map = pawn?.Map;
            if (target == null || map == null || !target.Spawned || target == pawn)
            {
                return;
            }

            IntVec3 destination = target.Position;
            IntVec3 origin = pawn.Position;

            SkipUtility.SkipTo(pawn, destination, map);
            target.Position = origin;
            target.Notify_Teleported(true, false);
        }
    }
}
