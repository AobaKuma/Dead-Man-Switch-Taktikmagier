using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using VEF.Abilities;
using Ability = VEF.Abilities.Ability;

namespace Militarmagier
{
    /// <summary>
    /// A shockwave centred on the caster: everything nearby is knocked outward and stunned.
    /// </summary>
    public class Ability_Repel : Ability
    {
        /// <summary>
        /// StunHandler turns stun damage into ticks with <c>amount * 30</c>, so dividing the def's
        /// durationTime (in ticks) by 30 makes &lt;durationTime&gt; mean exactly what it says.
        /// </summary>
        private const float StunTicksPerDamagePoint = 30f;

        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);

            Map map = pawn?.Map;
            if (map == null)
            {
                return;
            }

            float stunAmount = GetDurationForPawn() / StunTicksPerDamagePoint;
            float radius = GetRadiusForPawn();
            IntVec3 origin = pawn.Position;

            // The explosion is the visual/audio layer only - DMST_RepelShockWave carries 0 damage, so
            // the stun below is what actually lands.
            AbilityExtension_Explosion modExtension = def.GetModExtension<AbilityExtension_Explosion>();
            if (modExtension != null)
            {
                GenExplosion.DoExplosion(origin,
                    map,
                    modExtension.explosionRadius,
                    modExtension.explosionDamageDef,
                    pawn,
                    modExtension.explosionDamageAmount,
                    modExtension.explosionArmorPenetration,
                    modExtension.explosionSound,
                    null,
                    null,
                    null,
                    modExtension.postExplosionSpawnThingDef,
                    modExtension.postExplosionSpawnChance,
                    modExtension.postExplosionSpawnThingCount,
                    modExtension.postExplosionGasType,
                    modExtension.postExplosionGasRadiusOverride,
                    modExtension.postExplosionGasAmount,
                    modExtension.applyDamageToExplosionCellsNeighbors,
                    modExtension.preExplosionSpawnThingDef,
                    modExtension.preExplosionSpawnChance,
                    modExtension.preExplosionSpawnThingCount,
                    modExtension.chanceToStartFire,
                    modExtension.damageFalloff,
                    modExtension.explosionDirection,
                    modExtension.casterImmune ? new List<Thing> { pawn } : null);
            }

            foreach (GlobalTargetInfo globalTargetInfo in targets)
            {
                if (!(globalTargetInfo.Thing is Pawn pawnTarget) || pawnTarget == pawn || !pawnTarget.Spawned)
                {
                    continue;
                }

                Repel(pawnTarget, origin, radius, map);
                pawnTarget.TakeDamage(new DamageInfo(DamageDefOf.Stun, stunAmount, instigator: pawn));
            }
        }

        /// <summary>
        /// Slides <paramref name="target"/> directly away from <paramref name="origin"/> until it
        /// runs out of room or reaches the shockwave's reach.
        /// </summary>
        public void Repel(Pawn target, IntVec3 origin, float distance, Map map)
        {
            IntVec3 targetPos = target.Position;
            if (targetPos == origin)
            {
                return;
            }

            Vector3 direction = (targetPos - origin).ToVector3().normalized;
            IntVec3 destination = origin + (direction * distance).ToIntVec3();

            // Scan outward from the *target*, not from the caster. Scanning from the caster means
            // any cover between caster and target ends the walk early, leaving `end` closer to the
            // caster than the target already was - which turned the push into a pull.
            IntVec3 end = targetPos;
            foreach (IntVec3 cell in GenSight.PointsOnLineOfSight(targetPos, destination))
            {
                if (!cell.InBounds(map) || cell.Filled(map))
                {
                    break;
                }
                end = cell;
            }

            if (end == targetPos)
            {
                return;
            }

            target.Position = end;
            target.Notify_Teleported(true, false);
        }
    }
}
