using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Militarmagier
{
    /// <summary>
    /// Carries out what <see cref="Hediff_MagierSigil"/> queues, one tick later. Not saved: something
    /// queued in the very tick a save is made is simply dropped, which is harmless for a one-off rescue.
    /// RimWorld creates every GameComponent subclass for each game on its own, so nothing registers this.
    /// </summary>
    public class GameComponent_MagierSigil : GameComponent
    {
        private readonly List<Pawn> downed = new List<Pawn>();
        private readonly List<Corpse> corpses = new List<Corpse>();

        public GameComponent_MagierSigil(Game game)
        {
        }

        public static GameComponent_MagierSigil Instance => Current.Game?.GetComponent<GameComponent_MagierSigil>();

        public void QueueDowned(Pawn pawn)
        {
            if (!downed.Contains(pawn))
            {
                downed.Add(pawn);
            }
        }

        public void QueueCorpse(Corpse corpse)
        {
            if (!corpses.Contains(corpse))
            {
                corpses.Add(corpse);
            }
        }

        public override void GameComponentTick()
        {
            if (downed.Count > 0)
            {
                List<Pawn> batch = downed.ToList();
                downed.Clear();
                foreach (Pawn pawn in batch)
                {
                    RescueDowned(pawn);
                }
            }

            if (corpses.Count > 0)
            {
                List<Corpse> batch = corpses.ToList();
                corpses.Clear();
                foreach (Corpse corpse in batch)
                {
                    RecoverCorpse(corpse);
                }
            }
        }

        /// <summary>Stops the bleeding, then sends a player pawn home and makes anyone else vanish.</summary>
        private static void RescueDowned(Pawn pawn)
        {
            // Things may have moved on in the tick since: killed, carried off, already back on their feet.
            if (pawn == null || pawn.Dead || !pawn.Spawned || !pawn.Downed)
            {
                return;
            }

            Hediff_MagierSigil sigil = pawn.health.hediffSet.GetFirstHediff<Hediff_MagierSigil>();
            if (sigil == null)
            {
                return;
            }
            TendUtility.TendAllInjuries(pawn, sigil.TendQuality);

            if (pawn.Faction == Faction.OfPlayer)
            {
                if (MagierSigilUtility.TryFindSpotBesideBed(out Map map, out IntVec3 cell))
                {
                    SkipUtility.SkipTo(pawn, cell, map);
                    Messages.Message("DMST_SigilRecalled".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.NeutralEvent);
                }
                return;
            }

            Messages.Message("DMST_SigilEscaped".Translate(pawn.Named("PAWN")), new LookTargets(pawn.Position, pawn.Map), MessageTypeDefOf.NeutralEvent);
            EffecterDefOf.Skip_EntryNoDelay.Spawn(pawn, pawn.Map).Cleanup();
            // ExitMap rather than a bare despawn: it takes the pawn out of its raid lord, passes it to the
            // world and sends the faction and quest notifications, as if it had walked off the map edge.
            pawn.ExitMap(false, Rot4.Invalid);
        }

        /// <summary>A player corpse is brought home beside a bed; anyone else's vanishes with its gear.</summary>
        private static void RecoverCorpse(Corpse corpse)
        {
            if (corpse == null || corpse.Destroyed || !corpse.Spawned)
            {
                return;
            }

            Pawn pawn = corpse.InnerPawn;
            if (pawn?.Faction == Faction.OfPlayer)
            {
                if (MagierSigilUtility.TryFindSpotBesideBed(out Map map, out IntVec3 cell))
                {
                    Thing moved = SkipUtility.SkipTo(corpse, cell, map);
                    Messages.Message("DMST_SigilCorpseRecalled".Translate(pawn.Named("PAWN")), moved, MessageTypeDefOf.NeutralEvent);
                }
                return;
            }

            EffecterDefOf.Skip_EntryNoDelay.Spawn(corpse, corpse.Map).Cleanup();
            corpse.Destroy();
        }
    }

    public static class MagierSigilUtility
    {
        /// <summary>
        /// A standable cell next to a random humanlike, non-prisoner bed on any of the player's home maps.
        /// False when the player has no such bed anywhere, in which case the sigil sends nobody home.
        /// </summary>
        public static bool TryFindSpotBesideBed(out Map map, out IntVec3 cell)
        {
            List<Building_Bed> beds = new List<Building_Bed>();
            foreach (Map home in Find.Maps)
            {
                if (!home.IsPlayerHome)
                {
                    continue;
                }
                foreach (Building_Bed bed in home.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>())
                {
                    if (bed.def.building.bed_humanlike && !bed.ForPrisoners)
                    {
                        beds.Add(bed);
                    }
                }
            }

            foreach (Building_Bed bed in beds.InRandomOrder())
            {
                Map bedMap = bed.Map;
                if (CellFinder.TryFindRandomCellNear(bed.Position, bedMap, 2,
                        c => c.Standable(bedMap) && !c.Fogged(bedMap), out cell))
                {
                    map = bedMap;
                    return true;
                }
            }

            map = null;
            cell = IntVec3.Invalid;
            return false;
        }
    }
}
