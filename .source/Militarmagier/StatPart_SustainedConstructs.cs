using RimWorld;
using Verse;

namespace Militarmagier
{
    /// <summary>
    /// Lowers PsychicEntropyMax by the heat capacity the pawn's live constructs occupy. Added to the
    /// stat by 1.7/Patches/Stat_PsychicEntropyMax.xml. See <see cref="Hediff_Focus"/> for why this is a
    /// stat part rather than a hediff stage offset.
    /// </summary>
    public class StatPart_SustainedConstructs : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.Thing is Pawn pawn)
            {
                val -= Hediff_Focus.SustainedHeat(pawn);
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (req.Thing is Pawn pawn)
            {
                int heat = Hediff_Focus.SustainedHeat(pawn);
                if (heat > 0)
                {
                    return "DMST_SustainedConstructsStat".Translate() + ": -" + heat;
                }
            }
            return null;
        }
    }
}
