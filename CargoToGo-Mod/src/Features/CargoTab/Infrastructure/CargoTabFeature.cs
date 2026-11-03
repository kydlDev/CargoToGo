using Kydl.Common;
using Kydl.Core.Features;

namespace CargoToGo.Features.CargoTab.Infrastructure
{
    /// <summary>The 5th Cargos tab of the build menu. Gate: [General] EnableAll and [CargoTab] Enable. The tab is built when the build menu is created, so a change needs a game restart (as the setting says).</summary>
    internal sealed class CargoTabFeature : IFeature
    {
        internal const string FeatureId = "cargo-tab";
        internal static bool Running { get; private set; }
        public string Id { get { return FeatureId; } }
        public bool IsEnabled() { return ModSettings.On && ModSettings.ShowCargoTab.Value; }
        public bool Start() { Running = true; ModLog.Verbose("CargoTabFeature: started"); return Running; }
        public bool Stop() { Running = false; ModLog.Verbose("CargoTabFeature: stopped"); return !Running; }
    }
}
