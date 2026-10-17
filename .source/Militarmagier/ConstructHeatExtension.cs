using Verse;

namespace Militarmagier
{
    /// <summary>
    /// Declares how much neural heat capacity a construct occupies while it is alive: the summoner's
    /// neural heat limit is lowered by this many points (flat, after the psylink multiplier).
    ///
    /// This lives on the construct's <see cref="ThingDef"/> rather than on the AbilityDef so that
    /// turrets and construct pawns share one source of truth. Both are tracked on
    /// <see cref="Hediff_Focus"/> and charged through <see cref="StatPart_SustainedConstructs"/>.
    /// </summary>
    public class ConstructHeatExtension : DefModExtension
    {
        public int heat = 10;

        /// <summary>Heat declared by <paramref name="def"/>, or <paramref name="fallback"/> if it declares none.</summary>
        public static int HeatFor(Def def, int fallback = 0)
        {
            ConstructHeatExtension ext = def?.GetModExtension<ConstructHeatExtension>();
            return ext?.heat ?? fallback;
        }
    }
}
