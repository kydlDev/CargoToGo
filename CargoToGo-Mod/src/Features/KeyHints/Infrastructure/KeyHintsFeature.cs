using Kydl.Common;
using Kydl.Core.Features;

namespace CargoToGo.Features.KeyHints.Infrastructure
{
    /// <summary>The Pack cargo / Repack hints in the build-mode key hint bar. Gate: [General] EnableAll and [KeyHints] Enable. KeyHintUi hides its row on the next tick when the gate closes.</summary>
    internal sealed class KeyHintsFeature : IFeature
    {
        internal const string FeatureId = "key-hints";
        internal static bool Running { get; private set; }
        public string Id { get { return FeatureId; } }
        public bool IsEnabled() { return ModSettings.On && ModSettings.ShowKeyHints.Value; }
        public bool Start() { Running = true; ModLog.Verbose("KeyHintsFeature: started"); return Running; }
        public bool Stop() { Running = false; ModLog.Verbose("KeyHintsFeature: stopped"); return !Running; }
    }
}
