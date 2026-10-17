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
        public static AbilityDef DMST_Flash;
        public static AbilityDef DMST_Repel;
        public static AbilityDef DMST_EmpBurst;
        public static AbilityDef DMST_QuickDraw;

        static MilitarmagierAbilityDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MilitarmagierAbilityDefOf));
        }
    }
}
