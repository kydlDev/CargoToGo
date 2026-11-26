using CargoToGo.Configuration;
using KydlCore.Common;
using KydlCore.Core;

namespace CargoToGo.Features.CargoTab.Infrastructure;

/// <summary>The 5th Cargos tab of the build menu. Gate: [General] Enable Mod and [CargoTab] Enable. The tab is built when the build menu is created, so a change needs a game restart (as the setting says).</summary>
internal sealed class CargoTabFeature : IFeature
{
    internal const string FeatureId = "cargo-tab";
    internal static bool Running { get; private set; }
    public string Id { get { return FeatureId; } }
    public bool IsEnabled() { return ModSettings.On && ModSettings.ShowCargoTab.Value; }
    public bool Start() { Running = true; ModLog.Verbose("CargoTabFeature: started (running " + Running + ", gate " + IsEnabled() + ")"); return Running; }
    public bool Stop() { Running = false; ModLog.Verbose("CargoTabFeature: stopped (running " + Running + ", gate " + IsEnabled() + ")"); return !Running; }
}
