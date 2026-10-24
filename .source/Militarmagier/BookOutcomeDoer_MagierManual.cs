using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Grammar;

namespace Militarmagier
{
    public class BookOutcomeProperties_MagierManual : BookOutcomeProperties
    {
        /// <summary>
        /// Reading needed to learn the spell from a normal-quality manual, in the units Book passes to
        /// OnReadingTick (ticks x reading speed x room bonus). 20000 is eight hours at speed 1.
        /// </summary>
        public float readingToLearn = 20000f;

        /// <summary>Draw weight of each spell level, index 0 = level 1. Low levels are common.</summary>
        public List<float> levelWeights = new List<float> { 5f, 4f, 3f, 2f, 1f };

        /// <summary>Reading speed by quality, index = (int)QualityCategory (awful .. legendary).</summary>
        public List<float> qualitySpeedFactors = new List<float> { 0.6f, 0.8f, 1f, 1.15f, 1.3f, 1.45f, 1.6f };

        public override Type DoerClass => typeof(BookOutcomeDoer_MagierManual);
    }

    /// <summary>
    /// A Militärmagier field manual: teaches the one spell it covers to a reader whose psylink is high
    /// enough to cast it. Progress is kept per reader on the book, so it can be read over several
    /// sittings or put down and picked up again.
    /// </summary>
    public class BookOutcomeDoer_MagierManual : BookOutcomeDoer
    {
        private AbilityDef spell;

        private Dictionary<Pawn, float> progress = new Dictionary<Pawn, float>();

        private List<Pawn> progressKeys;
        private List<float> progressValues;

        /// <summary>
        /// Set before the book is generated (i.e. before its quality is set) to choose the spell
        /// instead of drawing one. Not saved: it only matters during generation.
        /// </summary>
        public AbilityDef fixedSpell;

        public new BookOutcomeProperties_MagierManual Props => (BookOutcomeProperties_MagierManual)props;

        public AbilityDef Spell => spell;

        private float QualitySpeedFactor
        {
            get
            {
                int index = (int)Quality;
                List<float> factors = Props.qualitySpeedFactors;
                return index >= 0 && index < factors.Count ? factors[index] : 1f;
            }
        }

        public override bool DoesProvidesOutcome(Pawn reader)
        {
            return CanLearn(reader);
        }

        private bool CanLearn(Pawn reader)
        {
            return spell != null
                && reader?.abilities != null
                && reader.abilities.GetAbility(spell) == null
                && reader.GetPsylinkLevel() >= spell.level;
        }

        public override void OnBookGenerated(Pawn author = null)
        {
            spell = fixedSpell ?? DrawSpell();
        }

        private AbilityDef DrawSpell()
        {
            List<float> weights = Props.levelWeights;
            return DefDatabase<AbilityDef>.AllDefsListForReading
                .Where(def => def.HasModExtension<MagierSpellExtension>())
                .RandomElementByWeightWithFallback(def =>
                {
                    int index = def.level - 1;
                    return index >= 0 && index < weights.Count ? weights[index] : 0f;
                });
        }

        public override void OnReadingTick(Pawn reader, float factor)
        {
            if (!CanLearn(reader))
            {
                return;
            }

            progress.TryGetValue(reader, out float done);
            done += factor * QualitySpeedFactor;
            if (done < Props.readingToLearn)
            {
                progress[reader] = done;
                return;
            }

            progress.Remove(reader);
            reader.abilities.GainAbility(spell);
            Messages.Message("DMST_ManualLearned".Translate(reader.Named("PAWN"), spell.label.Named("SPELL")),
                reader, MessageTypeDefOf.PositiveEvent);
        }

        public override string GetBenefitsString(Pawn reader = null)
        {
            if (spell == null)
            {
                return null;
            }

            float hours = Props.readingToLearn / QualitySpeedFactor / GenDate.TicksPerHour;
            return " - " + "DMST_ManualTeaches".Translate(spell.LabelCap.Named("SPELL"), spell.level.Named("LEVEL"),
                hours.ToString("0.#").Named("HOURS"));
        }

        public override IEnumerable<Dialog_InfoCard.Hyperlink> GetHyperlinks()
        {
            if (spell != null)
            {
                yield return new Dialog_InfoCard.Hyperlink(spell);
            }
        }

        /// <summary>Lets the name and description rule packs refer to the spell.</summary>
        public override IEnumerable<Rule_String> GetTopicRuleStrings()
        {
            if (spell == null)
            {
                yield break;
            }
            yield return new Rule_String("spell_label", spell.label);
            yield return new Rule_String("spell_level", spell.level.ToString());
        }

        public override void Reset()
        {
            spell = null;
            progress.Clear();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Defs.Look(ref spell, "spell");
            Scribe_Collections.Look(ref progress, "progress", LookMode.Reference, LookMode.Value, ref progressKeys, ref progressValues);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                progress ??= new Dictionary<Pawn, float>();
                // Readers that no longer exist come back as null keys.
                progress.RemoveAll(pair => pair.Key == null);
            }
        }
    }
}
