using System;
using CargoToGo.Features.CargoTab.Infrastructure;
using HarmonyLib;
using KydlCore.Common;

namespace CargoToGo.Integrations.Harmony
{
    // MIGRATION_DECISIONS A5: the hammer menu hooks of feature CargoTab; the work is CargoTab.Install / CargoTab.Tick.
    // ---------------------------------------------------------------- hooks
    [HarmonyPatch(typeof(BuildUi), "Awake")]
    internal static class BuildUi_Awake
    {
        private static void Postfix(BuildUi __instance)
        {
            try { CargoTab.Install(__instance); }
            catch (Exception e) { ModLog.Error("Adding the Cargos tab failed; the build menu keeps its 4 vanilla tabs | " + e); }
        }
    }

    [HarmonyPatch(typeof(BuildUi), "Update")]
    internal static class BuildUi_Update
    {
        private static void Postfix(BuildUi __instance) { CargoTab.Tick(__instance); }
    }
}
