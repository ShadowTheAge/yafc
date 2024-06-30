using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Yafc.Model;
using Yafc.UI;

namespace Yafc {
    public class ModuleFillerParametersScreen : PseudoScreen {
        private static readonly float ModulesMinPayback = MathF.Log(600f);
        private static readonly float ModulesMaxPayback = MathF.Log(3600f * 120f);
        private readonly ModuleFillerParameters modules;
        private readonly VirtualScrollList<KeyValuePair<EntityCrafter, BeaconOverrideConfiguration>> overrideList;

        public static void Show(ModuleFillerParameters parameters) => _ = MainScreen.Instance.ShowPseudoScreen(new ModuleFillerParametersScreen(parameters));

        private ModuleFillerParametersScreen(ModuleFillerParameters modules) {
            this.modules = modules;
            overrideList = new(11, new(3.25f, 4.5f), ListDrawer, MainScreen.Instance.InputSystem, collapsible: true) { data = [.. modules.overrideCrafterBeacons] };
        }

        /// <summary>
        /// Draw one item in the per-crafter beacon override display.
        /// </summary>
        private void ListDrawer(ImGui gui, KeyValuePair<EntityCrafter, BeaconOverrideConfiguration> element, int index) {
            (EntityCrafter crafter, BeaconOverrideConfiguration config) = element;
            GoodsWithAmountEvent click = gui.BuildFactorioObjectWithEditableAmount(crafter, config.beaconCount, UnitOfMeasure.None, out float newAmount, allowScroll: false);
            gui.DrawIcon(new(gui.lastRect.X, gui.lastRect.Y, 1.25f, 1.25f), config.beacon.icon, SchemeColor.Source);
            gui.DrawIcon(new(gui.lastRect.TopRight - new Vector2(1.25f, 0), new Vector2(1.25f, 1.25f)), config.beaconModule.icon, SchemeColor.Source);
            switch (click) {
                case GoodsWithAmountEvent.LeftButtonClick:
                    SelectSingleObjectPanel.SelectWithNone(Database.allBeacons, "Select beacon", select => {
                        if (select is null) {
                            modules.RecordUndo().overrideCrafterBeacons.Remove(crafter);
                        }
                        else {
                            modules.RecordUndo().overrideCrafterBeacons[crafter].beacon = select;
                        }
                    });
                    break;
                case GoodsWithAmountEvent.RightButtonClick:
                    SelectSingleObjectPanel.SelectWithNone(Database.allModules.Where(m => modules.overrideCrafterBeacons[crafter].beacon.CanAcceptModule(m.moduleSpecification)), "Select beacon module", select => {
                        if (select is null) {
                            modules.RecordUndo().overrideCrafterBeacons.Remove(crafter);
                        }
                        else {
                            modules.RecordUndo().overrideCrafterBeacons[crafter].beaconModule = select;
                        }
                    });
                    break;
                case GoodsWithAmountEvent.TextEditing:
                    modules.RecordUndo().overrideCrafterBeacons[crafter].beaconCount = (int)newAmount;
                    break;
            }
            overrideList.data = [.. modules.overrideCrafterBeacons];
        }

        /// <summary>
        /// Draw the slider that controls the price of the modules that are automatically selected.
        /// </summary>
        public static void BuildSimple(ImGui gui, ModuleFillerParameters modules) {
            float payback = modules.autoFillPayback;
            float modulesLog = MathUtils.LogarithmicToLinear(payback, ModulesMinPayback, ModulesMaxPayback);
            if (gui.BuildSlider(modulesLog, out float newValue)) {
                payback = MathUtils.LinearToLogarithmic(newValue, ModulesMinPayback, ModulesMaxPayback, 0f, float.MaxValue); // JSON can't handle infinities
                modules.RecordUndo().autoFillPayback = payback;
            }

            if (payback <= 0f) {
                gui.BuildText("Use no modules");
            }
            else if (payback >= float.MaxValue) {
                gui.BuildText("Use best modules");
            }
            else {
                gui.BuildText("Modules payback estimate: " + DataUtils.FormatTime(payback), wrap: true);
            }
        }

        /// <summary>
        /// Draw the full configuration panel, with all the options except the slider.
        /// </summary>
        public override void Build(ImGui gui) {
            EntityBeacon? defaultBeacon = Database.usableBeacons.FirstOrDefault();
            _ = Database.GetDefaultModuleFor(defaultBeacon, out Module? defaultBeaconModule);

            BuildHeader(gui, "Module autofill parameters");
            BuildSimple(gui, modules);
            if (gui.BuildCheckBox("Fill modules in miners", modules.fillMiners, out bool newFill)) {
                modules.RecordUndo().fillMiners = newFill;
            }

            gui.AllocateSpacing();
            gui.BuildText("Filler module:", Font.subheader);
            gui.BuildText("Use this module when aufofill doesn't add anything (for example when productivity modules doesn't fit)", wrap: true);
            if (gui.BuildFactorioObjectButtonWithText(modules.fillerModule) == Click.Left) {
                SelectSingleObjectPanel.SelectWithNone(Database.allModules, "Select filler module", select => { modules.RecordUndo().fillerModule = select; });
            }

            gui.AllocateSpacing();
            gui.BuildText("Beacons & beacon modules:", Font.subheader);
            if (defaultBeacon is null || defaultBeaconModule is null) {
                gui.BuildText("Your mods contain no beacons, or no modules that can be put into beacons.");
            }
            else {
                if (gui.BuildFactorioObjectButtonWithText(modules.beacon) == Click.Left) {
                    SelectSingleObjectPanel.SelectWithNone(Database.allBeacons, "Select beacon", select => {
                        _ = modules.RecordUndo();
                        modules.beacon = select;
                        if (modules.beaconModule != null && (modules.beacon == null || !modules.beacon.CanAcceptModule(modules.beaconModule.moduleSpecification))) {
                            modules.beaconModule = null;
                        }

                        gui.Rebuild();
                    });
                }

                if (gui.BuildFactorioObjectButtonWithText(modules.beaconModule) == Click.Left) {
                    SelectSingleObjectPanel.SelectWithNone(Database.allModules.Where(x => modules.beacon?.CanAcceptModule(x.moduleSpecification) ?? false), "Select module for beacon", select => { modules.RecordUndo().beaconModule = select; });
                }

                using (gui.EnterRow()) {
                    gui.BuildText("Beacons per building: ");
                    if (gui.BuildTextInput(modules.beaconsPerBuilding.ToString(), out string newText, null, Icon.None, true, new Padding(0.5f, 0f)) &&
                        int.TryParse(newText, out int newAmount) && newAmount > 0) {
                        modules.RecordUndo().beaconsPerBuilding = newAmount;
                    }
                }
                gui.BuildText("Please note that beacons themselves are not part of the calculation", wrap: true);

                gui.AllocateSpacing();
                gui.BuildText("Override beacons:", Font.subheader);
                if (modules.overrideCrafterBeacons.Count > 0) {
                    using (gui.EnterGroup(new Padding(1, 0, 0, 0))) {
                        gui.BuildText("Click to change beacon, right-click to change module", topOffset: -0.5f);
                        gui.BuildText("Select the 'none' item in either prompt to remove the override.", topOffset: -0.5f);
                    }
                }
                using (gui.EnterRow()) {
                    foreach ((EntityCrafter crafter, BeaconOverrideConfiguration beaconInfo) in modules.overrideCrafterBeacons) {
                        GoodsWithAmountEvent click = gui.BuildFactorioObjectWithEditableAmount(crafter, beaconInfo.beaconCount, UnitOfMeasure.None, out float newAmount);
                        gui.DrawIcon(new Rect(gui.lastRect.TopLeft, new Vector2(1.25f, 1.25f)), beaconInfo.beacon.icon, SchemeColor.Source);
                        gui.DrawIcon(new Rect(gui.lastRect.TopRight - new Vector2(1.25f, 0), new Vector2(1.25f, 1.25f)), beaconInfo.beaconModule.icon, SchemeColor.Source);
                        switch (click) {
                            case GoodsWithAmountEvent.LeftButtonClick:
                                SelectSingleObjectPanel.SelectWithNone(Database.usableBeacons, "Select beacon", selectedBeacon => {
                                    if (selectedBeacon is null) {
                                        modules.RecordUndo().overrideCrafterBeacons.Remove(crafter);
                                    }
                                    else {
                                        modules.RecordUndo().overrideCrafterBeacons[crafter].beacon = selectedBeacon;
                                        if (!selectedBeacon.CanAcceptModule(modules.overrideCrafterBeacons[crafter].beaconModule.moduleSpecification)) {
                                            _ = Database.GetDefaultModuleFor(selectedBeacon, out Module? module);
                                            modules.overrideCrafterBeacons[crafter].beaconModule = module!; // null-forgiving: Anything from usableBeacons accepts at least one module.
                                        }
                                    }
                                }, noneTooltip: "Click here to remove the current override.");
                                return;
                            case GoodsWithAmountEvent.RightButtonClick:
                                SelectSingleObjectPanel.SelectWithNone(Database.allModules.Where(m => modules.overrideCrafterBeacons[crafter].beacon.CanAcceptModule(m.moduleSpecification)), "Select beacon module", selectedModule => {
                                    if (selectedModule is null) {
                                        modules.RecordUndo().overrideCrafterBeacons.Remove(crafter);
                                    }
                                    else {
                                        modules.RecordUndo().overrideCrafterBeacons[crafter].beaconModule = selectedModule;
                                    }
                                }, noneTooltip: "Click here to remove the current override.");
                                return;
                            case GoodsWithAmountEvent.TextEditing:
                                modules.RecordUndo().overrideCrafterBeacons[crafter].beaconCount = (int)newAmount;
                                return;
                        }
                    }
                }

                using (gui.EnterRow(allocator: RectAllocator.Center)) {
                    if (gui.BuildButton("Add an override for a building type")) {
                        SelectMultiObjectPanel.Select(Database.allCrafters.Where(x => x.allowedEffects != AllowedEffects.None && !modules.overrideCrafterBeacons.ContainsKey(x)), "Add exception(s) for:",
                            crafter => {
                                modules.RecordUndo().overrideCrafterBeacons[crafter] = new BeaconOverrideConfiguration(modules.beacon ?? defaultBeacon, modules.beaconsPerBuilding, modules.beaconModule ?? defaultBeaconModule);
                                overrideList.data = [.. modules.overrideCrafterBeacons];
                            });
                    }
                }
            }

            gui.AllocateSpacing();
            using (gui.EnterRow()) {
                gui.BuildText("Mining productivity bonus (project-wide setting): ");
                if (gui.BuildTextInput(DataUtils.FormatAmount(Project.current.settings.miningProductivity, UnitOfMeasure.Percent), out string newText, null, Icon.None, true, new Padding(0.5f, 0f)) &&
                    DataUtils.TryParseAmount(newText, out float newAmount, UnitOfMeasure.Percent) && newAmount >= 0) {
                    Project.current.settings.RecordUndo().miningProductivity = newAmount;
                }
            }
            using (gui.EnterRow()) {
                gui.BuildText("Research speed bonus (project-wide setting): ");
                if (gui.BuildTextInput(DataUtils.FormatAmount(Project.current.settings.researchSpeedBonus, UnitOfMeasure.Percent), out string newText, null, Icon.None, true, new Padding(0.5f, 0f)) &&
                    DataUtils.TryParseAmount(newText, out float newAmount, UnitOfMeasure.Percent) && newAmount >= 0) {
                    Project.current.settings.RecordUndo().researchSpeedBonus = newAmount;
                }
            }

            if (gui.BuildButton("Done")) {
                Close();
            }

            if (Project.current.settings.justChanged) {
                Project.current.RecalculateDisplayPages();
            }
        }

        protected override void ReturnPressed() => Close();
    }
}
