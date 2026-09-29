using HarmonyLib;
using Multiplayer.Compat;
using Verse;

namespace MultiplayerNewRatkinPlusPatch.Source.Mods;

/// <summary>
///     EMP incident map fix.
///     GameComponent_EMPCheck.DoEmpIncident uses Find.CurrentMap
///     but should use the exploding Thing's map.
/// </summary>
public partial class NewRatkinPlus
{
    private static void PatchEmpIncident()
    {
        // DoEmpIncident(Thing) uses Find.CurrentMap but should use Thing.Map.
        // ReplaceCurrentMapUsage rewrites Find.CurrentMap / Game.CurrentMap to use the Thing argument.
        var empCheckType = AccessTools.TypeByName("NewRatkin.GameComponent_EMPCheck");
        if (empCheckType == null)
        {
            Log.Warning($"{LogPrefix} Could not find type NewRatkin.GameComponent_EMPCheck, skipping CurrentMap fix.");
            return;
        }

        var doEmp = AccessTools.DeclaredMethod(empCheckType, "DoEmpIncident");
        if (doEmp == null)
        {
            Log.Warning($"{LogPrefix} Could not find GameComponent_EMPCheck.DoEmpIncident, skipping.");
            return;
        }

        PatchingUtilities.ReplaceCurrentMapUsage(doEmp);
        Log.Message($"{LogPrefix} Patched GameComponent_EMPCheck.DoEmpIncident CurrentMap usage.");
    }
}