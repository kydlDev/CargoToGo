using KydlCore.Common;
using KydlCore.Core;

namespace CargoToGo.Features.Gravestone.Infrastructure;

/// <summary>Cargos survive death: they move to the gravestone and back. Always on, even with [General] Enable Mod off: a cargo you hold must never be lost (the setting text says so), so this feature has no Enable entry of its own.</summary>
internal sealed class GravestoneFeature : IFeature
{
    internal const string FeatureId = "gravestone";
    internal static bool Running { get; private set; }
    public string Id { get { return FeatureId; } }
    public bool IsEnabled() { return true; }
    public bool Start() { Running = true; ModLog.Verbose("GravestoneFeature: started (running " + Running + ", gate " + IsEnabled() + ")"); return Running; }
    public bool Stop() { Running = false; ModLog.Verbose("GravestoneFeature: stopped (running " + Running + ", gate " + IsEnabled() + ")"); return !Running; }
}
