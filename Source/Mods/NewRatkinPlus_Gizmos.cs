using System.Reflection;
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

        // GetFaceDirectionGizmos is a private iterator with 7 lambdas in source order:
        // 0: Action<LocalTargetInfo> (Command_Target.action, the sync target),
        // 1-6: Func<> LINQ predicates for drafted/dead/busy/cooldown checks.
        // Resolve by delegate signature instead of bare ordinal so LINQ reorderings
        // do not silently sync the wrong lambda. Falls back to ordinal 0.
        var shieldAction = ResolveShieldFaceAction(shieldType);
        if (shieldAction == null)
        {
            Log.Warning($"{LogPrefix} Could not resolve CompShieldFaceDirection face action, skipping shield sync.");
            return;
        }

        // MapSelected restores Find.Selector.SelectedPawns on all clients during sync execution.
        // The lambda itself is non-capturing (static helpers only), so Method is appropriate.
        MP.RegisterSyncMethod(shieldAction).SetContext(SyncContext.MapSelected);
        Log.Message(
            $"{LogPrefix} Synced CompShieldFaceDirection.GetFaceDirectionGizmos face action ({shieldAction.DeclaringType?.Name}.{shieldAction.Name}).");
    }

    private static MethodInfo ResolveShieldFaceAction(Type shieldType)
    {
        for (var ordinal = 0; ordinal <= 15; ordinal++)
        {
            MethodInfo candidate;
            try
            {
                candidate = MpMethodUtil.GetLambda(shieldType, "GetFaceDirectionGizmos", MethodType.Normal, null,
                    ordinal);
            }
            catch (Exception exception)
            {
                // No more lambdas at higher ordinals; stop scanning.
                // Ordinal 0 must exist, so reaching here with ordinal 0 means parent lookup failed.
                Log.Warning($"{LogPrefix} Shield lambda scan stopped at ordinal {ordinal}: {exception.Message}");
                break;
            }

            if (candidate == null) continue;

            var parameters = candidate.GetParameters();
            if (candidate.ReturnType == typeof(void)
                && parameters.Length == 1
                && parameters[0].ParameterType == typeof(LocalTargetInfo))
                return candidate;
        }

        // Fallback: original ordinal-0 assumption (verified against 1.6 source).
        try
        {
            return MpMethodUtil.GetLambda(shieldType, "GetFaceDirectionGizmos");
        }
        catch (Exception exception)
        {
            Log.Warning($"{LogPrefix} Shield fallback ordinal 0 failed: {exception.Message}");
            return null;
        }
    }
}