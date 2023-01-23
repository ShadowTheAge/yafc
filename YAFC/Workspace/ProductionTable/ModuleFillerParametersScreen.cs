using System;
using System.Linq;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class ModuleFillerParametersScreen : PseudoScreen
    {
        private static readonly ModuleFillerParametersScreen Instance = new ModuleFillerParametersScreen();
        private ModuleFillerParameters modules;
        
        private static readonly float ModulesMinPayback = MathF.Log(600f);
        private static readonly float ModulesMaxPayback = MathF.Log(3600f * 120f);

        public static void Show(ModuleFillerParameters parameters)
        {
            Instance.modules = parameters;
            MainScreen.Instance.ShowPseudoScreen(Instance);
        }
        
        public static void BuildSimple(ImGui gui, ModuleFillerParameters modules)
        {
            var payback = modules.autoFillPayback;
            var modulesLog = MathUtils.LogarithmicToLinear(payback, ModulesMinPayback, ModulesMaxPayback);
            if (gui.BuildSlider(modulesLog, out var newValue))
            {
                payback = MathUtils.LinearToLogarithmic(newValue, ModulesMinPayback, ModulesMaxPayback, 0f, float.MaxValue); // JSON can't handle infinities
                modules.RecordUndo().autoFillPayback = payback;
            }

            if (payback <= 0f)
                gui.BuildText("Use no modules");
            else if (payback >= float.MaxValue)
                gui.BuildText("Use best modules");
            else gui.BuildText("Modules payback estimate: "+DataUtils.FormatTime(payback), wrap:true);
        }
        
        public override void Build(ImGui gui)
        {
            BuildHeader(gui, "Module autofill parameters");
            BuildSimple(gui, modules);
            if (gui.BuildCheckBox("Fill modules in miners", modules.fillMiners, out var newFill))
                modules.RecordUndo().fillMiners = newFill;
            gui.AllocateSpacing();
            gui.BuildText("Filler module:", Font.subheader);
            gui.BuildText("Use this module when aufofill doesn't add anything (for example when productivity modules doesn't fit)", wrap:true);
            if (gui.BuildFactorioObjectButtonWithText(modules.fillerModule) == Click.Left)
            {
                SelectSingleObjectPanel.Select(Database.allModules, "Select filler module", select => { modules.RecordUndo().fillerModule = select; }, true);
            }
            
            gui.AllocateSpacing();
            gui.BuildText("Beacons & beacon modules:", Font.subheader);
            if (gui.BuildFactorioObjectButtonWithText(modules.beacon) == Click.Left)
            {
                SelectSingleObjectPanel.Select(Database.allBeacons, "Select beacon", select =>
                {
                    modules.RecordUndo();
                    modules.beacon = select;
                    if (modules.beaconModule != null && (modules.beacon == null || !modules.beacon.CanAcceptModule(modules.beaconModule.module)))
                        modules.beaconModule = null;
                    gui.Rebuild();
                }, true);
            }

            if (gui.BuildFactorioObjectButtonWithText(modules.beaconModule) == Click.Left)
                SelectSingleObjectPanel.Select(Database.allModules.Where(x => modules.beacon?.CanAcceptModule(x.module) ?? false), "Select module for beacon", select => { modules.RecordUndo().beaconModule = select; }, true);

            using (gui.EnterRow())
            {
                gui.BuildText("Beacons per building: ");
                if (gui.BuildTextInput(modules.beaconsPerBuilding.ToString(), out var newText, null, Icon.None, true, new Padding(0.5f, 0f)) && 
                    int.TryParse(newText, out var newAmount) && newAmount > 0)
                    modules.RecordUndo().beaconsPerBuilding = newAmount;
            }
            gui.BuildText("Please note that beacons themself are not part of the calculation", wrap:true);

            gui.AllocateSpacing();
            gui.BuildText("Override beacon count:", Font.subheader);
            gui.BuildText("(right-click to remove)", topOffset: -0.5f);
            using (gui.EnterRow())
                foreach (var crafter in modules.overrideBeaconsPerBuilding)
                    switch (gui.BuildFactorioObjectWithEditableAmount(crafter.Key, crafter.Value, UnitOfMeasure.None, out float newAmount))
                    {
                        case GoodsWithAmountEvent.RightButtonClick:
                            modules.RecordUndo().overrideBeaconsPerBuilding.Remove(crafter.Key);
                            return;
                        case GoodsWithAmountEvent.TextEditing:
                            modules.RecordUndo().overrideBeaconsPerBuilding[crafter.Key] = (int)newAmount;
                            return;
                    }

            using (gui.EnterRow(allocator: RectAllocator.Center))
                if (gui.BuildButton("Add override"))
                    SelectMultiObjectPanel.Select(Database.allCrafters.Where(x => x.allowedEffects != AllowedEffects.None && !modules.overrideBeaconsPerBuilding.ContainsKey(x)), "Add exception(s) for:",
                        ec => modules.RecordUndo().overrideBeaconsPerBuilding[ec] = modules.beaconsPerBuilding);

            gui.AllocateSpacing();
            using (gui.EnterRow())
            {
                gui.BuildText("Mining productivity bonus (project-wide setting): ");
                if (gui.BuildTextInput(DataUtils.FormatAmount(Project.current.settings.miningProductivity, UnitOfMeasure.Percent), out var newText, null, Icon.None, true, new Padding(0.5f, 0f)) &&
                    DataUtils.TryParseAmount(newText, out var newAmount, UnitOfMeasure.Percent))
                    Project.current.settings.RecordUndo().miningProductivity = newAmount;
            }
            
            if (gui.BuildButton("Done"))
                Close();
            if (Project.current.settings.justChanged)
                Project.current.RecalculateDisplayPages();
        }
    }
}