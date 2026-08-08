using RimWorld;
using RimWorld.Planet;
using Verse;
using VEF.Abilities;
using Ability = VEF.Abilities.Ability;

namespace Militarmagier
{
    /// <summary>
    /// Refills the target's rest need and plants a reassuring memory.
    /// </summary>
    public class AbilityExtension_Calm : AbilityExtension_AbilityMod
    {
        public ThoughtDef thought;

        public override void Cast(GlobalTargetInfo[] targets, Ability ability)
        {
            base.Cast(targets, ability);

            foreach (GlobalTargetInfo target in targets)
            {
                Pawn pawn = target.Pawn;
                if (pawn?.needs == null)
                {
                    continue;
                }

                Need_Rest rest = pawn.needs.rest;
                if (rest != null)
                {
                    rest.CurLevel = rest.MaxLevel;
                }

                // Mechs, constructs and animals have no mood need - guard it the same way rest is.
                if (thought != null)
                {
                    pawn.needs.mood?.thoughts?.memories?.TryGainMemory(thought);
                }
            }
        }
    }
}
