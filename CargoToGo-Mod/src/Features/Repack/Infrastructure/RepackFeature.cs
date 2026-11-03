using Kydl.Common;
using Kydl.Core.Features;

namespace CargoToGo.Features.Repack.Infrastructure
{
    /// <summary>ALT + destroy turns a piece back into a cargo. Gate: [General] EnableAll and [Repack] Enable.</summary>
    internal sealed class RepackFeature : IFeature
    {
        internal const string FeatureId = "repack";
        internal static bool Running { get; private set; }
        public string Id { get { return FeatureId; } }
        public bool IsEnabled() { return ModSettings.On && ModSettings.Repack.Value; }
        public bool Start() { Running = true; ModLog.Verbose("RepackFeature: started"); return Running; }
        public bool Stop() { Running = false; ModLog.Verbose("RepackFeature: stopped"); return !Running; }
    }
}
