using RimWorld;
using Verse;
using Verse.AI;
using VanillaPsycastsExpanded;
using VanillaPsycastsExpanded.Technomancer;
using Fortified;

namespace Militarmagier
{
    /// <summary>
    /// A VPE construct pawn that can be ordered to pick up weapons and armour.
    /// </summary>
    public class Pawn_ConstructWeaponUsable : Pawn_Construct, IMinHeatGiver, IWeaponUsable
    {
        /// <summary>
        /// VPE's <c>Pawn_Construct.MinHeat</c> is a hard-coded, non-virtual <c>=&gt; 20</c>, so it
        /// cannot be overridden normally. Re-listing <c>IMinHeatGiver</c> on this class makes C#
        /// rebuild the interface map against the most derived members, and VPE only ever reads
        /// the value through the interface (<c>minHeatGivers.Sum(g =&gt; g.MinHeat)</c>), so this
        /// is what actually gets charged. Falls back to VPE's 20 if the def declares no
        /// <see cref="ConstructHeatExtension"/>.
        /// </summary>
        public new int MinHeat => ConstructHeatExtension.HeatFor(def, 20);

        void IWeaponUsable.Equip(ThingWithComps equipment)
        {
            equipment.SetForbidden(false);
            jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Equip, equipment), JobTag.DraftedOrder);
        }

        void IWeaponUsable.Wear(ThingWithComps apparel)
        {
            apparel.SetForbidden(false);
            jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Wear, apparel), JobTag.DraftedOrder);
        }
    }
}
