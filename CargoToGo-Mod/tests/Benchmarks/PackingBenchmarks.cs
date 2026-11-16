using BenchmarkDotNet.Attributes;
using CargoToGo.Features.Packing.Domain;

namespace CargoToGo.Benchmarks;

/// <summary>Packing rules: run when the player loads a piece into a cargo stack (comparison axis: how many inventory stacks are searched).</summary>
[MemoryDiagnoser]
public class PackingBenchmarks
{
    int[] _available;
    int[] _took;
    int _wanted;
    string _piece, _cargo;

    [Params(4, 32, 128)]
    public int Stacks { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var rnd = new System.Random(2026);
        _available = new int[Stacks];
        for (int i = 0; i < Stacks; i++)
        {
            _available[i] = rnd.Next(0, 50);
        }

        _wanted = Stacks * 10;
        _took = PackPlan.Allocate(_available, _wanted);
        _piece = "stone_wall_1x1";
        _cargo = CargoNames.CargoFor(_piece);
    }

    [Benchmark]
    public int Allocate() => PackPlan.Allocate(_available, _wanted).Length;

    [Benchmark]
    public bool Covers() => PackPlan.Covers(_available, _wanted);

    [Benchmark]
    public int Shortfall() => PackPlan.Shortfall(_wanted, _took);

    [Benchmark]
    public string CargoNamesRoundTrip() => CargoNames.PieceOf(CargoNames.CargoFor(_piece)) + (CargoNames.IsCargo(_cargo) ? "" : "!");
}
