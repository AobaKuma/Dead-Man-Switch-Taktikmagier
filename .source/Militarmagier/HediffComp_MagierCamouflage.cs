using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Militarmagier
{
    public class HediffCompProperties_MagierCamouflage : HediffCompProperties
    {
        /// <summary>How far an enemy with perfect sight in full light spots the wearer.</summary>
        public float baseDetectRange = 10f;

        /// <summary>How long the camouflage stays down after the wearer is spotted.</summary>
        public int ticksUntilRecover = 1200;

        /// <summary>Chance a non-melee verb gives the wearer away. Melee always does.</summary>
        public float detectChanceOnCast = 0.05f;

        [NoTranslate]
        public string texPath = "UI/Ability/Camouflage";

        /// <summary>Prefix of the gizmo's _Label / _Desc / _CooldownDesc keys. FFF's, as on the jaeger.</summary>
        [NoTranslate]
        public string commandKey = "FFF_Camouflage";

        /// <summary>Every piece that must be worn for the camouflage to hold.</summary>
        public List<ThingDef> requiredApparel;

        public HediffCompProperties_MagierCamouflage()
        {
            compClass = typeof(HediffComp_MagierCamouflage);
        }
    }

    /// <summary>
    /// The DMS jaeger's camouflage (Fortified.CompCamouflage), carried by the magier cloak.
    ///
    /// The jaeger's comp is a ThingComp that casts its parent to Pawn, so it cannot sit on apparel.
    /// This is the same behaviour on the hediff the cloak gives while worn (CompCauseHediff_Apparel):
    /// a toggle that keeps the wearer hidden until an awake, non-animal enemy with line of sight comes
    /// within its sight radius (baseDetectRange scaled by light and the enemy's sight), after which the
    /// camouflage stays down for ticksUntilRecover. Melee always gives the wearer away; anything else has
    /// detectChanceOnCast. A hostile wearer being spotted is announced the way a jaeger is.
    ///
    /// Two differences from the jaeger: the camouflage only holds while every piece in requiredApparel is
    /// worn (the helmet and cloak descriptions both promise a two-piece set), and the jaeger comp's
    /// once-a-second forced attack for non-player pawns is left out - that exists for mechs, and on a
    /// humanlike it would cut into the magier AI's spellcasting.
    /// </summary>
    public class HediffComp_MagierCamouflage : HediffComp
    {
        private const int CheckIntervalTicks = 7;

        /// <summary>Same throttle as the jaeger's: one letter per real-time minute, messages in between.</summary>
        private const float LetterCooldownRealSeconds = 60f;

        private static float lastNotifiedRealTime = -99999f;

        private int lastDetectedTick = -99999;

        public bool active = true;

        [Unsaved]
        private Texture2D iconTex;

        public HediffCompProperties_MagierCamouflage Props => (HediffCompProperties_MagierCamouflage)props;

        private HediffComp_Invisibility Invisibility => parent.TryGetComp<HediffComp_Invisibility>();

        private Texture2D Icon => iconTex ??= ContentFinder<Texture2D>.Get(Props.texPath);

        private bool Recovering => Find.TickManager.TicksGame <= lastDetectedTick + Props.ticksUntilRecover;

        /// <summary>The first required piece the wearer is missing, or null if the set is complete.</summary>
        private ThingDef MissingPiece()
        {
            List<ThingDef> required = Props.requiredApparel;
            if (required.NullOrEmpty())
            {
                return null;
            }

            List<Apparel> worn = Pawn.apparel?.WornApparel;
            for (int i = 0; i < required.Count; i++)
            {
                bool wearing = false;
                if (worn != null)
                {
                    for (int j = 0; j < worn.Count; j++)
                    {
                        if (worn[j].def == required[i])
                        {
                            wearing = true;
                            break;
                        }
                    }
                }
                if (!wearing)
                {
                    return required[i];
                }
            }
            return null;
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            // HediffComp_Invisibility hides the pawn the moment the hediff is added; undo that if the cloak
            // went on without the helmet.
            if (MissingPiece() != null)
            {
                Invisibility?.BecomeVisible(instant: true);
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            Pawn pawn = Pawn;
            if (!pawn.Spawned || pawn.Downed || !pawn.IsHashIntervalTick(CheckIntervalTicks))
            {
                return;
            }

            HediffComp_Invisibility invisibility = Invisibility;
            if (invisibility == null)
            {
                return;
            }

            if (MissingPiece() != null)
            {
                invisibility.BecomeVisible();
                return;
            }
            if (!active)
            {
                return;
            }

            if (!Recovering)
            {
                CheckDetected();
            }
            if (!Recovering)
            {
                invisibility.BecomeInvisible();
            }
        }

        private void CheckDetected()
        {
            IReadOnlyList<Pawn> pawns = Pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                if (CanDetect(pawns[i]))
                {
                    Reveal();
                    return;
                }
            }
        }

        private bool CanDetect(Pawn other)
        {
            if (other == Pawn || other.Downed || !other.HostileTo(Pawn) || !other.Awake() || other.IsAnimal)
            {
                return false;
            }
            if (!Pawn.Position.InHorDistOf(other.Position, SightRadius(other)))
            {
                return false;
            }
            return GenSight.LineOfSightToThing(other.Position, Pawn, Pawn.Map);
        }

        /// <summary>The jaeger's formula: darkness cuts it to a third unless the spotter ignores darkness.</summary>
        private float SightRadius(Pawn other)
        {
            float radius = Props.baseDetectRange;
            if (other.genes == null || other.genes.AffectedByDarkness)
            {
                float glow = Pawn.Map.glowGrid.GroundGlowAt(Pawn.Position);
                radius *= Mathf.Lerp(0.33f, 1f, glow);
            }
            return radius * other.health.capacities.GetLevel(PawnCapacityDefOf.Sight);
        }

        public override void Notify_PawnUsedVerb(Verb verb, LocalTargetInfo target)
        {
            base.Notify_PawnUsedVerb(verb, target);
            if (verb.IsMeleeAttack || Rand.Chance(Props.detectChanceOnCast))
            {
                Reveal();
            }
        }

        /// <summary>Drops the camouflage, starts the recovery clock and, for a hostile wearer, warns the player.</summary>
        private void Reveal()
        {
            HediffComp_Invisibility invisibility = Invisibility;
            bool wasHidden = invisibility != null && !invisibility.PsychologicallyVisible;
            invisibility?.BecomeVisible();
            lastDetectedTick = Find.TickManager.TicksGame;

            if (wasHidden && Pawn.HostileTo(Faction.OfPlayerSilentFail))
            {
                AnnounceRevealed();
            }
        }

        private void AnnounceRevealed()
        {
            // Like the jaeger: stay quiet if another of the same kind is already in plain sight nearby, so a
            // squad being spotted one by one does not bury the player in letters.
            IReadOnlyList<Pawn> pawns = Pawn.MapHeld.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (other != Pawn && other.kindDef == Pawn.kindDef
                    && other.Position.InHorDistOf(Pawn.Position, 30f)
                    && !other.IsPsychologicallyInvisible()
                    && GenSight.LineOfSight(other.Position, Pawn.Position, other.Map))
                {
                    return;
                }
            }

            if (RealTime.LastRealTime > lastNotifiedRealTime + LetterCooldownRealSeconds)
            {
                Find.LetterStack.ReceiveLetter("DMST_MagierRevealedLabel".Translate(), "DMST_MagierRevealedDesc".Translate(Pawn.Named("PAWN")),
                    LetterDefOf.ThreatBig, Pawn);
            }
            else
            {
                Messages.Message("DMST_MagierRevealedMessage".Translate(Pawn.Named("PAWN")), Pawn, MessageTypeDefOf.ThreatBig);
            }
            lastNotifiedRealTime = RealTime.LastRealTime;
        }

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            foreach (Gizmo gizmo in base.CompGetGizmos() ?? new List<Gizmo>())
            {
                yield return gizmo;
            }

            if (Pawn.Faction != Faction.OfPlayerSilentFail)
            {
                yield break;
            }

            string desc = Props.commandKey + "_Desc";
            Command_Toggle toggle = new Command_Toggle
            {
                hotKey = KeyBindingDefOf.Command_TogglePower,
                icon = Icon,
                defaultLabel = (Props.commandKey + "_Label").Translate(),
                defaultDesc = desc.Translate() + (Recovering
                    ? (Props.commandKey + "_CooldownDesc").Translate((lastDetectedTick + Props.ticksUntilRecover - Find.TickManager.TicksGame + 1).ToStringTicksToPeriod())
                    : TaggedString.Empty),
                isActive = () => active,
                toggleAction = () =>
                {
                    active = !active;
                    if (!active)
                    {
                        Invisibility?.BecomeVisible();
                    }
                    lastDetectedTick = Find.TickManager.TicksGame;
                }
            };

            ThingDef missing = MissingPiece();
            if (missing != null)
            {
                toggle.Disable("DMST_CamouflageNeedsApparel".Translate(missing.label));
            }
            yield return toggle;
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref lastDetectedTick, "lastDetectedTick", -99999);
            Scribe_Values.Look(ref active, "active", true);
        }
    }
}
