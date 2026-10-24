using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Militarmagier
{
    /// <summary>
    /// Keeps a female-only pawn kind female and on one body type. The gender itself is the kind's
    /// vanilla fixedGender; this covers the two ways around it.
    ///
    /// * A generation request that names a gender overrides fixedGender. When vanilla generates the
    ///   parents of two siblings it reuses the child's kind and asks for a male father, so without this
    ///   a magier's sister produces a male "senior magier" world pawn. Such a request gets
    ///   <see cref="maleStandInKind"/> instead (<see cref="Patch_PawnGenerator_GeneratePawn"/>).
    /// * Body type normally comes from the adulthood backstory, which leaves pawns too young for one on
    ///   a random type, and Biotech body genes overwrite it after it is chosen. <see cref="bodyType"/> is
    ///   applied once the genes are in and before gear is picked (<see cref="Patch_PawnGenerator_GenerateGenes"/>).
    /// </summary>
    public class FemaleKindExtension : DefModExtension
    {
        /// <summary>Body type every pawn of this kind is generated with.</summary>
        public BodyTypeDef bodyType;

        /// <summary>Generated instead when a request explicitly asks for a male of this kind.</summary>
        public PawnKindDef maleStandInKind;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }
            if (maleStandInKind != null && maleStandInKind.HasModExtension<FemaleKindExtension>())
            {
                yield return "maleStandInKind " + maleStandInKind.defName + " is itself female-only; a male request would never be satisfied.";
            }
        }
    }
}
