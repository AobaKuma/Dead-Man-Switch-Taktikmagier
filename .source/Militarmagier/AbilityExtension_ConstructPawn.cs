using RimWorld.Planet;
using Verse;
using Ability = VEF.Abilities.Ability;

namespace Militarmagier
{
    /// <summary>
    /// Assembles a construct *pawn* (the gargoyle) out of a consumed component.
    ///
    /// This intentionally does not touch <see cref="Hediff_Focus"/>. <c>Pawn_Construct</c>
    /// implements VPE's <c>IMinHeatGiver</c> and <c>CompBreakLink.PostSpawnSetup</c> registers
    /// the pawn with the caster's <c>Hediff_PsycastAbilities</c> automatically, which also
    /// handles pruning and save/load for us. Registering here as well would charge the caster
    /// twice for the same construct — the bug this replaced.
    /// <see cref="Pawn_ConstructWeaponUsable"/> supplies the heat value.
    /// </summary>
    public class AbilityExtension_ConstructPawn : AbilityExtension_ConstructBase
    {
        public PawnKindDef constructDef;

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

                Pawn construct = PawnGenerator.GeneratePawn(constructDef, caster.Faction);

                // Must be set before spawning: CompBreakLink reads Pawn in PostSpawnSetup to
                // register the construct as a min-heat giver.
                LinkToCaster(construct, caster);

                GenSpawn.Spawn(construct, position, map);
            }
        }
    }
}
