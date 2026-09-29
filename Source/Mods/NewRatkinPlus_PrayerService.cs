using HarmonyLib;
using Multiplayer.API;
using Multiplayer.Compat;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace MultiplayerNewRatkinPlusPatch.Source.Mods;

/// <summary>
///     Prayer service ability (Command_AbilityPrayService).
///     ProcessInput picks a random pulpit and cooldown in UI context,
///     then creates a LordJob_PrayerService. Both choices must be synced.
/// </summary>
public partial class NewRatkinPlus
{
    private static void PatchPrayServiceAbility()
    {
        var commandType = AccessTools.TypeByName("NewRatkin.Command_AbilityPrayService");
        if (commandType == null)
        {
            Log.Warning(
                $"{LogPrefix} Could not find type NewRatkin.Command_AbilityPrayService, skipping prayer service sync.");
            return;
        }

        var processInput = AccessTools.DeclaredMethod(commandType, "ProcessInput", new[] { typeof(Event) });
        if (processInput == null)
        {
            Log.Warning($"{LogPrefix} Could not find Command_AbilityPrayService.ProcessInput(Event), skipping.");
            return;
        }

        MpCompat.harmony.Patch(processInput,
            new HarmonyMethod(typeof(NewRatkinPlus), nameof(PrePrayServiceProcessInput)));
        MP.RegisterSyncMethod(typeof(NewRatkinPlus), nameof(SyncedPrayService));
        Log.Message($"{LogPrefix} Synced prayer service ability.");
    }

    private static bool PrePrayServiceProcessInput(object __instance)
    {
        if (!MP.IsInMultiplayer) return true;

        if (MP.IsExecutingSyncCommand) return true;

        try
        {
            var organizer = ExtractAbilityPawn(__instance);
            if (organizer == null) return true;

            var ability = organizer.abilities?.GetAbility(DefDatabase<AbilityDef>.GetNamed("RK_PrayerService", false));
            if (ability == null) return false;

            if (organizer.Drafted || ability.CooldownTicksRemaining > 0) return false;

            // Check lord already giving service (mirrors original: ability.pawn.GetLord()?.LordJob is LordJob_PrayerService)
            var lord = organizer.GetLord();
            var prayerLordType = AccessTools.TypeByName("NewRatkin.LordJob_PrayerService");
            if (prayerLordType != null && lord?.LordJob != null &&
                prayerLordType.IsInstanceOfType(lord.LordJob))
                return false;

            // Pick pulpit + spot + cooldown locally (UI context), then sync the chosen values.
            // This avoids per-client Rand divergence for RandomElement / RandomInRange.
            if (!TryPickPrayerSpotLocal(organizer, out var pulpit, out var spot)) return false;

            var cooldownTicks = ability.def.cooldownTicksRange.RandomInRange;
            SyncedPrayService(organizer, pulpit, spot, cooldownTicks);
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} PrePrayServiceProcessInput failed, allowing original to run: {exception}");
            return true;
        }

        return false;
    }

    private static Pawn ExtractAbilityPawn(object commandInstance)
    {
        if (commandInstance == null) return null;

        var instanceType = commandInstance.GetType();
        // Base is RimWorld.Command_Ability (or VEF variant), field is usually "ability"
        var abilityField = AccessTools.Field(instanceType, "ability")
                           ?? AccessTools.Field(instanceType.BaseType, "ability");
        if (abilityField != null)
        {
            var ability = abilityField.GetValue(commandInstance) as Ability;
            if (ability?.pawn != null) return ability.pawn;
        }

        var pawnField = AccessTools.Field(instanceType, "pawn")
                        ?? AccessTools.Field(instanceType.BaseType, "pawn");
        return pawnField?.GetValue(commandInstance) as Pawn;
    }

    private static bool TryPickPrayerSpotLocal(Pawn organizer, out Building pulpit, out IntVec3 spot)
    {
        pulpit = null;
        spot = IntVec3.Invalid;

        if (organizer?.Map == null) return false;

        var pulpitDef = DefDatabase<ThingDef>.GetNamedSilentFail("RK_Pulpit");
        if (pulpitDef == null) return false;

        var candidates = organizer.Map.listerBuildings.AllBuildingsColonistOfDef(pulpitDef)
            .Where(candidate => IsPrayerSpotAvailable(organizer, candidate))
            .ToList();

        if (candidates.Count == 0) return false;

        var chosen = candidates.RandomElement();
        pulpit = chosen;
        spot = chosen.InteractionCell;
        return true;
    }

    private static bool IsPrayerSpotAvailable(Pawn organizer, Building pulpit)
    {
        try
        {
            var commandType = AccessTools.TypeByName("NewRatkin.Command_AbilityPrayService");
            var blockReasonMethod = AccessTools.Method(commandType, "GetPrayerServiceSpotBlockReason");
            if (blockReasonMethod == null) return true;

            var reason = blockReasonMethod.Invoke(null, new object[] { organizer, pulpit });
            // PrayerServiceSpotBlockReason.None == 0
            return reason != null && Convert.ToInt32(reason) == 0;
        }
        catch (Exception exception)
        {
            Log.Warning($"{LogPrefix} IsPrayerSpotAvailable check failed, assuming available: {exception}");
            return true;
        }
    }

    private static void SyncedPrayService(Pawn organizer, Building pulpit, IntVec3 spot, int cooldownTicks)
    {
        if (organizer == null || pulpit == null) return;

        var prayerDef = DefDatabase<AbilityDef>.GetNamedSilentFail("RK_PrayerService");
        var ability = prayerDef == null ? null : organizer.abilities?.GetAbility(prayerDef);
        if (ability == null) return;

        ability.StartCooldown(cooldownTicks);
        foreach (var other in PawnsFinder.AllMaps_FreeColonistsAndPrisoners)
            other.abilities?.GetAbility(prayerDef)?.StartCooldown(cooldownTicks);

        var lordJobType = AccessTools.TypeByName("NewRatkin.LordJob_PrayerService");
        if (lordJobType == null) return;

        var map = organizer.Map;
        if (map == null) return;

        var faction = organizer.Faction;
        var lordJob = Activator.CreateInstance(lordJobType, pulpit, spot, organizer);
        var organizerIsStartingPawn = false;
        try
        {
            var startingProp = AccessTools.Property(lordJobType, "OrganizerIsStartingPawn");
            if (startingProp != null) organizerIsStartingPawn = (bool)startingProp.GetValue(lordJob);
        }
        catch (Exception exception)
        {
            Log.Warning($"{LogPrefix} Failed to read OrganizerIsStartingPawn, defaulting to false: {exception}");
        }

        var startingPawns = organizerIsStartingPawn ? new[] { organizer } : null;
        LordMaker.MakeNewLord(faction, (LordJob)lordJob, map, startingPawns);
    }
}