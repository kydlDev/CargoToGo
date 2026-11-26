using BepInEx.Configuration;
using CargoToGo.Core;
using CargoToGo.Features.CargoTab.Infrastructure;
using CargoToGo.Features.Gravestone.Infrastructure;
using CargoToGo.Features.KeyHints.Infrastructure;
using CargoToGo.Features.Packing.Infrastructure;
using CargoToGo.Features.Repack.Infrastructure;
using CargoToGo.Presentation.Hub.Registration;
using KydlCore.Common;
using KydlCore.Core;

namespace CargoToGo.Bootstrap;

/// <summary>Composition root: the mod's KY hub page (title + settings tab) and its features in the FeatureRegistry (MIGRATION_PLAN A4).</summary>
internal sealed class ModInstaller
{
    internal static ModInstaller Current { get; private set; }
    readonly ConfigFile _config;
    internal FeatureRegistry Features { get; }
    internal ModInstaller(ConfigFile config) { _config = config; Features = new FeatureRegistry(line => ModLog.Verbose(line)); }

    internal bool Install()
    {
        Current = this;
        bool page = HubTabs.RegisterModPage(_config);
        bool added = true;
        foreach (IFeature feature in new IFeature[] { new PackingFeature(), new RepackFeature(), new KeyHintsFeature(), new CargoTabFeature(), new GravestoneFeature() })
        {
            OperationResponse<int> add = Features.Add(feature);
            if (!add.IsSuccess) { added = false; ModLog.Error("ModInstaller: " + add.Reason); }
        }
        { var returnedAt32 = page && added && Refresh(); if (RuleLog.IsOn()) ModLog.Every("ModInstaller.Install:32", 2d, $"ModInstaller.Install: returns {returnedAt32} (line 32)"); return returnedAt32; }
    }

    internal bool Refresh()
    {
        OperationResponse<int> r = Features.Refresh();
        if (r.Status == ResponseStatus.Failed) ModLog.Warn("ModInstaller: " + r.Reason); else ModLog.Verbose("ModInstaller: refresh - " + r);
        return r.IsSuccess;
    }

    internal bool Uninstall()
    {
        if (RuleLog.IsOn()) ModLog.Verbose($"ModInstaller.Uninstall: stopping the features and the hub page of {CargoToGoPlugin.ModGuid}");
        OperationResponse<int> r = Features.StopAll();
        bool gone = HubTabs.UnregisterModPage();
        if (Current == this) Current = null;
        { var returnedAt48 = r.IsSuccess && gone; if (RuleLog.IsOn()) ModLog.Verbose($"ModInstaller.Uninstall: returns {returnedAt48} (line 48)"); return returnedAt48; }
    }
}
