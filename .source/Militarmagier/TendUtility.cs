using System.Collections.Generic;
using Verse;

namespace Militarmagier
{
    public static class TendUtility
    {
        /// <summary>
        /// Tends every injury and missing part on <paramref name="pawn"/> that can be tended right now,
        /// which stops their bleeding. Returns how many were tended.
        /// </summary>
        public static int TendAllInjuries(Pawn pawn, FloatRange quality)
        {
            int tended = 0;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                if ((hediffs[i] is Hediff_Injury || hediffs[i] is Hediff_MissingPart) && hediffs[i].TendableNow())
                {
                    hediffs[i].Tended(quality.RandomInRange, quality.TrueMax, 1);
                    tended++;
                }
            }
            return tended;
        }
    }
}
