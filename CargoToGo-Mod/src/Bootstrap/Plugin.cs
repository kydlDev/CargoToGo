using System;
using BepInEx;
using CargoToGo.Configuration;
using CargoToGo.Features.Packing.Infrastructure;
using CargoToGo.Shared;
using KydlCore.Common;

namespace CargoToGo.Bootstrap;

[BepInPlugin(ModGuid, ModName, ModVersion)]
[BepInDependency(KydlCore.Bootstrap.Plugin.ModGuid, "1.2.0")]
[BepInDependency(AzuCompat.Guid, BepInDependency.DependencyFlags.SoftDependency)]
// Network check: ModSync.cs (KydlCore 1.2.0 KyNetwork, ClientOnly; no Jotunn - MIGRATION_PLAN part 0).
public class CargoToGoPlugin : BaseUnityPlugin
{
    public const string ModGuid = "com.kydldev.CargoToGo";   // also the config file name: com.kydldev.CargoToGo.cfg
    public const string ModName = "CargoToGo";
    public const string ModVersion = "2.0.2";

    ModInstaller _installer;

    private void Awake()
    {
        ModLog.Init(Logger, ModName, () => ModSettings.Verbose);
        Loc.Register(CgText.En);
        ModSettings.Bind(Config);
        int adminShared = ModSync.ShareAdminOnly(Config);     // network check + server-decided settings (KydlCore KyNetwork)
        CargoToGo.Core.RuleLog.Sink = ModLog.Verbose;                   // the pure code logs through ModLog (PROJECT_RULES 8, KydlCore D-103)
        CargoToGo.Core.RuleLog.DebugOn = () => ModSettings.Verbose;

        ModLog.Info(ModName + " " + ModVersion + " starting. EnableMod=" + ModSettings.EnableMod.Value + " Range=" + ModSettings.ContainerRange.Value + " PullFromContainers=" + ModSettings.PullFromContainers.Value + " UseAzuCraftyBoxes=" + ModSettings.UseAzuCraftyBoxes.Value + " (AzuCraftyBoxes installed: " + AzuCompat.Installed + ")" +
                 " ContainersFirst=" + ModSettings.ContainersFirst.Value + " PackInWorld=" + ModSettings.PackInWorld.Value + " Repack=" + ModSettings.Repack.Value + " PackEffects=" + ModSettings.PackEffects.Value + " ShowKeyHints=" + ModSettings.ShowKeyHints.Value + " ShowCargoTab=" + ModSettings.ShowCargoTab.Value +
                 " AltDestroyAnyPiece=" + ModSettings.AltDestroyAnyPiece.Value + " WeightMultiplier=" + ModSettings.WeightMultiplier.Value);
        try
        {
            var harmony = new HarmonyLib.Harmony(ModGuid);
            harmony.PatchAll();
            var n = 0;
            foreach (var m in harmony.GetPatchedMethods()) n++;
            ModLog.Info("Harmony patched " + n + " methods.");
        }
        catch (Exception e)
        {
            ModLog.Error("Harmony patching failed, the mod will not work" + " | " + e);
        }

        _installer = new ModInstaller(Config);
        bool installed = _installer.Install();
        Config.SettingChanged += (s, e) => { if (_installer != null) _installer.Refresh(); };
        ModLog.Verbose("Plugin: Awake finished (architecture " + Core.Architecture.Tag + ", features " + (installed ? "started" : "had failures") + ")");
    }

    private void OnDestroy()
    {
        if (_installer != null) { _installer.Uninstall(); _installer = null; }
        ModLog.Verbose("Plugin: destroyed (installer released " + (_installer == null) + ")");
    }
}
