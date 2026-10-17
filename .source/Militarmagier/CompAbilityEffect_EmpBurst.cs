using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Militarmagier
{
    public class CompProperties_AbilityEmpBurst : CompProperties_AbilityEffect
    {
        public float radius = 14.9f;
        public DamageDef damageDef;
        public int damageAmount = 40;

        public CompProperties_AbilityEmpBurst()
        {
            compClass = typeof(CompAbilityEffect_EmpBurst);
        }
    }

    /// <summary>
    /// An EMP burst centred on the caster that leaves the caster out.
    ///
    /// Vanilla's CompAbilityEffect_Explosion cannot do this: it always detonates on the caster and
    /// passes no ignore list, so the caster's own shield belt or power armour would eat the pulse.
    /// Everyone else in range, friend or foe, is hit - telling friend from foe is meant to be the
    /// spell's refinement reward later on, not the default.
    /// </summary>
    public class CompAbilityEffect_EmpBurst : CompAbilityEffect
    {
        public new CompProperties_AbilityEmpBurst Props => (CompProperties_AbilityEmpBurst)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            if (caster?.Map == null || Props.damageDef == null)
            {
                return;
            }

            GenExplosion.DoExplosion(caster.Position, caster.Map, Props.radius, Props.damageDef, caster,
                damAmount: Props.damageAmount,
                ignoredThings: new List<Thing> { caster });
        }
    }
}
