using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Militarmagier
{
    /// <summary>
    /// Gates a vanilla <see cref="RimWorld.Ability"/> on the caster wearing a full set of apparel.
    ///
    /// Used by DMST_Camouflage: the ability is granted by the magier cloak, but both the cloak
    /// and the helmet describe themselves as needing the other piece to work. Without this the
    /// cloak alone gave full invisibility and the descriptions were fiction.
    /// </summary>
    public class CompProperties_AbilityRequireApparel : AbilityCompProperties
    {
        public List<ThingDef> requiredApparel;

        public CompProperties_AbilityRequireApparel()
        {
            compClass = typeof(CompAbilityEffect_RequireApparel);
        }
    }

    public class CompAbilityEffect_RequireApparel : AbilityComp
    {
        public CompProperties_AbilityRequireApparel Props => (CompProperties_AbilityRequireApparel)props;

        /// <summary>
        /// Gating on CanCast rather than only GizmoDisabled matters: Ability.CanCast is what the
        /// AI casting path consults, so this stops an unequipped raider from using it too.
        /// </summary>
        public override bool CanCast => MissingPiece() == null;

        public override bool GizmoDisabled(out string reason)
        {
            ThingDef missing = MissingPiece();
            if (missing == null)
            {
                reason = null;
                return false;
            }

            reason = "DMST_CamouflageNeedsApparel".Translate(missing.label).Resolve();
            return true;
        }

        /// <summary>The first required piece the caster is not wearing, or null if the set is complete.</summary>
        private ThingDef MissingPiece()
        {
            List<ThingDef> required = Props.requiredApparel;
            if (required.NullOrEmpty())
            {
                return null;
            }

            List<Apparel> worn = parent?.pawn?.apparel?.WornApparel;
            if (worn == null)
            {
                return required[0];
            }

            for (int i = 0; i < required.Count; i++)
            {
                bool wearing = false;
                for (int j = 0; j < worn.Count; j++)
                {
                    if (worn[j].def == required[i])
                    {
                        wearing = true;
                        break;
                    }
                }

                if (!wearing)
                {
                    return required[i];
                }
            }

            return null;
        }
    }
}
