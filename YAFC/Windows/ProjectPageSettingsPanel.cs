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
        
        public static void Build(ImGui gui, ref string name, FactorioObject icon, Action<FactorioObject> setIcon)
        {
            gui.BuildTextInput(name, out name, "Input name");
            if (gui.BuildFactorioObjectButton(icon, 4f, MilestoneDisplay.None, SchemeColor.Grey))
            {
                SelectObjectPanel.Select(Database.objects.all, "Select icon", setIcon);
            }

            if (icon == null && gui.isBuilding)
                gui.DrawText(gui.lastRect, "And select icon", RectAlignment.Middle);
        }

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
            Build(gui, ref name, icon, s =>
            {
                icon = s;
                Rebuild();
            });

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
                
                if (editingPage != null && gui.BuildButton("Duplicate page", SchemeColor.Grey, active:!string.IsNullOrEmpty(name)))
                {
                    var project = editingPage.owner;
                    var collector = new ErrorCollector();
                    var serializedCopy = JsonUtils.Copy(editingPage, project, collector);
                    if (collector.severity > ErrorSeverity.None)
                        ErrorListPanel.Show(collector);
                    if (serializedCopy != null)
                    {
                        serializedCopy.GenerateNewGuid();
                        serializedCopy.icon = icon;
                        serializedCopy.name = name;
                        project.RecordUndo().pages.Add(serializedCopy);
                        MainScreen.Instance.SetActivePage(serializedCopy);
                        Close();
                    }
                }

                gui.allocator = RectAllocator.LeftRow;
                if (editingPage != null && gui.BuildRedButton("Delete page") == ImGuiUtils.Event.Click)
                {
                    Project.current.RecordUndo().pages.Remove(editingPage);
                    Close();
                }
            }
        }
    }
}