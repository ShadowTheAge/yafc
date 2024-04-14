using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using SDL2;
using Yafc.Model;
using Yafc.UI;

namespace Yafc {

    public class ProjectPageSettingsPanel : PseudoScreen {
        private static readonly ProjectPageSettingsPanel Instance = new ProjectPageSettingsPanel();

        private ProjectPage editingPage;
        private string name;
        private FactorioObject icon;
        private Action<string, FactorioObject> callback;

        public static void Build(ImGui gui, ref string name, FactorioObject icon, Action<FactorioObject> setIcon) {
            _ = gui.BuildTextInput(name, out name, "Input name");
            if (gui.BuildFactorioObjectButton(icon, 4f, MilestoneDisplay.None, SchemeColor.Grey)) {
                SelectSingleObjectPanel.Select(Database.objects.all, "Select icon", setIcon);
            }

            if (icon == null && gui.isBuilding) {
                gui.DrawText(gui.lastRect, "And select icon", RectAlignment.Middle);
            }
        }

        public static void Show(ProjectPage page, Action<string, FactorioObject> callback = null) {
            Instance.editingPage = page;
            Instance.name = page?.name;
            Instance.icon = page?.icon;
            Instance.callback = callback;
            _ = MainScreen.Instance.ShowPseudoScreen(Instance);
        }

        public override void Build(ImGui gui) {
            gui.spacing = 3f;
            BuildHeader(gui, editingPage == null ? "Create new page" : "Edit page icon and name");
            Build(gui, ref name, icon, s => {
                icon = s;
                Rebuild();
            });

            using (gui.EnterRow(0.5f, RectAllocator.RightRow)) {
                if (editingPage == null && gui.BuildButton("Create", active: !string.IsNullOrEmpty(name))) {
                    callback?.Invoke(name, icon);
                    Close();
                }

                if (editingPage != null && gui.BuildButton("OK", active: !string.IsNullOrEmpty(name))) {
                    if (editingPage.name != name || editingPage.icon != icon) {
                        editingPage.RecordUndo(true).name = name;
                        editingPage.icon = icon;
                    }
                    Close();
                }

                if (gui.BuildButton("Cancel", SchemeColor.Grey)) {
                    Close();
                }

                if (editingPage != null && gui.BuildButton("Other tools", SchemeColor.Grey, active: !string.IsNullOrEmpty(name))) {
                    gui.ShowDropDown(OtherToolsDropdown);
                }

                gui.allocator = RectAllocator.LeftRow;
                if (editingPage != null && gui.BuildRedButton("Delete page")) {
                    if (editingPage.canDelete) {
                        Project.current.RemovePage(editingPage);
                    }
                    else {
                        // Only hide if the (singleton) page cannot be deleted
                        MainScreen.Instance.ClosePage(editingPage.guid);
                    }
                    Close();
                }
            }
        }

        private void OtherToolsDropdown(ImGui gui) {
            if (editingPage.guid != MainScreen.SummaryGuid && gui.BuildContextMenuButton("Duplicate page")) {
                _ = gui.CloseDropdown();
                var project = editingPage.owner;
                ErrorCollector collector = new ErrorCollector();
                var serializedCopy = JsonUtils.Copy(editingPage, project, collector);
                if (collector.severity > ErrorSeverity.None) {
                    ErrorListPanel.Show(collector);
                }

                if (serializedCopy != null) {
                    serializedCopy.GenerateNewGuid();
                    serializedCopy.icon = icon;
                    serializedCopy.name = name;
                    project.RecordUndo().pages.Add(serializedCopy);
                    MainScreen.Instance.SetActivePage(serializedCopy);
                    Close();
                }
            }

            if (editingPage.guid != MainScreen.SummaryGuid && gui.BuildContextMenuButton("Share (export string to clipboard)")) {
                _ = gui.CloseDropdown();
                var data = JsonUtils.SaveToJson(editingPage);
                using MemoryStream targetStream = new MemoryStream();
                using (DeflateStream compress = new DeflateStream(targetStream, CompressionLevel.Optimal, true)) {
                    using (BinaryWriter writer = new BinaryWriter(compress, Encoding.UTF8, true)) {
                        // write some magic chars and version as a marker
                        writer.Write("YAFC\nProjectPage\n".AsSpan());
                        writer.Write(YafcLib.version.ToString().AsSpan());
                        writer.Write("\n\n\n".AsSpan());
                    }
                    data.CopyTo(compress);
                }
                string encoded = Convert.ToBase64String(targetStream.GetBuffer(), 0, (int)targetStream.Length);
                _ = SDL.SDL_SetClipboardText(encoded);
            }

            if (editingPage == MainScreen.Instance.activePage && gui.BuildContextMenuButton("Make full page screenshot")) {
                var screenshot = MainScreen.Instance.activePageView.GenerateFullPageScreenshot();
                ImageSharePanel.Show(screenshot, editingPage.name);
                _ = gui.CloseDropdown();
            }

            if (gui.BuildContextMenuButton("Export calculations (to clipboard)")) {
                ExportPage(editingPage);
                _ = gui.CloseDropdown();
            }
        }

        private class ExportRow {
            public ExportRecipe Header { get; }
            public IEnumerable<ExportRow> Children { get; }

            public ExportRow(RecipeRow row) {
                Header = row.recipe is null ? null : new ExportRecipe(row);
                Children = row.subgroup?.recipes.Select(r => new ExportRow(r)) ?? Array.Empty<ExportRow>();
            }
        }

        private class ExportRecipe {
            public string Recipe { get; }
            public string Building { get; }
            public float BuildingCount { get; }
            public IEnumerable<string> Modules { get; }
            public string Beacon { get; }
            public int BeaconCount { get; }
            public IEnumerable<string> BeaconModules { get; }
            public ExportMaterial Fuel { get; }
            public IEnumerable<ExportMaterial> Inputs { get; }
            public IEnumerable<ExportMaterial> Outputs { get; }

            public ExportRecipe(RecipeRow row) {
                Recipe = row.recipe.name;
                Building = row.entity.name;
                BuildingCount = row.buildingCount;
                Fuel = new ExportMaterial(row.fuel.name, row.parameters.fuelUsagePerSecondPerRecipe * row.recipesPerSecond);
                Inputs = row.recipe.ingredients.Select(i => new ExportMaterial(i.goods.name, i.amount * row.recipesPerSecond));
                Outputs = row.recipe.products.Select(i => new ExportMaterial(i.goods.name, i.GetAmount(row.parameters.productivity) * row.recipesPerSecond));
                Beacon = row.parameters.modules.beacon?.name;
                BeaconCount = row.parameters.modules.beaconCount;

                if (row.parameters.modules.modules is null) {
                    Modules = BeaconModules = Array.Empty<string>();
                }
                else {
                    List<string> modules = new List<string>();
                    List<string> beaconModules = new List<string>();

                    foreach (var (module, count, isBeacon) in row.parameters.modules.modules) {
                        if (isBeacon) {
                            beaconModules.AddRange(Enumerable.Repeat(module.name, count));
                        }
                        else {
                            modules.AddRange(Enumerable.Repeat(module.name, count));
                        }
                    }

                    Modules = modules;
                    BeaconModules = beaconModules;
                }
            }
        }

        private class ExportMaterial {
            public string Name { get; }
            public double CountPerSecond { get; }

            public ExportMaterial(string name, double countPerSecond) {
                Name = name;
                CountPerSecond = countPerSecond;
            }
        }

        private static void ExportPage(ProjectPage page) {
            using MemoryStream stream = new MemoryStream();
            using Utf8JsonWriter writer = new Utf8JsonWriter(stream);
            JsonSerializer.Serialize(stream, ((ProductionTable)page.content).recipes.Select(rr => new ExportRow(rr)), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            _ = SDL.SDL_SetClipboardText(Encoding.UTF8.GetString(stream.GetBuffer()));
        }

        public static void LoadProjectPageFromClipboard() {
            ErrorCollector collector = new ErrorCollector();
            var project = Project.current;
            ProjectPage page = null;
            try {
                string text = SDL.SDL_GetClipboardText();
                byte[] compressedBytes = Convert.FromBase64String(text.Trim());
                using DeflateStream deflateStream = new DeflateStream(new MemoryStream(compressedBytes), CompressionMode.Decompress);
                using MemoryStream ms = new MemoryStream();
                deflateStream.CopyTo(ms);
                byte[] bytes = ms.GetBuffer();
                int index = 0;
                if (DataUtils.ReadLine(bytes, ref index) != "YAFC" || DataUtils.ReadLine(bytes, ref index) != "ProjectPage") {
                    throw new InvalidDataException();
                }

                Version version = new Version(DataUtils.ReadLine(bytes, ref index) ?? "");
                if (version > YafcLib.version) {
                    collector.Error("String was created with the newer version of YAFC (" + version + "). Data may be lost.", ErrorSeverity.Important);
                }

                _ = DataUtils.ReadLine(bytes, ref index); // reserved 1
                if (DataUtils.ReadLine(bytes, ref index) != "") // reserved 2 but this time it is required to be empty
{
                    throw new NotSupportedException("Share string was created with future version of YAFC (" + version + ") and is incompatible");
                }

                page = JsonUtils.LoadFromJson<ProjectPage>(new ReadOnlySpan<byte>(bytes, index, (int)ms.Length - index), project, collector);
            }
            catch (Exception ex) {
                collector.Exception(ex, "Clipboard text does not contain valid YAFC share string", ErrorSeverity.Critical);
            }

            if (page != null) {
                var existing = project.FindPage(page.guid);
                if (existing != null) {
                    MessageBox.Show((haveChoice, choice) => {
                        if (!haveChoice) {
                            return;
                        }

                        if (choice) {
                            project.RemovePage(existing);
                        }
                        else {
                            page.GenerateNewGuid();
                        }

                        project.RecordUndo().pages.Add(page);
                        MainScreen.Instance.SetActivePage(page);
                    }, "Page already exists",
                    "Looks like this page already exists with name '" + existing.name + "'. Would you like to replace it or import as copy?", "Replace", "Import as copy");
                }
                else {
                    project.RecordUndo().pages.Add(page);
                    MainScreen.Instance.SetActivePage(page);
                }
            }

            if (collector.severity > ErrorSeverity.None) {
                ErrorListPanel.Show(collector);
            }
        }
    }
}
