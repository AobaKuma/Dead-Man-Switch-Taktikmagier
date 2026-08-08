using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using VEF.Abilities;
using Ability = VEF.Abilities.Ability;

namespace Militarmagier
{
    /// <summary>
    /// Field-tends every tendable injury on the target in one go.
    /// </summary>
    public class AbilityExtension_Heal : AbilityExtension_AbilityMod
    {
        public FloatRange tendQualityRange;

        public override void Cast(GlobalTargetInfo[] targets, Ability ability)
        {
            base.Cast(targets, ability);

            foreach (GlobalTargetInfo target in targets)
            {
                Pawn pawn = target.Pawn;
                if (pawn?.health == null)
                {
                    continue;
                }

                int tended = 0;
                List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
                for (int i = hediffs.Count - 1; i >= 0; i--)
                {
                    if ((hediffs[i] is Hediff_Injury || hediffs[i] is Hediff_MissingPart) && hediffs[i].TendableNow())
                    {
                        hediffs[i].Tended(tendQualityRange.RandomInRange, tendQualityRange.TrueMax, 1);
                        tended++;
                    }
                }

                if (pawn.Map == null)
                {
                    continue;
                }

                if (tended > 0)
                {
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "NumWoundsTended".Translate(tended), 3.65f);
                }
                FleckMaker.AttachedOverlay(pawn, FleckDefOf.FlashHollow, Vector3.zero, 1.5f);
            }
        }
    }
}
