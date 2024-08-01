using System;
using Yafc.Model;
using Yafc.UI;

namespace Yafc {
    public class PreferencesScreen : PseudoScreen {
        private static readonly PreferencesScreen Instance = new PreferencesScreen();

        public override void Build(ImGui gui) {
            BuildHeader(gui, "Preferences");
            gui.BuildText("Unit of time:", Font.subheader);
            var prefs = Project.current.preferences;
            var settings = Project.current.settings;
            using (gui.EnterRow()) {
                if (gui.BuildRadioButton("Second", prefs.time == 1)) {
                    prefs.RecordUndo(true).time = 1;
                }

                if (gui.BuildRadioButton("Minute", prefs.time == 60)) {
                    prefs.RecordUndo(true).time = 60;
                }

                if (gui.BuildRadioButton("Hour", prefs.time == 3600)) {
                    prefs.RecordUndo(true).time = 3600;
                }

                if (gui.BuildRadioButton("Custom", prefs.time is not 1 and not 60 and not 3600)) {
                    prefs.RecordUndo(true).time = 0;
                }

                if (gui.BuildIntegerInput(prefs.time, out int newTime)) {
                    prefs.RecordUndo(true).time = newTime;
                }
            }
            gui.AllocateSpacing(1f);
            gui.BuildText("Item production/consumption:", Font.subheader);
            BuildUnitPerTime(gui, false, prefs);
            gui.BuildText("Fluid production/consumption:", Font.subheader);
            BuildUnitPerTime(gui, true, prefs);

            using (gui.EnterRowWithHelpIcon("0 for off, 100% for old default")) {
                gui.BuildText("Pollution cost modifier", topOffset: 0.5f);
                DisplayAmount amount = new(settings.PollutionCostModifier, UnitOfMeasure.Percent);
                if (gui.BuildFloatInput(amount, new Padding(0.5f))) {
                    settings.RecordUndo().PollutionCostModifier = amount.Value;
                    gui.Rebuild();
                }
            }

            using (gui.EnterRowWithHelpIcon("Some mod icons have little or no transparency, hiding the background color. This setting reduces the size of icons that could hide link information.")) {
                gui.BuildText("Display scale for linkable icons", topOffset: 0.5f);
                DisplayAmount amount = new(prefs.iconScale, UnitOfMeasure.Percent);
                if (gui.BuildFloatInput(amount, new Padding(0.5f)) && amount.Value > 0 && amount.Value <= 1) {
                    prefs.RecordUndo().iconScale = amount.Value;
                    gui.Rebuild();
                }
            }

            ChooseObject(gui, "Default belt:", Database.allBelts, prefs.defaultBelt, s => {
                prefs.RecordUndo().defaultBelt = s;
                gui.Rebuild();
            });
            ChooseObject(gui, "Default inserter:", Database.allInserters, prefs.defaultInserter, s => {
                prefs.RecordUndo().defaultInserter = s;
                gui.Rebuild();
            });

            using (gui.EnterRow()) {
                gui.BuildText("Inserter capacity:", topOffset: 0.5f);
                if (gui.BuildIntegerInput(prefs.inserterCapacity, out int newCapacity)) {
                    prefs.RecordUndo().inserterCapacity = newCapacity;
                }
            }

            using (gui.EnterRow()) {
                gui.BuildText("Reactor layout:", topOffset: 0.5f);
                if (gui.BuildTextInput(settings.reactorSizeX + "x" + settings.reactorSizeY, out string newSize, null, delayed: true)) {
                    int px = newSize.IndexOf("x", StringComparison.Ordinal);
                    if (px < 0 && int.TryParse(newSize, out int value)) {
                        settings.RecordUndo().reactorSizeX = value;
                        settings.reactorSizeY = value;
                    }
                    else if (int.TryParse(newSize[..px], out int sizeX) && int.TryParse(newSize[(px + 1)..], out int sizeY)) {
                        settings.RecordUndo().reactorSizeX = sizeX;
                        settings.reactorSizeY = sizeY;
                    }
                }
            }
            ChooseObjectWithNone(gui, "Target technology for cost analysis: ", Database.technologies.all, prefs.targetTechnology, x => {
                prefs.RecordUndo().targetTechnology = x;
                gui.Rebuild();
            }, width: 25f);

            if (gui.BuildButton("Done")) {
                Close();
            }

            if (prefs.justChanged) {
                MainScreen.Instance.RebuildProjectView();
            }

            if (settings.justChanged) {
                Project.current.RecalculateDisplayPages();
            }
        }

        protected override void ReturnPressed() => Close();

        /// <summary>Add a GUI element that opens a popup to allow the user to choose from the <paramref name="list"/>, which triggers <paramref name="selectItem"/>.</summary>
        /// <param name="text">Label to show.</param>
        /// <param name="width">Width of the popup. Make sure it is wide enough to fit text!</param>
        private static void ChooseObject<T>(ImGui gui, string text, T[] list, T? current, Action<T> selectItem, float width = 20f) where T : FactorioObject {
            using (gui.EnterRow()) {
                gui.BuildText(text, topOffset: 0.5f);
                if (gui.BuildFactorioObjectButtonWithText(current) == Click.Left) {
                    gui.BuildObjectSelectDropDown(list, DataUtils.DefaultOrdering, selectItem, text, width: width);
                }
            }
        }

        /// <summary>Add a GUI element that opens a popup to allow the user to choose from the <paramref name="list"/>, which triggers <paramref name="selectItem"/>.
        /// An additional "clear" or "none" option will also be displayed.</summary>
        /// <param name="text">Label to show.</param>
        /// <param name="width">Width of the popup. Make sure it is wide enough to fit text!</param>
        private static void ChooseObjectWithNone<T>(ImGui gui, string text, T[] list, T? current, Action<T?> selectItem, float width = 20f) where T : FactorioObject {
            using (gui.EnterRow()) {
                gui.BuildText(text, topOffset: 0.5f);
                if (gui.BuildFactorioObjectButtonWithText(current) == Click.Left) {
                    gui.BuildObjectSelectDropDownWithNone(list, DataUtils.DefaultOrdering, selectItem, text, width: width);
                }
            }
        }

        private void BuildUnitPerTime(ImGui gui, bool fluid, ProjectPreferences preferences) {
            DisplayAmount unit = fluid ? preferences.fluidUnit : preferences.itemUnit;
            if (gui.BuildRadioButton("Simple Amount" + preferences.GetPerTimeUnit().suffix, unit == 0f)) {
                unit = 0f;
            }

            using (gui.EnterRow()) {
                if (gui.BuildRadioButton("Custom: 1 unit equals", unit != 0f)) {
                    unit = 1f;
                }

                gui.AllocateSpacing();
                gui.allocator = RectAllocator.RightRow;
                if (!fluid) {
                    if (gui.BuildButton("Set from belt")) {
                        gui.BuildObjectSelectDropDown<EntityBelt>(Database.allBelts, DataUtils.DefaultOrdering, setBelt => {
                            _ = preferences.RecordUndo(true);
                            preferences.itemUnit = setBelt.beltItemsPerSecond;
                        }, "Select belt", extra: b => DataUtils.FormatAmount(b.beltItemsPerSecond, UnitOfMeasure.PerSecond));
                    }
                }
                gui.BuildText("per second");
                _ = gui.BuildFloatInput(unit, new Padding(.5f));
            }
            gui.AllocateSpacing(1f);

            if (fluid) {
                if (preferences.fluidUnit != unit.Value) {
                    preferences.RecordUndo(true).fluidUnit = unit.Value;
                }
            }
            else {
                if (preferences.itemUnit != unit.Value) {
                    preferences.RecordUndo(true).itemUnit = unit.Value;
                }
            }
        }

        public static void Show() => _ = MainScreen.Instance.ShowPseudoScreen(Instance);
    }
}
