using RimWorld;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace Militarmagier
{
    /// <summary>
    /// Tracks every construct (turrets and construct pawns alike) this psycaster is sustaining.
    ///
    /// The hediff itself only keeps the books. The cost is applied by
    /// <see cref="StatPart_SustainedConstructs"/>, which lowers the caster's neural heat limit by
    /// <see cref="TotalHeat"/>. A stat part rather than a stage offset because the psylink scales
    /// PsychicEntropyMax with stage *factors*: an offset here would be multiplied by the psylink level
    /// and psychic sensitivity, so a construct would cost a level 4 caster more than twice what the
    /// def says. Stat parts run after the factors, so the def value is what gets charged.
    /// </summary>
    public class Hediff_Focus : HediffWithComps
    {
        /// <summary>Pruning cadence. Constructs die rarely, so per-tick scanning buys nothing.</summary>
        private const int PruneIntervalTicks = 60;

        public List<Thing> heatGivers = new List<Thing>();

        private int pruneCounter;

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

        /// <summary>The caster's focus hediff, or null if they are sustaining nothing.</summary>
        public static Hediff_Focus Of(Pawn caster)
        {
            return caster?.health?.hediffSet?.GetFirstHediffOfDef(MilitarmagierDefOf.DMST_PsycastFocus) as Hediff_Focus;
        }

        public static Hediff_Focus GetOrAdd(Pawn caster)
        {
            return Of(caster) ?? (Hediff_Focus)caster.health.AddHediff(MilitarmagierDefOf.DMST_PsycastFocus);
        }

        /// <summary>Total heat capacity <paramref name="caster"/>'s constructs occupy; 0 if none.</summary>
        public static int SustainedHeat(Pawn caster)
        {
            return Of(caster)?.TotalHeat ?? 0;
        }

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
            NotifyChanged();
        }

        /// <summary>Drops constructs that are gone. Returns true if anything was removed.</summary>
        private bool Prune()
        {
            return heatGivers.RemoveAll(t => t == null || t.Destroyed || !t.Spawned) > 0;
        }

        /// <summary>Tells the health tracker the heat limit moved, so cached stats and the UI catch up.</summary>
        private void NotifyChanged()
        {
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
                NotifyChanged();
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
            }
        }
    }
}
