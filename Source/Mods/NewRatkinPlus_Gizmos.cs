using HarmonyLib;
using Multiplayer.API;
using Multiplayer.Compat;
using Verse;

namespace MultiplayerNewRatkinPlusPatch.Source.Mods;

/// <summary>
///     Gizmo toggles for weapons and shield.
///     BFR ammo toggle flips isHEMode, PulseRifle flips isBurstMode,
///     Shield face direction issues jobs to selected pawns.
/// </summary>
public partial class NewRatkinPlus
{
    private static void PatchToggleGizmos()
    {
        var bfrToggleType = AccessTools.TypeByName("NewRatkin.Comp_BFRAmmoToggle");
        if (bfrToggleType == null)
        {
            Log.Warning(
                $"{LogPrefix} Could not find type NewRatkin.Comp_BFRAmmoToggle, skipping BFR ammo toggle sync.");
        }
        else
        {
            // Captures canToggle bool + this, must use Delegate to sync captured fields.
            MpCompat.RegisterLambdaDelegate(bfrToggleType, "GetToggleGizmos", 0);
            Log.Message($"{LogPrefix} Synced Comp_BFRAmmoToggle.GetToggleGizmos lambda 0.");
        }

        var pulseToggleType = AccessTools.TypeByName("NewRatkin.Comp_PulseRifleFireMode");
        if (pulseToggleType == null)
        {
            Log.Warning(
                $"{LogPrefix} Could not find type NewRatkin.Comp_PulseRifleFireMode, skipping pulse rifle toggle sync.");
        }
        else
        {
            // Captures only this, Method is sufficient and lighter.
            MpCompat.RegisterLambdaMethod(pulseToggleType, "GetToggleGizmos", 0);
            Log.Message($"{LogPrefix} Synced Comp_PulseRifleFireMode.GetToggleGizmos lambda 0.");
        }
    }

    private static void PatchShieldFaceDirection()
    {
        var shieldType = AccessTools.TypeByName("NewRatkin.CompShieldFaceDirection");
        if (shieldType == null)
        {
            Log.Warning($"{LogPrefix} Could not find type NewRatkin.CompShieldFaceDirection, skipping shield sync.");
            return;
        }

        // Lambda is Action<LocalTargetInfo> operating on Find.Selector.SelectedPawns.
        // MapSelected restores selection on all clients during sync execution.
        // The lambda itself is non-capturing (static helpers only), so Method is appropriate.
        MpCompat.RegisterLambdaMethod(shieldType, "GetFaceDirectionGizmos", 0)
            .SetContext(SyncContext.MapSelected);
        Log.Message($"{LogPrefix} Synced CompShieldFaceDirection.GetFaceDirectionGizmos lambda 0.");
    }
}