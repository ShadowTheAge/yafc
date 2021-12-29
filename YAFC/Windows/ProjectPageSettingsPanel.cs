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

namespace YAFC {

    public class ProjectPageSettingsPanel : PseudoScreen {
        private static readonly ProjectPageSettingsPanel Instance = new ProjectPageSettingsPanel();

        private ProjectPage editingPage;
        private string name;
        private FactorioObject icon;
        private Action<string, FactorioObject> callback;

        public static void Build(ImGui gui, ref string name, FactorioObject icon, Action<FactorioObject> setIcon) {
            gui.BuildTextInput(name, out name, "Input name");
            if (gui.BuildFactorioObjectButton(icon, 4f, MilestoneDisplay.None, SchemeColor.Grey)) {
                SelectObjectPanel.Select(Database.objects.all, "Select icon", setIcon);
            }

            if (icon == null && gui.isBuilding)
                gui.DrawText(gui.lastRect, "And select icon", RectAlignment.Middle);
        }

        public static void Show(ProjectPage page, Action<string, FactorioObject> callback = null) {
            Instance.editingPage = page;
            Instance.name = page?.name;
            Instance.icon = page?.icon;
            Instance.callback = callback;
            MainScreen.Instance.ShowPseudoScreen(Instance);
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

                if (gui.BuildButton("Cancel", SchemeColor.Grey))
                    Close();

                if (editingPage != null && gui.BuildButton("Other tools", SchemeColor.Grey, active: !string.IsNullOrEmpty(name))) {
                    gui.ShowDropDown(OtherToolsDropdown);
                }

                gui.allocator = RectAllocator.LeftRow;
                if (editingPage != null && gui.BuildRedButton("Delete page")) {
                    Project.current.RemovePage(editingPage);
                    Close();
                }
            }
        }

        private void OtherToolsDropdown(ImGui gui) {
            if (editingPage.guid != MainScreen.SummaryGuid && gui.BuildContextMenuButton("Duplicate page")) {
                gui.CloseDropdown();
                var project = editingPage.owner;
                var collector = new ErrorCollector();
                var serializedCopy = JsonUtils.Copy(editingPage, project, collector);
                if (collector.severity > ErrorSeverity.None)
                    ErrorListPanel.Show(collector);
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
                gui.CloseDropdown();
                var data = JsonUtils.SaveToJson(editingPage);
                using (var targetStream = new MemoryStream()) {
                    using (var compress = new DeflateStream(targetStream, CompressionLevel.Optimal, true)) {
                        using (var writer = new BinaryWriter(compress, Encoding.UTF8, true)) {
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

            if (editingPage == MainScreen.Instance.activePage && gui.BuildContextMenuButton("Make full page screenshot")) {
                var screenshot = MainScreen.Instance.activePageView.GenerateFullPageScreenshot();
                ImageSharePanel.Show(screenshot, editingPage.name);
                gui.CloseDropdown();
            }

            if (gui.BuildContextMenuButton("Export calculations (to clipboard)")) {
                ExportPage(editingPage);
                gui.CloseDropdown();
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

                if (row.parameters.modules.modules is null)
                    Modules = BeaconModules = Array.Empty<string>();
                else {
                    var modules = new List<string>();
                    var beaconModules = new List<string>();

                    foreach (var (module, count, isBeacon) in row.parameters.modules.modules)
                        if (isBeacon)
                            beaconModules.AddRange(Enumerable.Repeat(module.name, count));
                        else
                            modules.AddRange(Enumerable.Repeat(module.name, count));

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
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);
            JsonSerializer.Serialize(stream, ((ProductionTable)page.content).recipes.Select(rr => new ExportRow(rr)), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            SDL.SDL_SetClipboardText(Encoding.UTF8.GetString(stream.GetBuffer()));
        }

        public static void LoadProjectPageFromClipboard() {
            var collector = new ErrorCollector();
            var project = Project.current;
            ProjectPage page = null;
            try {
                var text = SDL.SDL_GetClipboardText();
                var compressedBytes = Convert.FromBase64String(text.Trim());
                using (var deflateStream = new DeflateStream(new MemoryStream(compressedBytes), CompressionMode.Decompress)) {
                    using (var ms = new MemoryStream()) {
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
                        page = JsonUtils.LoadFromJson<ProjectPage>(new ReadOnlySpan<byte>(bytes, index, (int)ms.Length - index), project, collector);
                    }
                }
            }
            catch (Exception ex) {
                collector.Exception(ex, "Clipboard text does not contain valid YAFC share string", ErrorSeverity.Critical);
            }

            if (page != null) {
                var existing = project.FindPage(page.guid);
                if (existing != null) {
                    MessageBox.Show((haveChoice, choice) => {
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
