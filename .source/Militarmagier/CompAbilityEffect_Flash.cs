using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Militarmagier
{
    public class CompProperties_AbilityFlash : CompProperties_AbilityEffect
    {
        /// <summary>Width of the cone at its far end, in cells. The cone reaches as far as the verb range.</summary>
        public float coneWidth = 12f;

        /// <summary>How long everything caught in the cone stays stunned.</summary>
        public int stunTicks = 300;

        public CompProperties_AbilityFlash()
        {
            compClass = typeof(CompAbilityEffect_Flash);
        }
    }

    /// <summary>
    /// A directional flash: a cone of light and noise from the caster toward the target cell that
    /// blinds everything caught in it.
    /// </summary>
    public class CompAbilityEffect_Flash : CompAbilityEffect
    {
        /// <summary>
        /// StunHandler turns stun damage into ticks with <c>amount * 30</c>, so dividing stunTicks by
        /// 30 makes &lt;stunTicks&gt; mean exactly what it says.
        /// </summary>
        private const float StunTicksPerDamagePoint = 30f;

        /// <summary>
        /// Reusable scratch buffer. <see cref="AffectedCells"/> refills and returns it, so the
        /// result is only valid until the next call - consume it immediately, never store it.
        /// This is deliberate: DrawEffectPreview runs every frame while targeting and allocating a
        /// fresh list per frame is worse than the aliasing risk.
        /// </summary>
        private readonly List<IntVec3> cachedCells = new List<IntVec3>();

        public new CompProperties_AbilityFlash Props => (CompProperties_AbilityFlash)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Map map = caster?.Map;
            if (map == null)
            {
                return;
            }

            float stunAmount = Props.stunTicks / StunTicksPerDamagePoint;
            List<IntVec3> cells = AffectedCells(target);

            for (int i = 0; i < cells.Count; i++)
            {
                FleckMaker.ThrowMicroSparks(cells[i].ToVector3Shifted(), map);

                foreach (Pawn stunPawn in cells[i].GetThingList(map).OfType<Pawn>().ToList())
                {
                    stunPawn.TakeDamage(new DamageInfo(DamageDefOf.Stun, stunAmount, instigator: caster));
                }
            }
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            GenDraw.DrawFieldEdges(AffectedCells(target));
        }

        public List<IntVec3> AffectedCells(LocalTargetInfo target)
        {
            cachedCells.Clear();

            Pawn caster = parent.pawn;
            Map map = caster?.Map;
            if (map == null || !target.IsValid)
            {
                return cachedCells;
            }

            float range = parent.verb.verbProps.range;
            float radius = Props.coneWidth;
            Vector3 vector = caster.Position.ToVector3Shifted().Yto0();
            IntVec3 intVec = target.Cell.ClampInsideMap(map);
            if (caster.Position == intVec)
            {
                return cachedCells;
            }

            float lengthHorizontal = (intVec - caster.Position).LengthHorizontal;
            float num = (intVec.x - caster.Position.x) / lengthHorizontal;
            float num2 = (intVec.z - caster.Position.z) / lengthHorizontal;
            intVec.x = Mathf.RoundToInt(caster.Position.x + num * range);
            intVec.z = Mathf.RoundToInt(caster.Position.z + num2 * range);

            float target2 = Vector3.SignedAngle(intVec.ToVector3Shifted().Yto0() - vector, Vector3.right, Vector3.up);
            float num3 = radius / 2f;
            float num4 = Mathf.Sqrt(Mathf.Pow((intVec - caster.Position).LengthHorizontal, 2f) + Mathf.Pow(num3, 2f));
            float num5 = 57.29578f * Mathf.Asin(num3 / num4);

            int num6 = GenRadial.NumCellsInRadius(range);
            for (int i = 0; i < num6; i++)
            {
                IntVec3 intVec2 = caster.Position + GenRadial.RadialPattern[i];
                if (CanUseCell(intVec2)
                    && Mathf.Abs(Mathf.DeltaAngle(Vector3.SignedAngle(intVec2.ToVector3Shifted().Yto0() - vector, Vector3.right, Vector3.up), target2)) <= num5)
                {
                    cachedCells.Add(intVec2);
                }
            }

            List<IntVec3> list = GenSight.BresenhamCellsBetween(caster.Position, intVec);
            for (int j = 0; j < list.Count; j++)
            {
                IntVec3 intVec3 = list[j];
                if (!cachedCells.Contains(intVec3) && CanUseCell(intVec3))
                {
                    cachedCells.Add(intVec3);
                }
            }

            return cachedCells;

            bool CanUseCell(IntVec3 c)
            {
                if (!c.InBounds(map) || c == caster.Position || !c.InHorDistOf(caster.Position, range))
                {
                    return false;
                }
                return parent.verb.TryFindShootLineFromTo(caster.Position, c, out _, ignoreRange: true);
            }
        }
    }
}
