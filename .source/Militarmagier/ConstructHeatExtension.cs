using Verse;

namespace Militarmagier
{
    /// <summary>
    /// Declares how much minimum psychic entropy ("heat") a construct costs its summoner while
    /// it is alive.
    ///
    /// This lives on the construct's <see cref="ThingDef"/> rather than on the AbilityDef so that
    /// turrets and construct pawns share one source of truth: turrets are charged through
    /// <see cref="Hediff_Focus"/>, construct pawns through VPE's own
    /// <c>IMinHeatGiver</c> registry, and both read the value from here.
    /// </summary>
    public class ConstructHeatExtension : DefModExtension
    {
        public int heat = 50;

        /// <summary>Heat declared by <paramref name="def"/>, or <paramref name="fallback"/> if it declares none.</summary>
        public static int HeatFor(Def def, int fallback = 0)
        {
            ConstructHeatExtension ext = def?.GetModExtension<ConstructHeatExtension>();
            return ext?.heat ?? fallback;
        }
    }
}
