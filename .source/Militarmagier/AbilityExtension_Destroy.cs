using RimWorld.Planet;
using UnityEngine;
using Verse;
using VEF.Abilities;
using Ability = VEF.Abilities.Ability;

namespace Militarmagier
{
    /// <summary>
    /// Unmakes a structure: weak buildings come apart entirely, sturdier ones are left badly
    /// damaged.
    /// </summary>
    public class AbilityExtension_Destroy : AbilityExtension_AbilityMod
    {
        public int destroyPoints = 300;
        public float undestroyFactor = 0.5f;

        public override void Cast(GlobalTargetInfo[] targets, Ability ability)
        {
            base.Cast(targets, ability);

            foreach (GlobalTargetInfo target in targets)
            {
                // Pattern match rather than a hard cast: the targeting parameters allow anything
                // in ThingCategory.Building, and not every such def uses the Building class.
                if (!(target.Thing is Building building) || !building.def.destroyable)
                {
                    continue;
                }

                if (building.HitPoints <= destroyPoints)
                {
                    // KillFinalize, not the default Vanish, so the teardown leaves salvage and
                    // debris behind - the ability disassembles things, it does not erase them.
                    building.Destroy(DestroyMode.KillFinalize);
                }
                else
                {
                    // Never round down to 0: a 0 HP building is left standing in a broken state.
                    building.HitPoints = Mathf.Max(1, Mathf.RoundToInt(building.HitPoints * undestroyFactor));
                }
            }
        }
    }
}
