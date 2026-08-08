using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Ability = VEF.Abilities.Ability;

namespace Militarmagier
{
    /// <summary>
    /// A directional flash: a cone of light and noise from the caster toward the target cell that
    /// blinds everything caught in it.
    /// </summary>
    public class Ability_Flash : Ability
    {
        /// <summary>
        /// StunHandler turns stun damage into ticks with <c>amount * 30</c>, so dividing the def's
        /// durationTime (in ticks) by 30 makes &lt;durationTime&gt; mean exactly what it says.
        /// </summary>
        private const float StunTicksPerDamagePoint = 30f;

        /// <summary>
        /// Reusable scratch buffer. <see cref="AffectedCells"/> refills and returns it, so the
        /// result is only valid until the next call - consume it immediately, never store it.
        /// This is deliberate: DrawHighlight runs every frame while targeting and allocating a
        /// fresh list per frame is worse than the aliasing risk.
        /// </summary>
        private readonly List<IntVec3> cachedCells = new List<IntVec3>();

        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);

            Map map = pawn?.Map;
            if (map == null || targets.Length == 0)
            {
                return;
            }

            float stunAmount = GetDurationForPawn() / StunTicksPerDamagePoint;
            List<IntVec3> cells = AffectedCells((LocalTargetInfo)targets[0]);

            for (int i = 0; i < cells.Count; i++)
            {
                FleckMaker.ThrowMicroSparks(cells[i].ToVector3Shifted(), map);

                foreach (Pawn stunPawn in cells[i].GetThingList(map).OfType<Pawn>().ToList())
                {
                    stunPawn.TakeDamage(new DamageInfo(DamageDefOf.Stun, stunAmount, instigator: pawn));
                }
            }
        }

        public override void DrawHighlight(LocalTargetInfo target)
        {
            GenDraw.DrawRadiusRing(pawn.Position, GetRangeForPawn(), def.rangeRingColor);
            GenDraw.DrawFieldEdges(AffectedCells(target));
        }

        public List<IntVec3> AffectedCells(LocalTargetInfo target)
        {
            cachedCells.Clear();

            Map map = pawn?.Map;
            if (map == null)
            {
                return cachedCells;
            }

            float range = GetRangeForPawn();
            float radius = GetRadiusForPawn();
            Vector3 vector = pawn.Position.ToVector3Shifted().Yto0();
            IntVec3 intVec = target.Cell.ClampInsideMap(map);
            if (pawn.Position == intVec)
            {
                return cachedCells;
            }

            float lengthHorizontal = (intVec - pawn.Position).LengthHorizontal;
            float num = (intVec.x - pawn.Position.x) / lengthHorizontal;
            float num2 = (intVec.z - pawn.Position.z) / lengthHorizontal;
            intVec.x = Mathf.RoundToInt(pawn.Position.x + num * range);
            intVec.z = Mathf.RoundToInt(pawn.Position.z + num2 * range);

            float target2 = Vector3.SignedAngle(intVec.ToVector3Shifted().Yto0() - vector, Vector3.right, Vector3.up);
            float num3 = radius / 2f;
            float num4 = Mathf.Sqrt(Mathf.Pow((intVec - pawn.Position).LengthHorizontal, 2f) + Mathf.Pow(num3, 2f));
            float num5 = 57.29578f * Mathf.Asin(num3 / num4);

            int num6 = GenRadial.NumCellsInRadius(range);
            for (int i = 0; i < num6; i++)
            {
                IntVec3 intVec2 = pawn.Position + GenRadial.RadialPattern[i];
                if (CanUseCell(intVec2)
                    && Mathf.Abs(Mathf.DeltaAngle(Vector3.SignedAngle(intVec2.ToVector3Shifted().Yto0() - vector, Vector3.right, Vector3.up), target2)) <= num5)
                {
                    cachedCells.Add(intVec2);
                }
            }

            List<IntVec3> list = GenSight.BresenhamCellsBetween(pawn.Position, intVec);
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
                if (!c.InBounds(map) || c == pawn.Position || !c.InHorDistOf(pawn.Position, range))
                {
                    return false;
                }
                // verb is null while the ability is being previewed outside a live cast.
                if (verb == null)
                {
                    return GenSight.LineOfSight(pawn.Position, c, map, skipFirstCell: true);
                }
                return verb.TryFindShootLineFromTo(pawn.Position, c, out _);
            }
        }
    }
}
