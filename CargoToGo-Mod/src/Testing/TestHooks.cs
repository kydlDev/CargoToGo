using Kydl.Common;

namespace CargoToGo.Testing
{
    /// <summary>
    /// KydlCore D-084: hooks for DrakesTestLab suites (CargoToGo-Tests/TestLab/suites/CargoToGo). A hook is a static method with [TestHook("name")]; DrakesTestLab finds it by the
    /// attribute's NAME (the mod does not reference DrakesTestLab). Hook names carry the mod id so two mods never clash.
    /// </summary>
    internal static class TestHooks
    {
        /// <summary>All Startup_* / World_* checks of InGameTests: "PASS: ..." or "FAIL: ...".</summary>
        [TestHook("CargoToGo.selftest")]
        static string SelfTest() { return TestLabSupport.RunInGameTests(typeof(InGameTests)); }

        /// <summary>How many cargo items the ObjectDB holds (one per build piece).</summary>
        [TestHook("CargoToGo.cargoItems")]
        static int CargoItems()
        {
            var db = ObjectDB.instance;
            if (db == null) return -1;
            int n = 0;
            foreach (var go in db.m_items) if (go != null && go.name.StartsWith(CargoFactory.PrefabPrefix)) n++;
            return n;
        }
    }
}