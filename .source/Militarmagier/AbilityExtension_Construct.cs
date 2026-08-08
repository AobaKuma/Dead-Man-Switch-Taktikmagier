using RimWorld;
using RimWorld.Planet;
using Verse;
using Ability = VEF.Abilities.Ability;

namespace Militarmagier
{
    /// <summary>
    /// Assembles a non-pawn construct (a turret) out of a consumed component.
    ///
    /// Buildings are not <c>IMinHeatGiver</c>, so VPE's CompBreakLink cannot register them for
    /// upkeep by itself and we track them on <see cref="Hediff_Focus"/> instead. The heat value
    /// comes from the construct's <see cref="ConstructHeatExtension"/>.
    /// </summary>
    public class AbilityExtension_Construct : AbilityExtension_ConstructBase
    {
        public ThingDef constructDef;

        public override void Cast(GlobalTargetInfo[] targets, Ability ability)
        {
            base.Cast(targets, ability);

            Pawn caster = ability?.pawn;
            if (caster == null || constructDef == null)
            {
                return;
            }

            foreach (GlobalTargetInfo target in targets)
            {
                if (!TryConsumeCost(target, out IntVec3 position, out Map map))
                {
                    continue;
                }

                ThingDef stuff = constructDef.MadeFromStuff ? GenStuff.DefaultStuffFor(constructDef) : null;
                Thing construct = ThingMaker.MakeThing(constructDef, stuff);
                construct.SetFactionDirect(caster.Faction);
                LinkToCaster(construct, caster);

                GenSpawn.Spawn(construct, position, map);

                // Register only after spawning: Hediff_Focus prunes unspawned givers.
                FocusOf(caster).AddHeatGiver(construct);
            }
        }

        private static Hediff_Focus FocusOf(Pawn caster)
        {
            Hediff_Focus focus = caster.health.hediffSet
                .GetFirstHediffOfDef(MilitarmagierDefOf.DMST_PsycastFocus) as Hediff_Focus;

            return focus ?? (Hediff_Focus)caster.health.AddHediff(MilitarmagierDefOf.DMST_PsycastFocus);
        }
    }
}
