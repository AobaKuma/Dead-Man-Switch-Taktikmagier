using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Militarmagier
{
    public class CompProperties_AbilityRepel : CompProperties_AbilityEffect
    {
        public float radius = 5.9f;

        /// <summary>How long every pawn caught in the shockwave stays stunned.</summary>
        public int stunTicks = 300;

        /// <summary>
        /// Visual/audio layer only. DMST_RepelShockWave carries 0 damage, so this explosion never stuns
        /// by itself - the knockback and stun both come from <see cref="CompAbilityEffect_Repel"/>.
        /// </summary>
        public DamageDef shockwaveDamage;

        public CompProperties_AbilityRepel()
        {
            compClass = typeof(CompAbilityEffect_Repel);
        }
    }

    /// <summary>
    /// A shockwave centred on the caster: everyone nearby is knocked outward and stunned.
    /// </summary>
    public class CompAbilityEffect_Repel : CompAbilityEffect
    {
        /// <summary>
        /// StunHandler turns stun damage into ticks with <c>amount * 30</c>, so dividing stunTicks by
        /// 30 makes &lt;stunTicks&gt; mean exactly what it says.
        /// </summary>
        private const float StunTicksPerDamagePoint = 30f;

        public new CompProperties_AbilityRepel Props => (CompProperties_AbilityRepel)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Map map = caster?.Map;
            if (map == null)
            {
                return;
            }

            IntVec3 origin = caster.Position;
            float stunAmount = Props.stunTicks / StunTicksPerDamagePoint;

            if (Props.shockwaveDamage != null)
            {
                GenExplosion.DoExplosion(origin, map, Props.radius, Props.shockwaveDamage, caster,
                    damAmount: 0,
                    ignoredThings: new List<Thing> { caster });
            }

            // Collect first: Repel moves pawns, which would otherwise disturb the radial walk.
            List<Pawn> victims = GenRadial.RadialDistinctThingsAround(origin, map, Props.radius, useCenter: true)
                .OfType<Pawn>()
                .Where(p => p != caster && p.Spawned)
                .ToList();

            for (int i = 0; i < victims.Count; i++)
            {
                Repel(victims[i], origin, Props.radius, map);
                victims[i].TakeDamage(new DamageInfo(DamageDefOf.Stun, stunAmount, instigator: caster));
            }
        }

        /// <summary>
        /// Slides <paramref name="target"/> directly away from <paramref name="origin"/> until it
        /// runs out of room or reaches the shockwave's reach.
        /// </summary>
        public static void Repel(Pawn target, IntVec3 origin, float distance, Map map)
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
