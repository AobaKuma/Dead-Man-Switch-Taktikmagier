using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Militarmagier
{
    public class CompProperties_MagierLink : CompProperties
    {
        [NoTranslate]
        public string gizmoIconPath = "UI/Gizmos/DMST_BreakLink";

        [Unsaved]
        private Texture2D cachedIcon;

        /// <summary>
        /// The break-link icon. Falls back to the vanilla cancel icon until the mod ships its own
        /// texture, so a missing file never shows up as the pink error square.
        /// </summary>
        public Texture2D Icon
        {
            get
            {
                if (cachedIcon == null)
                {
                    cachedIcon = ContentFinder<Texture2D>.Get(gizmoIconPath, reportFailure: false)
                        ?? ContentFinder<Texture2D>.Get("UI/Designators/Cancel", reportFailure: false)
                        ?? BaseContent.BadTex;
                }
                return cachedIcon;
            }
        }

        public CompProperties_MagierLink()
        {
            compClass = typeof(CompMagierLink);
        }
    }

    /// <summary>
    /// Ties a construct to the magier who assembled it, replacing VPE's CompBreakLink.
    ///
    /// The construct falls apart when its caster dies, is downed or ends up on a different map, and
    /// the player can break the link by hand. Upkeep is not handled here - see <see cref="Hediff_Focus"/>.
    /// </summary>
    public class CompMagierLink : ThingComp
    {
        private const int CheckIntervalTicks = 60;

        private Pawn caster;

        public CompProperties_MagierLink Props => (CompProperties_MagierLink)props;

        public Pawn Caster => caster;

        /// <summary>Links <paramref name="construct"/> to <paramref name="summoner"/>. Call before spawning.</summary>
        public static void LinkTo(Thing construct, Pawn summoner)
        {
            CompMagierLink link = construct.TryGetComp<CompMagierLink>();
            if (link != null)
            {
                link.caster = summoner;
                return;
            }

            Log.ErrorOnce(
                "[Taktikmagier] " + construct.def.defName + " is missing CompProperties_MagierLink; "
                + "it will not be tied to its summoner and will never be cleaned up.",
                construct.def.shortHash);
        }

        private bool ShouldDissolve
        {
            get
            {
                if (caster == null || caster.Dead || caster.Downed)
                {
                    return true;
                }
                return caster.MapHeld != parent.MapHeld;
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            if (parent.Spawned && parent.IsHashIntervalTick(CheckIntervalTicks) && ShouldDissolve)
            {
                Dissolve();
            }
        }

        /// <summary>
        /// Construct pawns are killed so their death action turns them into slag. Turrets vanish
        /// rather than being killed: a killed building leaves part of its cost behind, which would
        /// let a magier turn one component into steel by summoning and immediately breaking the link.
        /// </summary>
        public void Dissolve()
        {
            if (parent.Destroyed)
            {
                return;
            }

            if (parent is Pawn pawn)
            {
                if (!pawn.Dead)
                {
                    pawn.Kill(null);
                }
                return;
            }

            if (parent.Spawned)
            {
                FleckMaker.ThrowMicroSparks(parent.DrawPos, parent.Map);
            }
            parent.Destroy(DestroyMode.Vanish);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (parent.Faction != Faction.OfPlayer || caster == null)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "DMST_BreakLink".Translate(),
                defaultDesc = "DMST_BreakLinkDesc".Translate(caster.Named("CASTER")),
                icon = Props.Icon,
                action = Dissolve
            };
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref caster, "caster");
        }
    }
}
