using RimWorld;
using UnityEngine;
using Verse;

namespace Militarmagier
{
    public class CompProperties_AbilityDestroy : CompProperties_AbilityEffect
    {
        public int destroyPoints = 300;
        public float undestroyFactor = 0.5f;

        public CompProperties_AbilityDestroy()
        {
            compClass = typeof(CompAbilityEffect_Destroy);
        }
    }

    /// <summary>
    /// Unmakes a structure: weak buildings come apart entirely, sturdier ones are left badly
    /// damaged.
    /// </summary>
    public class CompAbilityEffect_Destroy : CompAbilityEffect
    {
        public new CompProperties_AbilityDestroy Props => (CompProperties_AbilityDestroy)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            // Pattern match rather than a hard cast: the targeting parameters allow anything in
            // ThingCategory.Building, and not every such def uses the Building class.
            if (!(target.Thing is Building building) || !building.def.destroyable)
            {
                return;
            }

            if (building.HitPoints <= Props.destroyPoints)
            {
                // KillFinalize, not the default Vanish, so the teardown leaves salvage and debris
                // behind - the ability disassembles things, it does not erase them.
                building.Destroy(DestroyMode.KillFinalize);
            }
            else
            {
                // Never round down to 0: a 0 HP building is left standing in a broken state.
                building.HitPoints = Mathf.Max(1, Mathf.RoundToInt(building.HitPoints * Props.undestroyFactor));
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            return target.Thing is Building building && building.def.destroyable && base.Valid(target, throwMessages);
        }
    }
}
