using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class ModuleTemplateConfiguration : PseudoScreen
    {
        private static readonly ModuleTemplateConfiguration Instance = new ModuleTemplateConfiguration();
        private readonly VirtualScrollList<ProjectModuleTemplate> templateList;
        private ProjectModuleTemplate pageToDelete;
        private string newPageName = "";

        public ModuleTemplateConfiguration()
        {
            templateList = new VirtualScrollList<ProjectModuleTemplate>(30, new Vector2(20, 2.5f), Drawer,
                reorder: (from, to) => Project.current.RecordUndo().sharedModuleTemplates.MoveListElementIndex(from, to));
        }

        public static void Show()
        {
            Instance.RefreshList();
            MainScreen.Instance.ShowPseudoScreen(Instance);
        }

        private void RefreshList()
        {
            templateList.data = Project.current.sharedModuleTemplates;
            Rebuild();
        }

        private void Drawer(ImGui gui, ProjectModuleTemplate element, int index)
        {
            gui.allocator = RectAllocator.RightRow;
            if (gui.BuildButton(Icon.Delete))
            {
                pageToDelete = element;
                Rebuild();
            }

            if (gui.BuildButton(Icon.Copy))
            {
                var copy = JsonUtils.Copy(element, element.owner, null);
                if (copy != null)
                {
                    element.owner.RecordUndo().sharedModuleTemplates.Add(copy);
                    ModuleCustomisationScreen.Show(copy);
                }
            }
            if (gui.BuildButton(Icon.Edit))
                ModuleCustomisationScreen.Show(element);
            gui.allocator = RectAllocator.LeftRow;
            if (element.icon != null)
                gui.BuildFactorioObjectIcon(element.icon);
            gui.BuildText(element.name);
        }

        public override void Activated()
        {
            base.Activated();
            templateList.RebuildContents();
        }

        public override void Build(ImGui gui)
        {
            BuildHeader(gui, "Module templates");
            templateList.Build(gui);
            if (pageToDelete != null)
            {
                Project.current.RecordUndo().sharedModuleTemplates.Remove(pageToDelete);
                RefreshList();
                pageToDelete = null;
            }
            using (gui.EnterRow(0.5f, RectAllocator.RightRow))
            {
                if (gui.BuildButton("Create", active: newPageName != ""))
                {
                    var template = new ProjectModuleTemplate(Project.current) {name = newPageName};
                    Project.current.RecordUndo().sharedModuleTemplates.Add(template);
                    newPageName = "";
                    ModuleCustomisationScreen.Show(template);
                    RefreshList();
                }

                gui.RemainingRow().BuildTextInput(newPageName, out newPageName, "Create new template");
            }
        }
    }
}