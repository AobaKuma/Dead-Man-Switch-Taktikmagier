using Fortified;
using RimWorld;
using Verse;

namespace Militarmagier
{
    public abstract class CompProperties_AbilityConstructBase : CompProperties_AbilityEffect
    {
        /// <summary>The item the ability is aimed at and consumes; the construct appears where it lay.</summary>
        public ThingDef costDef;
    }

    public class CompProperties_AbilityConstructTurret : CompProperties_AbilityConstructBase
    {
        public ThingDef constructDef;

        public CompProperties_AbilityConstructTurret()
        {
            compClass = typeof(CompAbilityEffect_ConstructTurret);
        }
    }

    public class CompProperties_AbilityConstructPawn : CompProperties_AbilityConstructBase
    {
        public PawnKindDef constructDef;

        public CompProperties_AbilityConstructPawn()
        {
            compClass = typeof(CompAbilityEffect_ConstructPawn);
        }
    }

    /// <summary>
    /// Shared targeting, cost and upkeep handling for the "assemble construct" abilities. The ability
    /// consumes one <see cref="CompProperties_AbilityConstructBase.costDef"/> item and spawns a
    /// construct where that item lay. Every construct is registered on the caster's
    /// <see cref="Hediff_Focus"/>, which lowers their neural heat limit while it lives.
    /// </summary>
    public abstract class CompAbilityEffect_ConstructBase : CompAbilityEffect
    {
        public new CompProperties_AbilityConstructBase Props => (CompProperties_AbilityConstructBase)props;

        /// <summary>The def carrying the construct's <see cref="ConstructHeatExtension"/>.</summary>
        protected abstract ThingDef ConstructThingDef { get; }

        /// <summary>
        /// Refuses a summon that would push the caster into overload once the construct starts
        /// occupying heat capacity. Skipped when the player has turned the heat limit off, which is
        /// an explicit choice to overload.
        /// </summary>
        public override bool GizmoDisabled(out string reason)
        {
            Pawn_PsychicEntropyTracker tracker = parent.pawn?.psychicEntropy;
            int upkeep = ConstructHeatExtension.HeatFor(ConstructThingDef);
            if (tracker != null && tracker.limitEntropyAmount && upkeep > 0
                && tracker.EntropyValue + parent.def.EntropyGain + upkeep > tracker.MaxEntropy)
            {
                reason = "DMST_ConstructNoCapacity".Translate(upkeep).Resolve();
                return true;
            }

            reason = null;
            return false;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Thing thing = target.Thing;
            if (thing != null && !thing.Destroyed && thing.def == Props.costDef)
            {
                return base.Valid(target, throwMessages);
            }

            if (throwMessages && Props.costDef != null)
            {
                Messages.Message(
                    "DMST_ConstructNeedsCost".Translate(Props.costDef.label),
                    MessageTypeDefOf.RejectInput,
                    historical: false);
            }
            return false;
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return Valid(target);
        }

        /// <summary>
        /// Consumes one cost item and hands back where it stood.
        /// Returns false if the target is no longer a usable cost item.
        /// </summary>
        protected bool TryConsumeCost(LocalTargetInfo target, out IntVec3 position, out Map map)
        {
            position = IntVec3.Invalid;
            map = null;

            Thing cost = target.Thing;
            if (cost == null || cost.Destroyed || cost.def != Props.costDef || cost.Map == null)
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
    }

    /// <summary>Assembles a turret construct.</summary>
    public class CompAbilityEffect_ConstructTurret : CompAbilityEffect_ConstructBase
    {
        public new CompProperties_AbilityConstructTurret Props => (CompProperties_AbilityConstructTurret)props;

        protected override ThingDef ConstructThingDef => Props.constructDef;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            if (caster == null || Props.constructDef == null || !TryConsumeCost(target, out IntVec3 position, out Map map))
            {
                return;
            }

            ThingDef stuff = Props.constructDef.MadeFromStuff ? GenStuff.DefaultStuffFor(Props.constructDef) : null;
            Thing construct = ThingMaker.MakeThing(Props.constructDef, stuff);
            construct.SetFactionDirect(caster.Faction);
            CompMagierLink.LinkTo(construct, caster);

            GenSpawn.Spawn(construct, position, map);

            // Register only after spawning: Hediff_Focus prunes unspawned givers.
            Hediff_Focus.GetOrAdd(caster).AddHeatGiver(construct);
        }
    }

    /// <summary>
    /// Assembles a construct pawn (the gargoyle). It is an FFF drone whose control platform is the
    /// caster, so it can only be drafted while the caster is drafted on the same map.
    /// </summary>
    public class CompAbilityEffect_ConstructPawn : CompAbilityEffect_ConstructBase
    {
        public new CompProperties_AbilityConstructPawn Props => (CompProperties_AbilityConstructPawn)props;

        protected override ThingDef ConstructThingDef => Props.constructDef?.race;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            if (caster == null || Props.constructDef == null || !TryConsumeCost(target, out IntVec3 position, out Map map))
            {
                return;
            }

            Pawn construct = PawnGenerator.GeneratePawn(Props.constructDef, caster.Faction);

            CompDrone drone = construct.TryGetComp<CompDrone>();
            if (drone != null)
            {
                drone.SetPlatform(caster);
            }
            else
            {
                Log.ErrorOnce(
                    "[Taktikmagier] " + construct.def.defName + " has no Fortified.CompProperties_Drone; "
                    + "it cannot be tied to its summoner for drafting.",
                    construct.def.shortHash ^ 0x5A17);
            }
            CompMagierLink.LinkTo(construct, caster);

            GenSpawn.Spawn(construct, position, map);

            // Register only after spawning: Hediff_Focus prunes unspawned givers.
            Hediff_Focus.GetOrAdd(caster).AddHeatGiver(construct);
        }
    }
}
