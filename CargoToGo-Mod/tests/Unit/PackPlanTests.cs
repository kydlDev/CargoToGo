using System;
using System.Linq;
using FsCheck;

namespace CargoToGo.Tests.Unit
{
    /// <summary>The material split of a cargo: nothing is invented, nothing is over-drawn, earlier sources are used first.</summary>
    [TestFixture]
    public class PackPlanTests
    {
        static readonly Gen<int[]> Sources = Gen.Choose(0, 6).SelectMany(n => Gen.ArrayOf(n, Gen.Choose(-5, 200)));
        static readonly Gen<int> Wanted = Gen.Choose(-5, 400);

        [Test]
        public void Allocate_UsesEarlierSourcesFirst()
        {
            Assert.That(PackPlan.Allocate(new[] { 10, 10, 10 }, 15), Is.EqualTo(new[] { 10, 5, 0 }));
        }

        [Test]
        public void Allocate_NotEnough_TakesEverythingThereIs()
        {
            Assert.That(PackPlan.Allocate(new[] { 3, 4 }, 50), Is.EqualTo(new[] { 3, 4 }));
        }

        [Test]
        public void Allocate_SkipsEmptyAndNegativeSources()
        {
            Assert.That(PackPlan.Allocate(new[] { 0, -7, 5 }, 3), Is.EqualTo(new[] { 0, 0, 3 }));
        }

        [Test]
        public void Allocate_NothingWanted_TakesNothing()
        {
            Assert.That(PackPlan.Allocate(new[] { 5, 5 }, 0), Is.EqualTo(new[] { 0, 0 }));
            Assert.That(PackPlan.Allocate(new[] { 5, 5 }, -3), Is.EqualTo(new[] { 0, 0 }));
        }

        [Test]
        public void Allocate_NoSources_IsEmpty()
        {
            Assert.That(PackPlan.Allocate(null, 5), Is.Empty);
            Assert.That(PackPlan.Allocate(new int[0], 5), Is.Empty);
        }

        [Test]
        public void Covers_BoundaryIsInclusive()
        {
            Assert.That(PackPlan.Covers(new[] { 2, 3 }, 5), Is.True);
            Assert.That(PackPlan.Covers(new[] { 2, 3 }, 6), Is.False);
        }

        [Test]
        public void Covers_NothingWanted_IsAlwaysTrue_EvenWithoutSources()
        {
            Assert.That(PackPlan.Covers(null, 0), Is.True);
            Assert.That(PackPlan.Covers(new int[0], -1), Is.True);
            Assert.That(PackPlan.Covers(null, 1), Is.False);
        }

        [Test]
        public void Covers_NegativeCountsDoNotCancelRealOnes()
        {
            Assert.That(PackPlan.Covers(new[] { 10, -100 }, 10), Is.True);
        }

        [Test]
        public void Covers_HugeCounts_DoNotOverflow()
        {
            Assert.That(PackPlan.Covers(new[] { int.MaxValue, int.MaxValue }, 5), Is.True);
        }

        [Test]
        public void Shortfall_IsWhatIsMissing_NeverNegative()
        {
            Assert.That(PackPlan.Shortfall(10, new[] { 4, 3 }), Is.EqualTo(3));
            Assert.That(PackPlan.Shortfall(10, new[] { 10 }), Is.EqualTo(0));
            Assert.That(PackPlan.Shortfall(10, new[] { 99 }), Is.EqualTo(0));
            Assert.That(PackPlan.Shortfall(10, null), Is.EqualTo(10));
        }

        [Test]
        public void Property_Allocate_NeverOverdraws_AndTakesExactlyMinOfWantedAndTotal()
        {
            Props.Check(Prop.ForAll(Sources.ToArbitrary(), Wanted.ToArbitrary(), (avail, wanted) =>
            {
                var take = PackPlan.Allocate(avail, wanted);
                var total = avail.Where(a => a > 0).Sum();
                var ok = take.Length == avail.Length;
                for (var i = 0; ok && i < take.Length; i++) ok = take[i] >= 0 && take[i] <= Math.Max(0, avail[i]);
                return ok && take.Sum() == Math.Max(0, Math.Min(wanted, total));
            }));
        }

        [Test]
        public void Property_Allocate_IsGreedy_NoSourceIsTouchedWhileAnEarlierOneStillHasSome()
        {
            Props.Check(Prop.ForAll(Sources.ToArbitrary(), Wanted.ToArbitrary(), (avail, wanted) =>
            {
                var take = PackPlan.Allocate(avail, wanted);
                for (var i = 0; i < take.Length; i++)
                    for (var j = i + 1; j < take.Length; j++)
                        if (take[j] > 0 && take[i] < Math.Max(0, avail[i])) return false;
                return true;
            }));
        }

        [Test]
        public void Property_CoversMatchesAllocate_AndShortfallIsZeroExactlyWhenCovered()
        {
            Props.Check(Prop.ForAll(Sources.ToArbitrary(), Wanted.ToArbitrary(), (avail, wanted) =>
            {
                var take = PackPlan.Allocate(avail, wanted);
                var covered = PackPlan.Covers(avail, wanted);
                return covered == (PackPlan.Shortfall(wanted, take) == 0);
            }));
        }
    }

    [TestFixture]
    public class CargoNamesTests
    {
        [Test]
        public void CargoFor_ThenPieceOf_RoundTrips()
        {
            Assert.That(CargoNames.PieceOf(CargoNames.CargoFor("piece_workbench")), Is.EqualTo("piece_workbench"));
            Assert.That(CargoNames.CargoFor("stone_wall_2x1"), Is.EqualTo("CC_Cargo_stone_wall_2x1"));
        }

        [Test]
        public void IsCargo_NeedsThePrefixAndAPieceName()
        {
            Assert.That(CargoNames.IsCargo("CC_Cargo_x"), Is.True);
            Assert.That(CargoNames.IsCargo("CC_Cargo_"), Is.False);
            Assert.That(CargoNames.IsCargo("Wood"), Is.False);
            Assert.That(CargoNames.IsCargo(""), Is.False);
            Assert.That(CargoNames.IsCargo(null), Is.False);
        }

        [Test]
        public void IsCargo_IsCaseSensitive_AndNeedsThePrefixAtTheStart()
        {
            Assert.That(CargoNames.IsCargo("cc_cargo_x"), Is.False);
            Assert.That(CargoNames.IsCargo("X_CC_Cargo_x"), Is.False);
        }

        [Test]
        public void PieceOf_NotACargo_IsNull_NeverThrows()
        {
            Assert.That(CargoNames.PieceOf("Wood"), Is.Null);
            Assert.That(CargoNames.PieceOf("CC_"), Is.Null);
            Assert.That(CargoNames.PieceOf(null), Is.Null);
            Assert.That(CargoNames.PieceOf("CC_Cargo_"), Is.Null);
        }

        [Test]
        public void PieceOf_KeepsPrefixLikeTextInsideThePieceName()
        {
            Assert.That(CargoNames.PieceOf("CC_Cargo_CC_Cargo_x"), Is.EqualTo("CC_Cargo_x"));
        }

        [Test]
        public void Property_AnyPieceName_RoundTrips()
        {
            Props.Check(Prop.ForAll(Arb.From(Gen.Elements("a", "Z", "piece_x", "wall 2x1", "_", "CC_Cargo_", "ä")), name =>
                CargoNames.PieceOf(CargoNames.CargoFor(name)) == name));
        }
    }
}
