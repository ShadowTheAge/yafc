using System;
using YAFC.Model;
using YAFC.UI;

namespace YAFC {
    public class PreferencesScreen : PseudoScreen {
        private static readonly PreferencesScreen Instance = new PreferencesScreen();

        public override void Build(ImGui gui) {
            BuildHeader(gui, "Preferences");
            gui.BuildText("Unit of time:", Font.subheader);
            var prefs = Project.current.preferences;
            var settings = Project.current.settings;
            using (gui.EnterRow()) {
                if (gui.BuildRadioButton("Second", prefs.time == 1))
                    prefs.RecordUndo(true).time = 1;
                if (gui.BuildRadioButton("Minute", prefs.time == 60))
                    prefs.RecordUndo(true).time = 60;
                if (gui.BuildRadioButton("Hour", prefs.time == 3600))
                    prefs.RecordUndo(true).time = 3600;
                if (gui.BuildRadioButton("Custom", prefs.time != 1 && prefs.time != 60 && prefs.time != 3600))
                    prefs.RecordUndo(true).time = 0;
                if (gui.BuildIntegerInput(prefs.time, out var newTime))
                    prefs.RecordUndo(true).time = newTime;
            }
            gui.AllocateSpacing(1f);
            gui.BuildText("Item production/consumption:", Font.subheader);
            BuildUnitPerTime(gui, false, prefs);
            gui.BuildText("Fluid production/consumption:", Font.subheader);
            BuildUnitPerTime(gui, true, prefs);

            ChoiceObject(gui, "Default belt:", Database.allBelts, prefs.defaultBelt, s => {
                prefs.RecordUndo().defaultBelt = s;
                gui.Rebuild();
            });
            ChoiceObject(gui, "Default inserter:", Database.allInserters, prefs.defaultInserter, s => {
                prefs.RecordUndo().defaultInserter = s;
                gui.Rebuild();
            });

            using (gui.EnterRow()) {
                gui.BuildText("Inserter capacity:", topOffset: 0.5f);
                if (gui.BuildIntegerInput(prefs.inserterCapacity, out var newCapacity))
                    prefs.RecordUndo().inserterCapacity = newCapacity;
            }

            using (gui.EnterRow()) {
                gui.BuildText("Reactor layout:", topOffset: 0.5f);
                if (gui.BuildTextInput(settings.reactorSizeX + "x" + settings.reactorSizeY, out var newSize, null, delayed: true)) {
                    var px = newSize.IndexOf("x", StringComparison.Ordinal);
                    if (px < 0 && int.TryParse(newSize, out var value)) {
                        settings.RecordUndo().reactorSizeX = value;
                        settings.reactorSizeY = value;
                    }
                    else if (int.TryParse(newSize[..px], out var sizeX) && int.TryParse(newSize[(px + 1)..], out var sizeY)) {
                        settings.RecordUndo().reactorSizeX = sizeX;
                        settings.reactorSizeY = sizeY;
                    }
                }
            }
            ChoiceObject(gui, "Target technology for cost analysis: ", Database.technologies.all, prefs.targetTechnology, x => {
                prefs.RecordUndo().targetTechnology = x;
                gui.Rebuild();
            }, width: 25f);

            if (gui.BuildButton("Done"))
                Close();
            if (prefs.justChanged)
                MainScreen.Instance.RebuildProjectView();
            if (settings.justChanged)
                Project.current.RecalculateDisplayPages();
        }

        /// <summary>Add a GUI element that opens a popup to allow the user to choose from the <paramref name="list"/>, which triggers <paramref name="select"/>.</summary>
        /// <param name="text">Label to show.</param>
        /// <param name="width">Width of the popup. Make sure it is wide enough to fit text!</param>
        private void ChoiceObject<T>(ImGui gui, string text, T[] list, T current, Action<T> select, float width = 20f) where T : FactorioObject {
            using (gui.EnterRow()) {
                gui.BuildText(text, topOffset: 0.5f);
                if (gui.BuildFactorioObjectButtonWithText(current))
                    gui.BuildObjectSelectDropDown(list, DataUtils.DefaultOrdering, select, text, width: width);
            }
        }

        private void BuildUnitPerTime(ImGui gui, bool fluid, ProjectPreferences preferences) {
            var unit = fluid ? preferences.fluidUnit : preferences.itemUnit;
            var newUnit = unit;
            if (gui.BuildRadioButton("Simple Amount" + preferences.GetPerTimeUnit().suffix, unit == 0f))
                newUnit = 0f;
            using (gui.EnterRow()) {
                if (gui.BuildRadioButton("Custom: 1 unit equals", unit != 0f))
                    newUnit = 1f;
                gui.AllocateSpacing();
                gui.allocator = RectAllocator.RightRow;
                if (!fluid) {
                    if (gui.BuildButton("Set from belt")) {
                        gui.BuildObjectSelectDropDown<EntityBelt>(Database.allBelts, DataUtils.DefaultOrdering, setBelt => {
                            preferences.RecordUndo(true);
                            preferences.itemUnit = setBelt.beltItemsPerSecond;
                        }, "Select belt", extra: b => DataUtils.FormatAmount(b.beltItemsPerSecond, UnitOfMeasure.PerSecond));
                    }
                }
                gui.BuildText("per second");
                if (gui.BuildTextInput(DataUtils.FormatAmount(unit, UnitOfMeasure.None), out var updated, null, Icon.None, true) &&
                    DataUtils.TryParseAmount(updated, out var parsed, UnitOfMeasure.None))
                    newUnit = parsed;
            }
            gui.AllocateSpacing(1f);

            if (newUnit != unit) {
                preferences.RecordUndo(true);
                if (fluid)
                    preferences.fluidUnit = newUnit;
                else preferences.itemUnit = newUnit;
            }
        }

        public static void Show() => MainScreen.Instance.ShowPseudoScreen(Instance);
    }
}
