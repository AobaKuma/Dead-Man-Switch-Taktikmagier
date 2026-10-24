using RimWorld;
using Verse;

namespace Militarmagier
{
    [DefOf]
    public static class MilitarmagierDefOf
    {
        public static HediffDef DMST_PsycastFocus;

        /// <summary>The stance hediffs, not the abilities - see <see cref="MilitarmagierAbilityDefOf"/>.</summary>
        public static HediffDef DMST_QuickDraw;
        public static HediffDef DMST_SteadyAim;

        /// <summary>The ward hediff, not the ability.</summary>
        public static HediffDef DMST_Ward;

        public static HediffDef DMST_Marked;

        public static ThingDef DMST_FieldManual;

        static MilitarmagierDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MilitarmagierDefOf));
        }
    }

    /// <summary>
    /// Separate from <see cref="MilitarmagierDefOf"/> because DefOf resolves by field name, and
    /// DMST_QuickDraw, DMST_SteadyAim and DMST_Ward exist as both a HediffDef and an AbilityDef - the
    /// two cannot share a class.
    /// </summary>
    [DefOf]
    public static class MilitarmagierAbilityDefOf
    {
        public static AbilityDef DMST_Flash;
        public static AbilityDef DMST_Repel;
        public static AbilityDef DMST_EmpBurst;
        public static AbilityDef DMST_QuickDraw;
        public static AbilityDef DMST_SteadyAim;
        public static AbilityDef DMST_Mark;
        public static AbilityDef DMST_Smoke;
        public static AbilityDef DMST_FieldRepair;
        public static AbilityDef DMST_Ward;

        static MilitarmagierAbilityDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MilitarmagierAbilityDefOf));
        }
    }
}
