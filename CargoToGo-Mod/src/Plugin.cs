using Kydl.Common;
using System;
using BepInEx;
using BepInEx.Configuration;

namespace CargoToGo
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency(KydlCore.Plugin.ModGuid, "0.1.0")]
    [BepInDependency(AzuCompat.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    public class CargoToGoPlugin : BaseUnityPlugin
    {
        public const string ModGuid = "com.kydldev.CargoToGo";   // also the config file name: com.kydldev.CargoToGo.cfg
        public const string ModName = "CargoToGo";
        public const string ModVersion = "2.0.0";

        private void Awake()
        {
            ModLog.Init(Logger, ModName, () => ModSettings.Verbose);
            Loc.Register(CgText.En);
            ModSettings.Bind(Config);

            ModLog.Info(ModName + " " + ModVersion + " starting. EnableAll=" + ModSettings.EnableAll.Value + " Range=" + ModSettings.ContainerRange.Value + " PullFromContainers=" + ModSettings.PullFromContainers.Value + " UseAzuCraftyBoxes=" + ModSettings.UseAzuCraftyBoxes.Value + " (AzuCraftyBoxes installed: " + AzuCompat.Installed + ")" +
                     " ContainersFirst=" + ModSettings.ContainersFirst.Value + " PackInWorld=" + ModSettings.PackInWorld.Value + " Repack=" + ModSettings.Repack.Value + " PackEffects=" + ModSettings.PackEffects.Value + " ShowKeyHints=" + ModSettings.ShowKeyHints.Value + " ShowCargoTab=" + ModSettings.ShowCargoTab.Value +
                     " AltDestroyAnyPiece=" + ModSettings.AltDestroyAnyPiece.Value + " WeightMultiplier=" + ModSettings.WeightMultiplier.Value);            try
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
        }
    }
}
