using HarmonyLib;
using Multiplayer.Compat;
using RimWorld;
using Verse;

namespace MultiplayerNewRatkinPlusPatch.Source.Mods;

/// <summary>
///     Styling station dummy support for gene-gated Ratkin body addons.
///     With Biotech active, ear addons require RK_Gene_LargeEars
///     (Patches/BodyAddon_EarCondition_Patch.xml replaces Race with Gene).
///     MP builds a StylingDialog_DummyPawn without a genes tracker, so
///     ExtendedGraphicsPawnWrapper.HasGene throws NRE during DrawPawn and
///     ears disappear from "Racial features" -&gt; "Body addons".
///     Fix: copy the genes tracker onto the dummy + null-safe HasGene
///     fallback to the origin pawn. All reflection, safe when HAR/MP/Biotech
///     are missing.
/// </summary>
public partial class NewRatkinPlus
{
    private static void PatchStylingGenes()
    {
        var dummyType = AccessTools.TypeByName("Multiplayer.Client.Patches.StylingDialog_DummyPawn");
        if (dummyType == null)
        {
            Log.Warning(
                $"{LogPrefix} Could not find MP StylingDialog_DummyPawn, skipping styling genes copy (HasGene guard still applied).");
        }
        else
        {
            var stylingCtor =
                AccessTools.Constructor(typeof(Dialog_StylingStation), new[] { typeof(Pawn), typeof(Thing) });
            if (stylingCtor == null)
            {
                Log.Warning(
                    $"{LogPrefix} Could not find Dialog_StylingStation(Pawn, Thing) ctor, skipping styling genes copy.");
            }
            else
            {
                MpCompat.harmony.Patch(stylingCtor,
                    postfix: new HarmonyMethod(typeof(NewRatkinPlus), nameof(StylingGenesPostfix)));
                Log.Message($"{LogPrefix} Patched Dialog_StylingStation ctor for dummy genes copy.");
            }
        }

        var wrapperType = AccessTools.TypeByName("AlienRace.ExtendedGraphics.ExtendedGraphicsPawnWrapper");
        if (wrapperType == null)
        {
            Log.Warning($"{LogPrefix} Could not find ExtendedGraphicsPawnWrapper, skipping HasGene guard.");
            return;
        }

        var hasGene = AccessTools.DeclaredMethod(wrapperType, "HasGene");
        if (hasGene == null)
        {
            Log.Warning($"{LogPrefix} Could not find ExtendedGraphicsPawnWrapper.HasGene, skipping HasGene guard.");
            return;
        }

        MpCompat.harmony.Patch(hasGene,
            new HarmonyMethod(typeof(NewRatkinPlus), nameof(PreHasGene)));
        Log.Message($"{LogPrefix} Patched ExtendedGraphicsPawnWrapper.HasGene null-guard.");
    }

    private static void StylingGenesPostfix(Pawn pawn)
    {
        try
        {
            if (pawn == null) return;

            var dummyType = AccessTools.TypeByName("Multiplayer.Client.Patches.StylingDialog_DummyPawn");
            if (dummyType == null || !dummyType.IsInstanceOfType(pawn)) return;

            // Already has genes (e.g. future MP copies them) - nothing to do.
            if (pawn.genes != null) return;

            var origField = AccessTools.Field(dummyType, "origPawn");
            var origin = origField?.GetValue(pawn) as Pawn;
            if (origin?.genes == null) return;

            // Shallow-copy the tracker like MP does for story/style/apparel,
            // then repoint to the dummy. Shared Gene references are fine:
            // the styling dialog only reads genes for render conditions.
            var newTracker = new Pawn_GeneTracker(pawn);
            foreach (var field in AccessTools.GetDeclaredFields(typeof(Pawn_GeneTracker)))
            {
                if (field.IsStatic) continue;
                try
                {
                    field.SetValue(newTracker, field.GetValue(origin.genes));
                }
                catch (Exception exception)
                {
                    Log.Warning($"{LogPrefix} Genes field copy failed for {field.Name}: {exception.Message}");
                }
            }

            newTracker.pawn = pawn;
            pawn.genes = newTracker;
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} StylingGenesPostfix failed: {exception}");
        }
    }

    private static bool PreHasGene(object __instance, GeneDef gene, ref bool __result)
    {
        try
        {
            if (gene == null)
            {
                __result = false;
                return false;
            }

            // Resolve the wrapped pawn without depending on HAR field layout.
            Pawn wrapped = null;
            try
            {
                var wrappedProp = AccessTools.Property(__instance.GetType(), "WrappedPawn");
                wrapped = wrappedProp?.GetValue(__instance) as Pawn;
            }
            catch (Exception exception)
            {
                Log.Warning($"{LogPrefix} WrappedPawn read failed, hiding gene addon: {exception.Message}");
                __result = false;
                return false;
            }

            if (wrapped == null)
            {
                __result = false;
                return false;
            }

            // Healthy path: real pawn with genes tracker - let HAR run.
            if (wrapped.genes != null) return true;

            // Dummy path: fall back to the origin pawn's genes so ear
            // visibility matches single-player instead of throwing.
            try
            {
                var dummyType = AccessTools.TypeByName("Multiplayer.Client.Patches.StylingDialog_DummyPawn");
                if (dummyType != null && dummyType.IsInstanceOfType(wrapped))
                {
                    var origField = AccessTools.Field(dummyType, "origPawn");
                    var origin = origField?.GetValue(wrapped) as Pawn;
                    __result = origin?.genes?.GetGene(gene)?.Active ?? false;
                    return false;
                }
            }
            catch (Exception exception)
            {
                Log.Warning($"{LogPrefix} Origin genes fallback failed, hiding gene addon: {exception.Message}");
            }

            __result = false;
            return false;
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} PreHasGene guard failed, hiding gene addon: {exception}");
            __result = false;
            return false;
        }
    }
}