using BepInEx.Configuration;
using CargoToGo.Bootstrap;
using KydlCore.Common;
using CargoToGo.Core;

namespace CargoToGo.Presentation.Hub.Registration;

/// <summary>What CargoToGo declares to the KY hub (F6, MIGRATION_PLAN B11): its title and its settings tab (every config entry).</summary>
internal static class HubTabs
{
    internal const string SettingsId = "settings";

    internal static bool RegisterModPage(ConfigFile config)
    {
        global::KydlCore.Features.Hub.Contracts.KyHub.SetModTitle(CargoToGoPlugin.ModGuid, "Cargo To Go");
        var tab = global::KydlCore.Features.Hub.Contracts.KyHub.AddTab(CargoToGoPlugin.ModGuid, SettingsId, "Settings", () => global::KydlCore.Features.Hub.Contracts.KyHub.ConfigEntries(config), 0);
        ModLog.Verbose("HubTabs: settings tab " + (tab != null ? "registered" : "FAILED"));
        return tab != null;
    }

    internal static bool UnregisterModPage()
    {
        if (RuleLog.IsOn()) ModLog.Verbose($"HubTabs.UnregisterModPage: removing tab {SettingsId} of {CargoToGoPlugin.ModGuid}");
        global::KydlCore.Features.Hub.Contracts.KyHub.RemoveTab(CargoToGoPlugin.ModGuid, SettingsId);
        { var returnedAt25 = !global::KydlCore.Features.Hub.Contracts.KyHub.HasTab(CargoToGoPlugin.ModGuid, SettingsId); if (RuleLog.IsOn()) ModLog.Verbose($"HubTabs.UnregisterModPage: returns {returnedAt25} (line 25)"); return returnedAt25; }
    }
}
