using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Militarmagier
{
    /// <summary>
    /// Applies this assembly's Harmony patches as soon as the mod is constructed. That is before defs
    /// load, which <see cref="Patch_ThingDefGenerator_Neurotrainer_ImpliedThingDefs"/> depends on:
    /// implied defs are generated during def loading, long before StaticConstructorOnStartup runs.
    /// </summary>
    public class TaktikmagierMod : Mod
    {
        public TaktikmagierMod(ModContentPack content) : base(content)
        {
            new Harmony("Aoba.DeadManSwitch.Taktikmagier").PatchAll(typeof(TaktikmagierMod).Assembly);
        }
    }

    /// <summary>Applies <see cref="MagierKindExtension"/> to every freshly generated pawn whose kind carries one.</summary>
    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new Type[] { typeof(PawnGenerationRequest) })]
    public static class Patch_PawnGenerator_GeneratePawn
    {
        public static void Postfix(Pawn __result)
        {
            __result?.kindDef?.GetModExtension<MagierKindExtension>()?.Apply(__result);
        }
    }

    /// <summary>
    /// The random psycast vanilla hands out for each psylink level, minus the Militärmagier spells:
    /// those are learned from field manuals only (proposal D3). Magier spells are also ignored when
    /// checking whether the pawn already knows a psycast of that level, so learning one from a manual
    /// does not cost a magier their vanilla psycast.
    ///
    /// Otherwise this is vanilla's Hediff_Psylink.TryGiveAbilityOfLevel; keep it in step if that
    /// changes. <see cref="suppress"/> skips it entirely, for <see cref="MagierKindExtension"/>.
    /// </summary>
    [HarmonyPatch(typeof(Hediff_Psylink), nameof(Hediff_Psylink.TryGiveAbilityOfLevel))]
    public static class Patch_Hediff_Psylink_TryGiveAbilityOfLevel
    {
        /// <summary>Per thread as a precaution, so the flag can only ever affect the code that set it.</summary>
        [ThreadStatic]
        public static bool suppress;

        public static bool Prefix(Hediff_Psylink __instance, int abilityLevel, bool sendLetter)
        {
            if (suppress)
            {
                return false;
            }

            Pawn pawn = __instance.pawn;
            string text;
            if (!pawn.abilities.abilities.Any(a => IsVanillaPoolPsycast(a.def) && a.def.level == abilityLevel))
            {
                AbilityDef gained = DefDatabase<AbilityDef>.AllDefs
                    .Where(def => IsVanillaPoolPsycast(def) && def.level == abilityLevel)
                    .RandomElementWithFallback();
                if (gained == null)
                {
                    // Nothing left to give at this level once magier spells are excluded; let vanilla decide.
                    return true;
                }
                pawn.abilities.GainAbility(gained);
                text = Hediff_Psylink.MakeLetterTextNewPsylinkLevel(pawn, abilityLevel, Gen.YieldSingle(gained));
            }
            else
            {
                text = Hediff_Psylink.MakeLetterTextNewPsylinkLevel(pawn, abilityLevel);
            }

            if (sendLetter && PawnUtility.ShouldSendNotificationAbout(pawn) && !pawn.IsDuplicate)
            {
                string label = "LetterLabelPsylinkLevelGained".Translate() + ": " + pawn.LabelShortCap;
                Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.PositiveEvent, pawn);
            }
            return false;
        }

        private static bool IsVanillaPoolPsycast(AbilityDef def)
        {
            return def.IsPsycast && !def.HasModExtension<MagierSpellExtension>();
        }
    }

    /// <summary>
    /// No psytrainers for Militärmagier spells (proposal D3): field manuals are the way to learn them.
    /// Vanilla generates one psytrainer per psycast during def loading.
    /// </summary>
    [HarmonyPatch(typeof(ThingDefGenerator_Neurotrainer), nameof(ThingDefGenerator_Neurotrainer.ImpliedThingDefs))]
    public static class Patch_ThingDefGenerator_Neurotrainer_ImpliedThingDefs
    {
        public static void Postfix(ref IEnumerable<ThingDef> __result)
        {
            __result = __result.Where(def => !TeachesMagierSpell(def));
        }

        private static bool TeachesMagierSpell(ThingDef def)
        {
            if (def.comps == null)
            {
                return false;
            }
            for (int i = 0; i < def.comps.Count; i++)
            {
                if (def.comps[i] is CompProperties_UseEffect_GainAbility gain
                    && gain.ability != null
                    && gain.ability.HasModExtension<MagierSpellExtension>())
                {
                    return true;
                }
            }
            return false;
        }
    }
}
