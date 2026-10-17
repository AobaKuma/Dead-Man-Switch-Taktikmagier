using RimWorld;
using Verse;

namespace Militarmagier
{
    public class CompProperties_AbilitySwap : CompProperties_AbilityEffect
    {
        public CompProperties_AbilitySwap()
        {
            compClass = typeof(CompAbilityEffect_Swap);
        }
    }

    /// <summary>
    /// Trades places with the target pawn.
    /// </summary>
    public class CompAbilityEffect_Swap : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Pawn other = target.Pawn;
            Map map = caster?.Map;
            if (other == null || map == null || !other.Spawned || other == caster)
            {
                return;
            }

            IntVec3 destination = other.Position;
            IntVec3 origin = caster.Position;

            SkipUtility.SkipTo(caster, destination, map);
            other.Position = origin;
            other.Notify_Teleported(true, false);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            return target.Pawn != null && target.Pawn != parent.pawn && base.Valid(target, throwMessages);
        }
    }
}
