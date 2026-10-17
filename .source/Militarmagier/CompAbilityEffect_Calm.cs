using RimWorld;
using Verse;

namespace Militarmagier
{
    public class CompProperties_AbilityCalm : CompProperties_AbilityEffect
    {
        public ThoughtDef thought;

        public CompProperties_AbilityCalm()
        {
            compClass = typeof(CompAbilityEffect_Calm);
        }
    }

    /// <summary>
    /// Refills the target's rest need and plants a reassuring memory.
    /// </summary>
    public class CompAbilityEffect_Calm : CompAbilityEffect
    {
        public new CompProperties_AbilityCalm Props => (CompProperties_AbilityCalm)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn pawn = target.Pawn;
            if (pawn?.needs == null)
            {
                return;
            }

            Need_Rest rest = pawn.needs.rest;
            if (rest != null)
            {
                rest.CurLevel = rest.MaxLevel;
            }

            // Mechs, constructs and animals have no mood need - guard it the same way rest is.
            if (Props.thought != null)
            {
                pawn.needs.mood?.thoughts?.memories?.TryGainMemory(Props.thought);
            }
        }
    }
}
