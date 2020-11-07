using System;
using System.Linq;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class PreferencesScreen : PseudoScreen
    {
        private static readonly PreferencesScreen Instance = new PreferencesScreen();

        public override void Build(ImGui gui)
        {
            BuildHeader(gui, "Preferences");
            gui.BuildText("Unit of time:", Font.subheader);
            var prefs = Project.current.preferences;
            using (gui.EnterRow())
            {
                if (gui.BuildRadioButton("Second", prefs.time == 1))
                    prefs.RecordUndo(true).time = 1;
                if (gui.BuildRadioButton("Minute", prefs.time == 60))
                    prefs.RecordUndo(true).time = 60;
                if (gui.BuildRadioButton("Hour", prefs.time == 3600))
                    prefs.RecordUndo(true).time = 3600;
                if (gui.BuildRadioButton("Custom", prefs.time != 1 && prefs.time != 60 && prefs.time != 3600))
                    prefs.RecordUndo(true).time = 0;
                if (gui.BuildTextInput(prefs.time.ToString(), out var newTime, null, delayed: true) && int.TryParse(newTime, out var parsed))
                    prefs.RecordUndo(true).time = parsed;
            }
            gui.AllocateSpacing(1f);
            gui.BuildText("Item production/consumption:", Font.subheader);
            BuildUnitPerTime(gui, false, prefs);
            gui.BuildText("Fluid production/consumption:", Font.subheader);
            BuildUnitPerTime(gui, true, prefs);
            
            ChoiceObject(gui, "Default belt:", Database.allBelts, prefs.defaultBelt, s =>
            {
                prefs.RecordUndo().defaultBelt = s;
                gui.Rebuild();
            });
            ChoiceObject(gui, "Default inserter:", Database.allInserters, prefs.defaultInserter, s =>
            {
                prefs.RecordUndo().defaultInserter = s;
                gui.Rebuild();
            });
            
            using (gui.EnterRow())
            {
                gui.BuildText("Inserter capacity:", topOffset:0.5f);
                if (gui.BuildTextInput(prefs.inserterCapacity.ToString(), out var newText2, null, Icon.None, true) && int.TryParse(newText2, out var capacity2))
                    prefs.RecordUndo().inserterCapacity = capacity2;
            }

            if (gui.BuildButton("Done"))
                Close();
            if (prefs.justChanged)
                MainScreen.Instance.RebuildProjectView();
        }

        private void ChoiceObject<T>(ImGui gui, string text, T[] list, T current, Action<T> select) where T:FactorioObject
        {
            using (gui.EnterRow())
            {
                gui.BuildText(text, topOffset:0.5f);
                if (gui.BuildFactorioObjectButtonWithText(current))
                    gui.BuildObjectSelectDropDown(list, DataUtils.DefaultOrdering, select, text);
            }
        }

        private void BuildUnitPerTime(ImGui gui, bool fluid, ProjectPreferences preferences)
        {
            var unit = fluid ? preferences.fluidUnit : preferences.itemUnit;
            var newUnit = unit;
            if (gui.BuildRadioButton("Simple Amount"+preferences.GetPerTimeUnit().suffix, unit == 0f))
                newUnit = 0f;
            using (gui.EnterRow())
            {
                if (gui.BuildRadioButton("Custom: 1 unit equals", unit != 0f))
                    newUnit = 1f;
                gui.AllocateSpacing();
                gui.allocator = RectAllocator.RightRow;
                if (!fluid)
                {
                    if (gui.BuildButton("Set from belt"))
                    {
                        gui.BuildObjectSelectDropDown<EntityBelt>(Database.allBelts, DataUtils.DefaultOrdering, setBelt =>
                        {
                            preferences.RecordUndo(true);
                            preferences.itemUnit = setBelt.beltItemsPerSecond;
                        }, "Select belt", extra:b => DataUtils.FormatAmount(b.beltItemsPerSecond, UnitOfMeasure.PerSecond));
                    }
                }
                gui.BuildText("per second");
                if (gui.BuildTextInput(DataUtils.FormatAmount(unit, UnitOfMeasure.None), out var updated, null, Icon.None, true) &&
                    DataUtils.TryParseAmount(updated, out var parsed, UnitOfMeasure.None))
                    newUnit = parsed;
            }
            gui.AllocateSpacing(1f);

            if (newUnit != unit)
            {
                preferences.RecordUndo(true);
                if (fluid)
                    preferences.fluidUnit = newUnit;
                else preferences.itemUnit = newUnit;
            }
        }

        public static void Show() => MainScreen.Instance.ShowPseudoScreen(Instance);
    }
}