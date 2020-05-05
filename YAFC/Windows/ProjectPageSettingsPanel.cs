using System;
using YAFC.Model;
using YAFC.Parser;
using YAFC.UI;

namespace YAFC
{
    public class ProjectPageSettingsPanel : PseudoScreen
    {
        private static readonly ProjectPageSettingsPanel Instance = new ProjectPageSettingsPanel();

        private ProjectPage editingPage;
        private string name;
        private FactorioObject icon;
        private Action<string, FactorioObject> callback;

        public static void Show(ProjectPage page, Action<string, FactorioObject> callback = null)
        {
            Instance.editingPage = page;
            Instance.name = page?.name;
            Instance.icon = page?.icon;
            Instance.callback = callback;
            MainScreen.Instance.ShowPseudoScreen(Instance);
        }
        
        public override void Build(ImGui gui)
        {
            gui.spacing = 3f;
            BuildHeader(gui, editingPage == null ? "Create new page" : "Edit page icon and name");
            gui.BuildTextInput(name, out name, "Input name");
            if (gui.BuildFactorioObjectButton(icon, 4f, MilestoneDisplay.None, SchemeColor.Grey))
            {
                SelectObjectPanel.Select(Database.allObjects, "Select icon", s =>
                {
                    icon = s;
                    Rebuild();
                });
            }

            if (icon == null && gui.isBuilding)
                gui.DrawText(gui.lastRect, "And select icon", RectAlignment.Middle);

            using (gui.EnterRow(0.5f, RectAllocator.RightRow))
            {
                if (editingPage == null && gui.BuildButton("Create", active:!string.IsNullOrEmpty(name)))
                {
                    callback?.Invoke(name, icon);
                    Close();
                }

                if (editingPage != null && gui.BuildButton("OK", active:!string.IsNullOrEmpty(name)))
                {
                    if (editingPage.name != name || editingPage.icon != icon)
                    {
                        editingPage.RecordUndo(true).name = name;
                        editingPage.icon = icon;
                    }
                    Close();
                }

                if (gui.BuildButton("Cancel", SchemeColor.Grey))
                    Close();

                gui.allocator = RectAllocator.LeftRow;
                if (editingPage != null && gui.BuildRedButton("Delete page") == ImGuiUtils.Event.Click)
                {
                    MainScreen.Instance.project.RecordUndo().pages.Remove(editingPage);
                    Close();
                }
            }
        }
    }
}