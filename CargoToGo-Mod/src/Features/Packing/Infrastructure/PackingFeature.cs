using CargoToGo.Configuration;
using KydlCore.Common;
using KydlCore.Core;

namespace CargoToGo.Features.Packing.Infrastructure;

/// <summary>Pack the materials of a piece into a cargo (Ctrl+Alt + click) and build with a cargo. Gate: [General] Enable mod and [PackInWorld] Enable. The patches (Integrations/Harmony/PackingPatches) check the same settings on every call.</summary>
internal sealed class PackingFeature : IFeature
{
    internal const string FeatureId = "packing";
    internal static bool Running { get; private set; }
    public string Id { get { return FeatureId; } }
    public bool IsEnabled() { return ModSettings.On && ModSettings.PackInWorld.Value; }
    public bool Start() { Running = true; ModLog.Verbose("PackingFeature: started"); return Running; }
    public bool Stop() { Running = false; ModLog.Verbose("PackingFeature: stopped"); return !Running; }
}
