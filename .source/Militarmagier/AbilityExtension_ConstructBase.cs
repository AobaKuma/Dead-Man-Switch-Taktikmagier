using RimWorld;
using Verse;
using VEF.Abilities;
using Ability = VEF.Abilities.Ability;
using RimWorld.Planet;

namespace Militarmagier
{
    /// <summary>
    /// Shared targeting and cost handling for the "assemble construct" abilities. The ability
    /// consumes one <see cref="costDef"/> item and spawns a construct where that item lay.
    /// </summary>
    public abstract class AbilityExtension_ConstructBase : AbilityExtension_AbilityMod
    {
        public ThingDef costDef;

        /// <summary>
        /// Consumes one cost item and hands back where it stood.
        /// Returns false if the target is no longer a usable cost item.
        /// </summary>
        protected bool TryConsumeCost(GlobalTargetInfo target, out IntVec3 position, out Map map)
        {
            position = IntVec3.Invalid;
            map = null;

            Thing cost = target.Thing;
            if (cost == null || cost.Destroyed || cost.def != costDef || cost.Map == null)
            {
                return false;
            }

            // Read the location before consuming: SplitOff despawns the item when it takes the
            // whole stack, after which Position/Map are no longer meaningful.
            position = cost.Position;
            map = cost.Map;

            // SplitOff hands back either a freshly made stack of 1 or the original thing itself.
            // Either way it is unspawned and unowned, so it must be destroyed or it leaks.
            cost.SplitOff(1).Destroy();
            return true;
        }

        /// <summary>Links a construct to its summoner so VPE's break-link gizmo and upkeep work.</summary>
        protected static void LinkToCaster(Thing construct, Pawn caster)
        {
            VanillaPsycastsExpanded.CompBreakLink link = construct.TryGetComp<VanillaPsycastsExpanded.CompBreakLink>();
            if (link != null)
            {
                link.Pawn = caster;
                return;
            }

            Log.ErrorOnce(
                "[Taktikmagier] " + construct.def.defName + " is missing CompProperties_BreakLink; "
                + "it will not be tied to its summoner and will never be cleaned up.",
                construct.def.shortHash);
        }

        public override bool ValidateTarget(LocalTargetInfo target, Ability ability, bool throwMessages = false)
        {
            Thing thing = target.Thing;
            if (thing != null && !thing.Destroyed && thing.def == costDef)
            {
                return true;
            }

            if (throwMessages && costDef != null)
            {
                Messages.Message(
                    "DMST_ConstructNeedsCost".Translate(costDef.label),
                    MessageTypeDefOf.RejectInput,
                    historical: false);
            }
            return false;
        }

        public override bool CanApplyOn(LocalTargetInfo target, Ability ability, bool throwMessages = false)
        {
            return ValidateTarget(target, ability, throwMessages);
        }
    }
}
