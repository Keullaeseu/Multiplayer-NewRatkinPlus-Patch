using HarmonyLib;
using Multiplayer.Compat;
using Verse;

namespace MultiplayerNewRatkinPlusPatch.Source.Mods;

/// <summary>
///     Multiplayer Patch for NewRatkinPlus by Gloomylynx Nukafrog,
///     Last Update: 20 May @ 5:01pm 2026
/// </summary>
/// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=1578693166" />
[MpCompatFor("Solaris.RatkinRaceMod")]
public partial class NewRatkinPlus
{
    internal const string LogPrefix = "[Multiplayer NewRatkinPlus Patch]";

    // Cached reflection for Dialog_CaravanSettlers UI list cleanup (populated in caravan partial)
    private static AccessTools.FieldRef<object, List<Pawn>> caravanSettlersListField;

    public NewRatkinPlus(ModContentPack content)
    {
        LongEventHandler.ExecuteWhenFinished(LatePatch);
    }

    private static void LatePatch()
    {
        Log.Message($"{LogPrefix} Initializing...");

        try
        {
            PatchToggleGizmos();
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Failed to patch toggle gizmos: {exception}");
        }

        try
        {
            PatchShieldFaceDirection();
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Failed to patch shield face direction: {exception}");
        }

        try
        {
            PatchPrayServiceAbility();
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Failed to patch prayer service ability: {exception}");
        }

        try
        {
            PatchWanderingCaravanDialogs();
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Failed to patch wandering caravan dialogs: {exception}");
        }

        try
        {
            PatchEmpIncident();
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Failed to patch EMP incident: {exception}");
        }

        Log.Message($"{LogPrefix} Initialized.");
    }
}