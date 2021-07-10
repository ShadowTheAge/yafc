using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using SDL2;
using YAFC.Model;
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

                if (editingPage != null && gui.BuildButton("Other tools", SchemeColor.Grey, active:!string.IsNullOrEmpty(name)))
                {
                    gui.ShowDropDown(OtherToolsDropdown);
                }

                gui.allocator = RectAllocator.LeftRow;
                if (editingPage != null && gui.BuildRedButton("Delete page"))
                {
                    Project.current.RemovePage(editingPage);
                    Close();
                }
            }
        }

        private void OtherToolsDropdown(ImGui gui)
        {
            if (gui.BuildContextMenuButton("Duplicate page"))
            {
                gui.CloseDropdown();
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

            if (gui.BuildContextMenuButton("Share (export string to clipboard)"))
            {
                gui.CloseDropdown();
                var data = JsonUtils.SaveToJson(editingPage);
                using (var targetStream = new MemoryStream())
                {
                    using (var compress = new DeflateStream(targetStream, CompressionLevel.Optimal, true))
                    {
                        using (var writer = new BinaryWriter(compress, Encoding.UTF8, true))
                        {
                            // write some magic chars and version as a marker
                            writer.Write("YAFC\nProjectPage\n".AsSpan());
                            writer.Write(YafcLib.version.ToString().AsSpan());
                            writer.Write("\n\n\n".AsSpan());
                        }
                        data.CopyTo(compress);
                    }
                    var encoded = Convert.ToBase64String(targetStream.GetBuffer(), 0, (int)targetStream.Length);
                    SDL.SDL_SetClipboardText(encoded);
                }
            }

            if (editingPage == MainScreen.Instance.activePage && gui.BuildContextMenuButton("Make full page screenshot"))
            {
                var screenshot = MainScreen.Instance.activePageView.GenerateFullPageScreenshot();
                ImageSharePanel.Show(screenshot, editingPage.name);
                gui.CloseDropdown();
            }

            if (gui.BuildContextMenuButton("Export page calculations"))
            {
                ExportPage(editingPage);
                gui.CloseDropdown();
            }
        }

        private static void ExportPage(ProjectPage page)
        {
            var projectName = MainScreen.Instance.project.attachedFileName ?? String.Empty;
            var exportPath = Path.Combine(Path.GetDirectoryName(projectName), $"{Path.GetFileNameWithoutExtension(projectName)}-{page.name}.json");
            using var stream = File.Create(exportPath);
            using var writer = new Utf8JsonWriter(stream);
            var data = GetRecipes((ProductionTable)page.content).Select(rr => new
            {
                recipe = rr.recipe.name,
                building = rr.entity.name,
                rr.buildingCount,
                modules = ListModules(rr),
            });
            JsonSerializer.Serialize(stream, data);

            static IEnumerable<string> ListModules(RecipeRow recipeRow)
            {
                if (recipeRow.modules is null)
                    return Array.Empty<string>();

                var data = new List<string>();
                int remainingSlots = recipeRow.entity.moduleSlots;
                foreach (var item in recipeRow.modules.list)
                    if (item.fixedCount > 0)
                    {
                        data.AddRange(Enumerable.Repeat(item.module.name, item.fixedCount));
                        remainingSlots -= item.fixedCount;
                    }
                    else
                        data.AddRange(Enumerable.Repeat(item.module.name, remainingSlots));

                return data;
            }

            static IEnumerable<RecipeRow> GetRecipes(ProductionTable content)
            {
                foreach (var item in content.recipes)
                {
                    yield return item;
                    if (item.subgroup is ProductionTable subgroup)
                        foreach (var child in GetRecipes(subgroup))
                            yield return child;
                }
            }
        }

        public static void LoadProjectPageFromClipboard()
        {
            var collector = new ErrorCollector();
            var project = Project.current;
            ProjectPage page = null;
            try
            {
                var text = SDL.SDL_GetClipboardText();
                var compressedBytes = Convert.FromBase64String(text.Trim());
                using (var deflateStream = new DeflateStream(new MemoryStream(compressedBytes), CompressionMode.Decompress))
                {
                    using (var ms = new MemoryStream())
                    {
                        deflateStream.CopyTo(ms);
                        var bytes = ms.GetBuffer();
                        var index = 0;
                        if (DataUtils.ReadLine(bytes, ref index) != "YAFC" || DataUtils.ReadLine(bytes, ref index) != "ProjectPage")
                            throw new InvalidDataException();
                        var version = new Version(DataUtils.ReadLine(bytes, ref index) ?? "");
                        if (version > YafcLib.version)
                            collector.Error("String was created with the newer version of YAFC (" + version + "). Data may be lost.", ErrorSeverity.Important);
                        DataUtils.ReadLine(bytes, ref index); // reserved 1
                        if (DataUtils.ReadLine(bytes, ref index) != "") // reserved 2 but this time it is requried to be empty
                            throw new NotSupportedException("Share string was created with future version of YAFC (" + version + ") and is incompatible");
                        page = JsonUtils.LoadFromJson<ProjectPage>(new ReadOnlySpan<byte>(bytes, index, (int) ms.Length - index), project, collector);
                    }
                }
            }
            catch (Exception ex)
            {
                collector.Exception(ex, "Clipboard text does not contain valid YAFC share string", ErrorSeverity.Critical);
            }

            if (page != null)
            {
                var existing = project.FindPage(page.guid); 
                if (existing != null)
                {
                    MessageBox.Show((haveChoice, choice) =>
                    {
                        if (!haveChoice)
                            return;
                        if (choice)
                            project.RemovePage(existing);
                        else
                            page.GenerateNewGuid();
                        project.RecordUndo().pages.Add(page);
                        MainScreen.Instance.SetActivePage(page);
                    }, "Page already exists",
                    "Looks like this page already exists with name '" + existing.name + "'. Would you like to replace it or import as copy?", "Replace", "Import as copy");
                }
                else
                {
                    project.RecordUndo().pages.Add(page);
                    MainScreen.Instance.SetActivePage(page);
                }
            }

            if (collector.severity > ErrorSeverity.None)
            {
                ErrorListPanel.Show(collector);
            }
        }
    }
}