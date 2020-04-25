using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using YAFC.UI;

namespace YAFC
{
    public class FilesystemScreen : TaskWindow<string>
    {
        private enum EntryType {Drive, Directory, ParentDirectory, CreateDirectory, File}
        public enum Mode
        {
            SelectFolder,
            SelectOrCreateFolder,
            SelectFile,
            SelectOrCreateFile
        }

        private readonly string description;
        private string location;
        private readonly Mode mode;
        private readonly VirtualScrollList<(EntryType type, string location)> entries;
        private string fileName;
        private readonly string extension;
        private readonly string button;
        private readonly string defaultFileName;
        private readonly Func<string, bool> filter;
        private string selectedResult;
        private bool resultValid;

        public FilesystemScreen(string header, string description, string button, string location, Mode mode, string defaultFileName, Window parent, Func<string, bool> filter, string extension)
        {
            this.description = description;
            this.mode = mode;
            this.defaultFileName = defaultFileName;
            this.extension = extension;
            this.filter = filter;
            this.button = button;
            entries = new VirtualScrollList<(EntryType type, string location)>(30f, new Vector2(float.PositiveInfinity, 1.5f), BuildElement);
            SetLocation(Directory.Exists(location) ? location : "");
            Create(header, 30f, parent);
        }

        public override void Build(ImGui gui)
        {
            gui.BuildText(description, wrap:true);
            if (gui.BuildTextInput(location, out var newLocation, null))
            {
                if (Directory.Exists(newLocation))
                    SetLocation(newLocation);
            }
            entries.Build(gui);
            if (mode == Mode.SelectFolder || mode == Mode.SelectOrCreateFolder)
                BuildSelectButton(gui);
            else
            {
                using (gui.EnterGroup(default, RectAllocator.RightRow))
                {
                    BuildSelectButton(gui);
                    if (gui.RemainingRow().BuildTextInput(fileName, out fileName, null))
                        UpdatePossibleResult();
                }
            }
        }

        private void BuildSelectButton(ImGui gui)
        {
            if (gui.BuildButton(button, active:resultValid))
                CloseWithResult(selectedResult);
        }
        
        private void SetLocation(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                entries.data = Directory.GetLogicalDrives().Select(x => (EntryType.Drive, x)).ToArray();
            }
            else
            {
                if (!Directory.Exists(directory))
                    return;
                
                var data = Directory.EnumerateDirectories(directory).Select(x => (EntryType.Directory, x));
                if (mode == Mode.SelectOrCreateFolder || mode == Mode.SelectOrCreateFile)
                    data = data.Append((EntryType.CreateDirectory, directory));
                var parent = Directory.GetParent(directory)?.FullName ?? "";
                data = data.Prepend((EntryType.ParentDirectory, parent));
                if (mode == Mode.SelectFile || mode == Mode.SelectOrCreateFile)
                {
                    fileName = defaultFileName;
                    IEnumerable<string> files = extension == null ? Directory.GetFiles(directory) : Directory.GetFiles(directory, "*." + extension);
                    if (filter != null)
                        files = files.Where(filter);
                    data = data.Concat(files.Select(x => (EntryType.File, x)));                    
                }
                entries.data = data.ToArray();
            }
            location = directory;

            UpdatePossibleResult();
            entries.scroll = 0;
        }
        
        public void UpdatePossibleResult()
        {
            if (mode == Mode.SelectFolder)
            {
                selectedResult = location;
            }
            else
            {
                var selectedFileName = fileName;
                if (string.IsNullOrEmpty(selectedFileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    selectedResult = null;
                }
                else
                {
                    if (!selectedFileName.EndsWith("." + extension, StringComparison.OrdinalIgnoreCase))
                        selectedFileName += "." + extension;
                    selectedResult = Path.Combine(location, selectedFileName);
                    if (mode == Mode.SelectFile && !File.Exists(selectedResult))
                        selectedResult = null;
                }
            }

            resultValid = selectedResult != null && (filter == null || filter(selectedResult));
            rootGui.Rebuild();
        }
        
        private (Icon, string) GetDisplay((EntryType type, string location) data)
        {
            switch (data.type)
            {
                case EntryType.Directory: return (Icon.Folder, Path.GetFileName(data.location));
                case EntryType.Drive: return (Icon.FolderOpen, data.location);
                case EntryType.ParentDirectory: return (Icon.Upload, "..");
                case EntryType.CreateDirectory: return (Icon.NewFolder, "[Create directory here]");
                default: return (Icon.Settings, Path.GetFileName(data.location));
            }
        }

        private void BuildElement(ImGui gui, (EntryType type, string location) element, int index)
        {
            var (icon, elementText) = GetDisplay(element);
            using (gui.EnterGroup(default, RectAllocator.LeftRow))
            {
                gui.BuildIcon(icon);
                gui.RemainingRow().BuildText(elementText);
            }

            if (gui.BuildButton(gui.lastRect, SchemeColor.None, SchemeColor.Grey, SchemeColor.Grey))
            {
                SetLocation(element.location);
            }
        }
    }
}