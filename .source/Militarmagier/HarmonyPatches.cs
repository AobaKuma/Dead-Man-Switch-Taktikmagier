using HarmonyLib;
using System;
using Verse;

namespace Militarmagier
{
    /// <summary>Applies this assembly's Harmony patches as soon as the mod is constructed.</summary>
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
    /// Lets <see cref="MagierKindExtension"/> raise a psylink without vanilla adding a random psycast
    /// for each level. Off unless that code sets <see cref="suppress"/>.
    /// </summary>
    [HarmonyPatch(typeof(Hediff_Psylink), nameof(Hediff_Psylink.TryGiveAbilityOfLevel))]
    public static class Patch_Hediff_Psylink_TryGiveAbilityOfLevel
    {
        /// <summary>Per thread as a precaution, so the flag can only ever affect the code that set it.</summary>
        [ThreadStatic]
        public static bool suppress;

        public static bool Prefix()
        {
            return !suppress;
        }
    }
}
