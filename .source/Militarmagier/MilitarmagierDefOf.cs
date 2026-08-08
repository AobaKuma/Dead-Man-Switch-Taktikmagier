using RimWorld;
using Verse;

namespace Militarmagier
{
    [DefOf]
    public static class MilitarmagierDefOf
    {
        public static HediffDef DMST_PsycastFocus;

        /// <summary>The buff hediff, not the ability - see <see cref="MilitarmagierAbilityDefOf"/>.</summary>
        public static HediffDef DMST_QuickDraw;

        // VPE and VEF are hard dependencies (see About.xml), so no MayRequire guards here - if
        // these are missing something is wrong and we want the DefOf error rather than a silent
        // null that only shows up later as an NRE.
        public static StatDef VPE_PsychicEntropyMinimum;

        /// <summary>VEF's generic "cast an ability" job, used when an AbilityDef declares no jobDef.</summary>
        public static JobDef VFEA_UseAbility;

        static MilitarmagierDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MilitarmagierDefOf));
        }
    }

    /// <summary>
    /// Separate from <see cref="MilitarmagierDefOf"/> because DefOf resolves by field name, and
    /// DMST_QuickDraw exists as both a HediffDef and an AbilityDef - the two cannot share a class.
    /// </summary>
    [DefOf]
    public static class MilitarmagierAbilityDefOf
    {
        public static VEF.Abilities.AbilityDef DMST_Flash;
        public static VEF.Abilities.AbilityDef DMST_Repel;
        public static VEF.Abilities.AbilityDef DMST_EmpBurst;
        public static VEF.Abilities.AbilityDef DMST_QuickDraw;

        static MilitarmagierAbilityDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MilitarmagierAbilityDefOf));
        }
    }
}
