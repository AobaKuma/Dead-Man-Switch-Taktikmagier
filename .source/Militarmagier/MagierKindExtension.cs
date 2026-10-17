using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Militarmagier
{
    /// <summary>
    /// Gives a generated pawn a psylink and a random set of Militärmagier spells. Replaces VPE's
    /// PawnKindAbilityExtension_Psycasts; applied by <see cref="Patch_PawnGenerator_GeneratePawn"/>.
    /// </summary>
    public class MagierKindExtension : DefModExtension
    {
        /// <summary>Psylink level the pawn is brought up to. Also caps which spells it can cast.</summary>
        public int psylinkLevel = 1;

        /// <summary>Spell levels to draw from. Draws above <see cref="psylinkLevel"/> are dropped.</summary>
        public IntRange spellLevelRange = new IntRange(1, 1);

        /// <summary>How many spells to draw; capped by how many qualify.</summary>
        public IntRange spellCountRange = new IntRange(1, 1);

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }
            if (spellLevelRange.max > psylinkLevel)
            {
                yield return "spellLevelRange max " + spellLevelRange.max + " is above psylinkLevel "
                    + psylinkLevel + "; those spells could be learned but never cast.";
            }
        }

        public void Apply(Pawn pawn)
        {
            if (pawn?.abilities == null || pawn.health?.hediffSet?.GetBrain() == null)
            {
                return;
            }

            GivePsylink(pawn);

            List<AbilityDef> candidates = DefDatabase<AbilityDef>.AllDefsListForReading
                .Where(def => def.HasModExtension<MagierSpellExtension>()
                    && def.level >= spellLevelRange.min
                    && def.level <= spellLevelRange.max
                    && def.level <= psylinkLevel
                    && pawn.abilities.GetAbility(def) == null)
                .ToList();

            int count = spellCountRange.RandomInRange;
            foreach (AbilityDef def in candidates.InRandomOrder().Take(count))
            {
                pawn.abilities.GainAbility(def);
            }
        }

        /// <summary>
        /// Brings the pawn's psylink up to <see cref="psylinkLevel"/> without vanilla handing out a
        /// random psycast for every level gained - an enemy magier's kit is this path only.
        /// </summary>
        private void GivePsylink(Pawn pawn)
        {
            int current = pawn.GetPsylinkLevel();
            if (current >= psylinkLevel)
            {
                return;
            }

            Patch_Hediff_Psylink_TryGiveAbilityOfLevel.suppress = true;
            try
            {
                if (current == 0)
                {
                    // ChangePsylinkLevel on a pawn with no psylink installs level 1 and ignores the offset.
                    pawn.ChangePsylinkLevel(1, sendLetter: false);
                    current = 1;
                }
                if (psylinkLevel > current)
                {
                    pawn.ChangePsylinkLevel(psylinkLevel - current, sendLetter: false);
                }
            }
            finally
            {
                Patch_Hediff_Psylink_TryGiveAbilityOfLevel.suppress = false;
            }
        }
    }
}
