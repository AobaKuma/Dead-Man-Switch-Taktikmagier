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

    /// <summary>
    /// A request for a male of a female-only kind (<see cref="FemaleKindExtension"/>) is turned into a
    /// request for its stand-in kind. Done before world-pawn reuse is considered, so the stand-in is
    /// what gets looked up there too.
    /// </summary>
    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new Type[] { typeof(PawnGenerationRequest) })]
    public static class Patch_PawnGenerator_GeneratePawn
    {
        public static void Prefix(ref PawnGenerationRequest request)
        {
            if (request.FixedGender != Gender.Male)
            {
                return;
            }

            PawnKindDef standIn = request.KindDef?.GetModExtension<FemaleKindExtension>()?.maleStandInKind;
            if (standIn != null)
            {
                // KindDef reads through PawnKindDefGetter when one is set, so clear it or the swap is ignored.
                request.PawnKindDefGetter = null;
                request.KindDef = standIn;
            }
        }
    }

    /// <summary>
    /// Applies <see cref="MagierKindExtension"/> to freshly generated pawns only.
    ///
    /// Not on GeneratePawn: that also hands back existing world pawns it reuses, and applying the kit
    /// to those again would stack another round of spells and manual rolls on the same magier every
    /// time she is reused.
    /// </summary>
    [HarmonyPatch(typeof(PawnGenerator), "GenerateNewPawnInternal")]
    public static class Patch_PawnGenerator_GenerateNewPawnInternal
    {
        public static void Postfix(Pawn __result)
        {
            __result?.kindDef?.GetModExtension<MagierKindExtension>()?.Apply(__result);
        }
    }

    /// <summary>
    /// Applies <see cref="FemaleKindExtension.bodyType"/> right after genes are generated: late enough
    /// that Biotech body genes cannot overwrite it, early enough that gear is chosen for it.
    /// </summary>
    [HarmonyPatch(typeof(PawnGenerator), "GenerateGenes")]
    public static class Patch_PawnGenerator_GenerateGenes
    {
        public static void Postfix(Pawn pawn)
        {
            BodyTypeDef bodyType = pawn?.kindDef?.GetModExtension<FemaleKindExtension>()?.bodyType;
            if (bodyType != null && pawn.story != null && !pawn.DevelopmentalStage.Juvenile())
            {
                pawn.story.bodyType = bodyType;
            }
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
    ///
    /// Runs last, so a mod that turns the freebie off entirely gets there first and Harmony skips this
    /// one: VPE's prefix just returns false. We patch in the Mod constructor, before VPE's
    /// StaticConstructorOnStartup, so at equal priority we would run first and hand it out anyway.
    /// </summary>
    [HarmonyPatch(typeof(Hediff_Psylink), nameof(Hediff_Psylink.TryGiveAbilityOfLevel))]
    public static class Patch_Hediff_Psylink_TryGiveAbilityOfLevel
    {
        /// <summary>Per thread as a precaution, so the flag can only ever affect the code that set it.</summary>
        [ThreadStatic]
        public static bool suppress;

        [HarmonyPriority(Priority.Last)]
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
    /// Also none for psycasts marked <see cref="NoPsytrainerExtension"/>. Vanilla generates one
    /// psytrainer per psycast during def loading.
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
                    && (gain.ability.HasModExtension<MagierSpellExtension>() || gain.ability.HasModExtension<NoPsytrainerExtension>()))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Only a psycaster can wield the broom (<see cref="PsychicLiftExtension.CanLift"/>). Runs last, so
    /// no other mod's postfix - FFF lets its mechs wield weapons by tag - can let anyone else through.
    /// The equip float menu, opportunistic weapon pickup and the caravan gear tab all ask this.
    /// </summary>
    [HarmonyPatch(
        typeof(EquipmentUtility),
        nameof(EquipmentUtility.CanEquip),
        new Type[] { typeof(Thing), typeof(Pawn), typeof(string), typeof(bool) },
        new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal })]
    public static class Patch_EquipmentUtility_CanEquip
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Thing __0, Pawn __1, ref string __2, ref bool __result)
        {
            if (!__result || __0 == null || __1 == null)
            {
                return;
            }
            if (__0.def.HasModExtension<PsychicLiftExtension>() && !PsychicLiftExtension.CanLift(__1))
            {
                __result = false;
                // Vanilla's float menu writes ": " + cantReason.CapitalizeFirst().
                __2 = "DMST_PsychicLiftCannotEquip".Translate();
            }
        }
    }
}
