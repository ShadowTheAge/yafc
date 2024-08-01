using System.Numerics;
using Yafc.Model;
using Yafc.UI;

namespace Yafc {
    public class ModuleTemplateConfiguration : PseudoScreen {
        private static readonly ModuleTemplateConfiguration Instance = new ModuleTemplateConfiguration();
        private readonly VirtualScrollList<ProjectModuleTemplate> templateList;
        private ProjectModuleTemplate? pageToDelete;
        private string newPageName = "";

        public ModuleTemplateConfiguration() => templateList = new VirtualScrollList<ProjectModuleTemplate>(30, new Vector2(20, 2.5f), Drawer,
                reorder: (from, to) => Project.current.RecordUndo().sharedModuleTemplates.MoveListElementIndex(from, to));

        public static void Show() {
            Instance.RefreshList();
            _ = MainScreen.Instance.ShowPseudoScreen(Instance);
        }

        private void RefreshList() {
            templateList.data = Project.current.sharedModuleTemplates;
            Rebuild();
        }

        private void Drawer(ImGui gui, ProjectModuleTemplate element, int index) {
            gui.allocator = RectAllocator.RightRow;
            if (gui.BuildButton(Icon.Delete)) {
                pageToDelete = element;
                Rebuild();
            }

            if (gui.BuildButton(Icon.Copy)) {
                var copy = JsonUtils.Copy(element, element.owner, null);
                if (copy != null) {
                    element.owner.RecordUndo().sharedModuleTemplates.Add(copy);
                    ModuleCustomizationScreen.Show(copy);
                }
            }
            if (gui.BuildButton(Icon.Edit)) {
                ModuleCustomizationScreen.Show(element);
            }

            gui.allocator = RectAllocator.LeftRow;
            if (element.icon != null) {
                gui.BuildFactorioObjectIcon(element.icon);
            }

            gui.BuildText(element.name);
        }

        public override void Activated() {
            base.Activated();
            templateList.RebuildContents();
        }

        public override void Build(ImGui gui) {
            BuildHeader(gui, "Module templates");
            templateList.Build(gui);
            if (pageToDelete != null) {
                _ = Project.current.RecordUndo().sharedModuleTemplates.Remove(pageToDelete);
                RefreshList();
                pageToDelete = null;
            }
            using (gui.EnterRow(0.5f, RectAllocator.RightRow)) {
                if (gui.BuildButton("Create", active: newPageName != "")) {
                    ProjectModuleTemplate template = new(Project.current, newPageName);
                    Project.current.RecordUndo().sharedModuleTemplates.Add(template);
                    newPageName = "";
                    ModuleCustomizationScreen.Show(template);
                    RefreshList();
                }

                _ = gui.RemainingRow().BuildTextInput(newPageName, out newPageName, "Create new template", setInitialFocus: true);
            }
        }
    }
}
