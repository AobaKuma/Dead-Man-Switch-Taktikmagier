using Fortified;
using RimWorld;
using Verse;
using Verse.Sound;

namespace Militarmagier
{
    public class HediffCompProperties_PsychicWard : HediffCompProperties
    {
        /// <summary>Damage the ward soaks up before it breaks.</summary>
        public float hitpoints = 60f;

        public HediffCompProperties_PsychicWard()
        {
            compClass = typeof(HediffComp_PsychicWard);
        }
    }

    /// <summary>
    /// A temporary shield laid over a pawn by the ward spell. Absorbs harmful damage until its pool
    /// is spent, then removes its hediff.
    ///
    /// Built on FFF's pre-apply-damage hook rather than FFF's own HediffComp_ProtectiveShield: that
    /// one is made for installed attachments (it scales with body size, merges with other
    /// attachments and announces "addon broken" when it runs out), none of which fits a spell.
    /// </summary>
    public class HediffComp_PsychicWard : HediffComp_PreApplyDamage
    {
        private float hitpoints = -1f;

        public HediffCompProperties_PsychicWard Props => (HediffCompProperties_PsychicWard)props;

        public override string CompLabelInBracketsExtra => hitpoints.ToString("F0");

        public override void CompPostMake()
        {
            base.CompPostMake();
            hitpoints = Props.hitpoints;
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            if (dinfo.Def == null || !dinfo.Def.harmsHealth || hitpoints <= 0f)
            {
                return;
            }

            float amount = dinfo.Amount;
            if (amount <= hitpoints)
            {
                hitpoints -= amount;
                absorbed = true;
            }
            else
            {
                dinfo.SetAmount(amount - hitpoints);
                hitpoints = 0f;
            }

            Pawn pawn = Pawn;
            if (pawn.MapHeld != null)
            {
                SoundDefOf.EnergyShield_AbsorbDamage.PlayOneShot(new TargetInfo(pawn.PositionHeld, pawn.MapHeld));
                FleckMaker.ThrowMicroSparks(pawn.DrawPos, pawn.MapHeld);
            }

            if (hitpoints <= 0f)
            {
                pawn.health.RemoveHediff(parent);
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref hitpoints, "hitpoints", -1f);
        }
    }
}
