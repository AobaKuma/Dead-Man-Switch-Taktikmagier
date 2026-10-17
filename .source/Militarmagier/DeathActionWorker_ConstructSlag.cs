using RimWorld;
using Verse;
using Verse.AI.Group;

namespace Militarmagier
{
    /// <summary>
    /// A construct pawn does not leave a body: it collapses into a steel slag chunk. Replaces VPE's
    /// DeathActionWorker_SlagChunk. Anything it was carrying or wearing is dropped first, since
    /// destroying the corpse would otherwise destroy it too.
    /// </summary>
    public class DeathActionWorker_ConstructSlag : DeathActionWorker
    {
        public override void PawnDied(Corpse corpse, Lord prevLord)
        {
            if (corpse == null || corpse.Destroyed || !corpse.Spawned)
            {
                return;
            }

            IntVec3 position = corpse.Position;
            Map map = corpse.Map;
            Pawn inner = corpse.InnerPawn;

            inner?.equipment?.DropAllEquipment(position, forbid: false);
            inner?.apparel?.DropAll(position, forbid: false);

            corpse.Destroy();
            GenPlace.TryPlaceThing(ThingMaker.MakeThing(ThingDefOf.ChunkSlagSteel), position, map, ThingPlaceMode.Near);
        }
    }
}
