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
            templateList = new VirtualScrollList<ProjectModuleTemplate>(30, new Vector2(20, 2.5f), Drawer);
        }

        public static void Show()
        {
            Instance.RefreshList();
            MainScreen.Instance.ShowPseudoScreen(Instance);
        }

        private void RefreshList()
        {
            templateList.data = Project.current.moduleTemplates.Values.ToArray();
            Rebuild();
        }

        private void Drawer(ImGui gui, ProjectModuleTemplate element, int index)
        {
            gui.allocator = RectAllocator.RightRow;
            if (gui.BuildButton(Icon.Close, size: 0.8f))
            {
                pageToDelete = element;
                Rebuild();
            }
            if (gui.RemainingRow().BuildContextMenuButton(element.name, icon:element.icon?.icon ?? default))
                ModuleCustomisationScreen.Show(element);
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
                Project.current.RecordUndo().moduleTemplates.RemoveValue(pageToDelete);
                RefreshList();
                pageToDelete = null;
            }
            using (gui.EnterRow(0.5f, RectAllocator.RightRow))
            {
                if (gui.BuildButton("Create", active: newPageName != ""))
                {
                    var template = new ProjectModuleTemplate(Project.current) {name = newPageName};
                    Project.current.RecordUndo().moduleTemplates.Add(Guid.NewGuid(), template);
                    newPageName = "";
                    ModuleCustomisationScreen.Show(template);
                    RefreshList();
                }

                gui.RemainingRow().BuildTextInput(newPageName, out newPageName, "Create new template");
            }
        }
    }
}