# CargoToGo - test coverage (spec 12)

**Automated** = an automated test that ran green; **Manual** = needs a person (`CargoToGo-Manual`); **Gap** = not covered.
Status "Automated" is only true after the TestLab run named in `_agent\TESTLOG.md`.

| Feature / requirement | Unit | Integration | TestLab | Manual | Status |
|---|---|---|---|---|---|
| Loads with its exact KydlCore, spec build, no errors | - | ManifestTests, ArchitectureRulesTests | startup-clean | - | Automated |
| Five features run, hub tab declared | - | ArchitectureRulesTests | features-running, hub-page | hub-look | Automated + Manual |
| Settings layout and defaults | Settings tests | SettingsTests, SuiteRulesTests | (configOverrides) | hub-look | Automated |
| Pack takes materials, unpack gives them back | Rules | - | pack-unpack | build-menu | Automated + Manual |
| A cargo stays in the inventory (chest refuses it) | Rules | - | cargo-stays-in-inventory | - | Automated |
| Death: cargo in the grave, survives reload, no ground drop, take-all | - | - | death-gravestone | build-menu | Automated + Manual |
| Pack from nearby chests, AzuCraftyBoxes | Rules | - | - | - | Gap (needs a chest + AzuCraftyBoxes setup) |
| Alt + destroy repack, Cargos tab, key hints | - | - | - | build-menu | Manual |