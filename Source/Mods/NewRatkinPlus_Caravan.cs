using HarmonyLib;
using Multiplayer.API;
using Multiplayer.Compat;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace MultiplayerNewRatkinPlusPatch.Source.Mods;

/// <summary>
///     Wandering caravan settler dialogs (Dialog_CaravanSettlers).
///     AcceptPawn / AcceptAllSettlers change faction, lord and GameComponent state,
///     so they must run as synced commands. Open dialog lists are cleaned up
///     on all clients after the sync.
/// </summary>
public partial class NewRatkinPlus
{
    private static void PatchWanderingCaravanDialogs()
    {
        var dialogType = AccessTools.TypeByName("NewRatkin.Dialog_CaravanSettlers");
        if (dialogType == null)
        {
            Log.Warning(
                $"{LogPrefix} Could not find type NewRatkin.Dialog_CaravanSettlers, skipping caravan dialog sync.");
            return;
        }

        caravanSettlersListField = AccessTools.FieldRefAccess<List<Pawn>>(dialogType, "settlers");
        if (caravanSettlersListField == null)
            Log.Warning(
                $"{LogPrefix} Could not find field Dialog_CaravanSettlers.settlers, UI cleanup will be skipped.");

        var acceptPawn = AccessTools.DeclaredMethod(dialogType, "AcceptPawn");
        var acceptAll = AccessTools.DeclaredMethod(dialogType, "AcceptAllSettlers");
        if (acceptPawn == null || acceptAll == null)
        {
            Log.Warning($"{LogPrefix} Could not find Dialog_CaravanSettlers.AcceptPawn/AcceptAllSettlers, skipping.");
            return;
        }

        MpCompat.harmony.Patch(acceptPawn,
            new HarmonyMethod(typeof(NewRatkinPlus), nameof(PreAcceptPawn)));
        MpCompat.harmony.Patch(acceptAll,
            new HarmonyMethod(typeof(NewRatkinPlus), nameof(PreAcceptAllSettlers)));

        MP.RegisterSyncMethod(typeof(NewRatkinPlus), nameof(SyncedAcceptPawn));
        MP.RegisterSyncMethod(typeof(NewRatkinPlus), nameof(SyncedAcceptAllSettlers));
        Log.Message($"{LogPrefix} Synced Dialog_CaravanSettlers accept actions.");
    }

    private static bool PreAcceptPawn(Pawn pawn)
    {
        if (!MP.IsInMultiplayer) return true;

        if (MP.IsExecutingSyncCommand) return true;

        SyncedAcceptPawn(pawn);
        return false;
    }

    private static bool PreAcceptAllSettlers(List<Pawn> pawns)
    {
        if (!MP.IsInMultiplayer) return true;

        if (MP.IsExecutingSyncCommand) return true;

        // Copy to avoid mutation during sync serialization
        SyncedAcceptAllSettlers(pawns == null ? new List<Pawn>() : new List<Pawn>(pawns));
        return false;
    }

    private static void SyncedAcceptPawn(Pawn pawn)
    {
        if (pawn == null || pawn.DestroyedOrNull() || pawn.Dead) return;

        var map = pawn.Map ?? Find.CurrentMap;
        if (map == null) return;

        var gameCompType = AccessTools.TypeByName("NewRatkin.GameComponent_WanderingCaravan");
        object gameComp = gameCompType == null ? null : Current.Game?.GetComponent(gameCompType);
        if (gameComp != null)
        {
            var onAccepted = AccessTools.Method(gameCompType, "OnSettlerAccepted");
            try
            {
                onAccepted?.Invoke(gameComp, new object[] { pawn });
            }
            catch (Exception exception)
            {
                Log.Error($"{LogPrefix} OnSettlerAccepted failed: {exception}");
            }
        }

        try
        {
            pawn.GetLord()?.Notify_PawnLost(pawn, PawnLostCondition.LeftVoluntarily);
        }
        catch (Exception exception)
        {
            Log.Warning($"{LogPrefix} Notify_PawnLost failed in SyncedAcceptPawn: {exception}");
        }

        try
        {
            pawn.SetFaction(Faction.OfPlayer);
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} SetFaction failed in SyncedAcceptPawn: {exception}");
        }

        Messages.Message("RK_WanderingCaravan_OneJoined".Translate(pawn.LabelShortCap), pawn,
            MessageTypeDefOf.PositiveEvent);

        RemovePawnFromOpenSettlerDialogs(pawn);
    }

    private static void SyncedAcceptAllSettlers(List<Pawn> pawns)
    {
        if (pawns == null || pawns.Count == 0) return;

        var valid = pawns.Where(candidate => candidate != null && !candidate.DestroyedOrNull() && !candidate.Dead)
            .ToList();
        if (valid.Count == 0) return;

        var map = valid[0].Map ?? Find.CurrentMap;
        if (map == null) return;

        var gameCompType = AccessTools.TypeByName("NewRatkin.GameComponent_WanderingCaravan");
        object gameComp = gameCompType == null ? null : Current.Game?.GetComponent(gameCompType);
        var onAccepted = gameCompType == null ? null : AccessTools.Method(gameCompType, "OnSettlerAccepted");

        var lord = valid[0].GetLord();
        foreach (var settler in valid)
        {
            try
            {
                onAccepted?.Invoke(gameComp, new object[] { settler });
            }
            catch (Exception exception)
            {
                Log.Warning($"{LogPrefix} OnSettlerAccepted failed for {settler}: {exception}");
            }

            try
            {
                lord?.Notify_PawnLost(settler, PawnLostCondition.LeftVoluntarily);
            }
            catch (Exception exception)
            {
                Log.Warning($"{LogPrefix} Notify_PawnLost failed for {settler}: {exception}");
            }

            try
            {
                settler.SetFaction(Faction.OfPlayer);
            }
            catch (Exception exception)
            {
                Log.Error($"{LogPrefix} SetFaction failed for {settler}: {exception}");
            }
        }

        Messages.Message("RK_WanderingCaravan_AllJoined".Translate(valid.Count), MessageTypeDefOf.PositiveEvent);

        CloseOpenSettlerDialogs();
    }

    private static void RemovePawnFromOpenSettlerDialogs(Pawn pawn)
    {
        if (caravanSettlersListField == null || pawn == null) return;

        try
        {
            var dialogType = AccessTools.TypeByName("NewRatkin.Dialog_CaravanSettlers");
            if (dialogType == null) return;

            foreach (var window in Find.WindowStack.Windows.ToList())
                if (window != null && dialogType.IsInstanceOfType(window))
                {
                    var list = caravanSettlersListField(window);
                    list?.Remove(pawn);
                }
        }
        catch (Exception exception)
        {
            Log.Warning($"{LogPrefix} Failed to remove pawn from open settler dialogs: {exception}");
        }
    }

    private static void CloseOpenSettlerDialogs()
    {
        try
        {
            var dialogType = AccessTools.TypeByName("NewRatkin.Dialog_CaravanSettlers");
            if (dialogType == null) return;

            foreach (var window in Find.WindowStack.Windows.ToList())
                if (window != null && dialogType.IsInstanceOfType(window))
                    window.Close();
        }
        catch (Exception exception)
        {
            Log.Warning($"{LogPrefix} Failed to close open settler dialogs: {exception}");
        }
    }
}