using System.Reflection;
using HarmonyLib;
using Multiplayer.Compat;
using Verse;

namespace MultiplayerNewRatkinPlusPatch.Source.Mods;

/// <summary>
///     Combat RNG fixes.
///     Verb_ChainSword.ApplyFirstDamage uses UnityEngine.Random in sim context,
///     which is per-client and desyncs. Replace with Verse.Rand (no PushPop,
///     so it stays part of the synced sim Rand sequence).
/// </summary>
public partial class NewRatkinPlus
{
    private static void PatchCombatRng()
    {
        var chainSwordType = AccessTools.TypeByName("NewRatkin.Verb_ChainSword");
        if (chainSwordType == null)
        {
            Log.Warning($"{LogPrefix} Could not find type NewRatkin.Verb_ChainSword, skipping combat RNG fix.");
            return;
        }

        MethodBase applyFirstDamage =
            AccessTools.DeclaredMethod(chainSwordType, "ApplyFirstDamage")
            ?? AccessTools.Method(chainSwordType, "ApplyFirstDamage");
        if (applyFirstDamage == null)
        {
            Log.Warning($"{LogPrefix} Could not find Verb_ChainSword.ApplyFirstDamage, skipping combat RNG fix.");
            return;
        }

        // Sim-context damage: patchPushPop false so the replaced Rand calls
        // remain part of the synced Rand sequence instead of being isolated.
        PatchingUtilities.PatchUnityRand(applyFirstDamage, false);
        Log.Message($"{LogPrefix} Patched Verb_ChainSword.ApplyFirstDamage Unity RNG.");
    }
}