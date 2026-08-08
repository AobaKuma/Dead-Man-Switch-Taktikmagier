using RimWorld;
using System.Collections.Generic;
using System.Text;
using System;
using Verse;

namespace Militarmagier
{
    /// <summary>
    /// Tracks the non-pawn constructs (turrets) this psycaster is sustaining and converts them
    /// into a minimum psychic entropy floor.
    ///
    /// Construct *pawns* deliberately do NOT go through this hediff. VPE's
    /// <c>Pawn_Construct</c> already implements <c>IMinHeatGiver</c> and
    /// <c>CompBreakLink.PostSpawnSetup</c> registers it with the caster's
    /// <c>Hediff_PsycastAbilities</c>; routing them through here as well would apply the same
    /// stat offset twice from two different hediffs. See
    /// <see cref="AbilityExtension_ConstructPawn"/>.
    /// </summary>
    public class Hediff_Focus : HediffWithComps
    {
        /// <summary>Pruning cadence. Constructs die rarely, so per-tick scanning buys nothing.</summary>
        private const int PruneIntervalTicks = 60;

        public List<Thing> heatGivers = new List<Thing>();

        private HediffStage cachedStage;
        private int pruneCounter;

        public override HediffStage CurStage
        {
            get
            {
                if (cachedStage == null)
                {
                    RecacheCurStage();
                }
                return cachedStage;
            }
        }

        /// <summary>
        /// The hediff exists only to represent live constructs; with none left it is noise.
        /// Deliberately does NOT fall through to base, whose rule is <c>Severity &lt;= 0</c> - this
        /// hediff never touches severity and must not have its lifetime tied to it.
        /// </summary>
        public override bool ShouldRemove => heatGivers.Count == 0;

        public int TotalHeat
        {
            get
            {
                int total = 0;
                for (int i = 0; i < heatGivers.Count; i++)
                {
                    total += ConstructHeatExtension.HeatFor(heatGivers[i]?.def);
                }
                return total;
            }
        }

        public override string Description => base.Description + CostBreakdown();

        private string CostBreakdown()
        {
            if (heatGivers.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine();
            for (int i = 0; i < heatGivers.Count; i++)
            {
                Thing thing = heatGivers[i];
                if (thing == null)
                {
                    continue;
                }
                sb.AppendLine(thing.LabelCap + ": " + ConstructHeatExtension.HeatFor(thing.def));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Registers a live construct. Call this only once the construct is spawned, otherwise the
        /// next prune pass will drop it again.
        /// </summary>
        public void AddHeatGiver(Thing thing)
        {
            if (thing == null || heatGivers.Contains(thing))
            {
                return;
            }
            heatGivers.Add(thing);
            RecacheCurStage();
        }

        /// <summary>Drops constructs that are gone. Returns true if anything was removed.</summary>
        private bool Prune()
        {
            return heatGivers.RemoveAll(t => t == null || t.Destroyed || !t.Spawned) > 0;
        }

        public void RecacheCurStage()
        {
            cachedStage = new HediffStage
            {
                statOffsets = new List<StatModifier>
                {
                    new StatModifier
                    {
                        stat = MilitarmagierDefOf.VPE_PsychicEntropyMinimum,
                        value = TotalHeat
                    }
                }
            };

            if (pawn != null && pawn.Spawned)
            {
                pawn.health.Notify_HediffChanged(this);
            }
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);

            pruneCounter += delta;
            if (pruneCounter < PruneIntervalTicks)
            {
                return;
            }
            pruneCounter = 0;

            if (Prune())
            {
                RecacheCurStage();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref heatGivers, "heatGivers", LookMode.Reference);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                heatGivers ??= new List<Thing>();
                // Constructs destroyed before the save cannot be resolved and come back as nulls.
                heatGivers.RemoveAll(t => t == null || t.Destroyed);
                RecacheCurStage();
            }
        }
    }
}
