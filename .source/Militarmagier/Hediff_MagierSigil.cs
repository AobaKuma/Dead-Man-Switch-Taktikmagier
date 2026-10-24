using RimWorld;
using Verse;

namespace Militarmagier
{
    /// <summary>Tuning for <see cref="Hediff_MagierSigil"/>, on its HediffDef.</summary>
    public class MagierSigilExtension : DefModExtension
    {
        /// <summary>Chance the sigil fires when its bearer goes down. Death always fires it.</summary>
        public float downedChance = 0.8f;

        /// <summary>Quality of the field tending applied when it fires.</summary>
        public FloatRange tendQuality = new FloatRange(0.4f, 0.7f);
    }

    /// <summary>
    /// The magier sigil, a torso implant in the death acidifier's mould. When its bearer goes down it
    /// has <see cref="MagierSigilExtension.downedChance"/> to fold space around them: every wound is
    /// tended so they stop bleeding (they stay down), then a non-player pawn vanishes from the map and
    /// a player pawn appears beside a bed in the colony. When the bearer dies the body goes too: a
    /// non-player corpse vanishes with its gear, a player corpse is brought home beside a bed.
    ///
    /// Never fires for prisoners or slaves, and not when the pawn went down to surgical anaesthesia -
    /// otherwise any operation on a bearer could send them across the map, and a captured magier
    /// would escape the first time she was knocked out.
    ///
    /// The actual moves are queued on <see cref="GameComponent_MagierSigil"/> and carried out next
    /// tick: going down is noticed in the middle of the pawn's hediff tick, and a corpse is announced
    /// from inside its own SpawnSetup, and neither is a safe place to despawn or move things.
    /// </summary>
    public class Hediff_MagierSigil : Hediff_Implant
    {
        private bool wasDowned;

        /// <summary>
        /// Corpse.SpawnSetup calls Notify_PawnCorpseSpawned every time the corpse spawns - on load,
        /// after being hauled and dropped, after our own teleport. Only the first one is the death.
        /// </summary>
        private bool deathHandled;

        private MagierSigilExtension Settings => def.GetModExtension<MagierSigilExtension>() ?? DefaultSettings;

        private static readonly MagierSigilExtension DefaultSettings = new MagierSigilExtension();

        public FloatRange TendQuality => Settings.tendQuality;

        /// <summary>Prisoners and slaves are already in someone else's hands; the sigil stays quiet.</summary>
        public static bool CanFireFor(Pawn pawn)
        {
            return pawn != null && !pawn.IsPrisoner && !pawn.IsSlave;
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            // Installed by surgery, the patient is still under anaesthesia. Start from the current state so
            // the sigil does not mistake that for going down; the next real downing is what counts.
            wasDowned = pawn.Downed;
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);

            if (pawn.Dead)
            {
                return;
            }

            bool downed = pawn.Downed;
            if (downed && !wasDowned)
            {
                OnWentDown();
            }
            wasDowned = downed;
        }

        private void OnWentDown()
        {
            if (!pawn.Spawned || !CanFireFor(pawn) || pawn.health.hediffSet.HasHediff(HediffDefOf.Anesthetic))
            {
                return;
            }
            if (Rand.Chance(Settings.downedChance))
            {
                GameComponent_MagierSigil.Instance?.QueueDowned(pawn);
            }
        }

        public override void Notify_PawnCorpseSpawned()
        {
            base.Notify_PawnCorpseSpawned();

            if (deathHandled)
            {
                return;
            }
            deathHandled = true;

            Corpse corpse = pawn.Corpse;
            if (corpse != null && CanFireFor(pawn))
            {
                GameComponent_MagierSigil.Instance?.QueueCorpse(corpse);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref wasDowned, "wasDowned", false);
            Scribe_Values.Look(ref deathHandled, "deathHandled", false);
        }
    }
}
