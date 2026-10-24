using RimWorld;
using Verse;

namespace Militarmagier
{
    public class CompProperties_AbilitySmoke : CompProperties_AbilityEffect
    {
        public float radius = 3.9f;

        /// <summary>The burst that releases the gas; vanilla's harmless Smoke damage.</summary>
        public DamageDef damageDef;

        public GasType gasType = GasType.BlindSmoke;

        public CompProperties_AbilitySmoke()
        {
            compClass = typeof(CompAbilityEffect_Smoke);
        }
    }

    /// <summary>
    /// Fills an area around the target cell with smoke, like a smoke grenade landing there.
    /// Vanilla's CompAbilityEffect_Explosion cannot be used: it always detonates on the caster.
    /// </summary>
    public class CompAbilityEffect_Smoke : CompAbilityEffect
    {
        public new CompProperties_AbilitySmoke Props => (CompProperties_AbilitySmoke)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Map map = caster?.Map;
            if (map == null || Props.damageDef == null)
            {
                return;
            }

            GenExplosion.DoExplosion(target.Cell, map, Props.radius, Props.damageDef, caster,
                postExplosionGasType: Props.gasType);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            GenDraw.DrawRadiusRing(target.Cell, Props.radius);
        }
    }
}
