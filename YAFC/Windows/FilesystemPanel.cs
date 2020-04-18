using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using YAFC.UI;

namespace YAFC
{
    public class FilesystemPanel : TaskWindow<string>
    {
        private enum EntryType {Drive, Directory, ParentDirectory, CreateDirectory, File}
        public enum Mode
        {
            SelectFolder,
            SelectOrCreateFolder,
            SelectFile,
            SelectOrCreateFile
        }
        
        private readonly FontString description;
        private readonly InputField location;
        private readonly InputField fileName;
        private readonly VirtualScrollList<(EntryType type, string location), EntryView> entries;
        private readonly string defaultFileNameText;
        private readonly string extension;
        private readonly TextButton selectButton;
        private readonly Mode mode;
        private readonly Func<string, bool> filter;

        private string currentLocation;
        private string selectedResult;
        
        public FilesystemPanel(string header, string description, string button, string location, Mode mode, string defaultFileName, Window parent, Func<string, bool> filter, string extension)
        {
            this.padding = new Padding(1f);
            this.description = new FontString(Font.text, description, true);
            this.location = new InputField(Font.text) {text = location, onChange = LocationChanged};
            this.entries = new VirtualScrollList<(EntryType type, string location), EntryView>(new SizeF(10, 30), 1.5f);
            if (mode == Mode.SelectFile || mode == Mode.SelectOrCreateFile)
            {
                this.fileName = new InputField(Font.text) {text = defaultFileName, onChange = UpdatePossibleResult};
            }
            this.defaultFileNameText = defaultFileName;
            this.extension = extension;
            this.filter = filter;
            this.mode = mode;
            this.selectButton = new TextButton(Font.text, button, SelectClick);
            SetLocation(Directory.Exists(location) ? location : "");
            
            Create(header, 30f, true, parent);
        }

        private void LocationChanged()
        {
            if (Directory.Exists(location.text))
                SetLocation(location.text);
        }

        private void SelectClick(UiBatch batch)
        {
            CloseWithResult(selectedResult);
        }

        protected override void BuildContent(LayoutState state)
        {
            state.Build(description).Build(location).Build(entries);
            if (mode == Mode.SelectFolder || mode == Mode.SelectOrCreateFolder)
                state.Build(selectButton);
            else
            {
                using (state.EnterGroup(default, RectAllocator.RightRow))
                {
                    state.Build(selectButton).BuildRemaining(fileName);
                }
            }
        }

        private void SetLocation(string directory)
        {
            currentLocation = directory;
            location.text = directory;
            if (string.IsNullOrEmpty(directory))
            {
                entries.data = Directory.GetLogicalDrives().Select(x => (EntryType.Drive, x)).ToArray();
            }
            else
            {
                if (!Directory.Exists(directory))
                {
                    location.text = currentLocation;
                    return;
                }

                var data = Directory.EnumerateDirectories(directory).Select(x => (EntryType.Directory, x));
                if (mode == Mode.SelectOrCreateFolder || mode == Mode.SelectOrCreateFile)
                    data = data.Append((EntryType.CreateDirectory, directory));
                var parent = Directory.GetParent(directory)?.FullName ?? "";
                data = data.Prepend((EntryType.ParentDirectory, parent));
                if (mode == Mode.SelectFile || mode == Mode.SelectOrCreateFile)
                {
                    fileName.text = defaultFileNameText;
                    IEnumerable<string> files = extension == null ? Directory.GetFiles(directory) : Directory.GetFiles(directory, "*." + extension);
                    if (filter != null)
                        files = files.Where(filter);
                    data = data.Concat(files.Select(x => (EntryType.File, x)));                    
                }
                entries.data = data.ToArray();
            }

            UpdatePossibleResult();
            entries.scroll = 0;
        }
        
        private void SelectFile(string path)
        {
            fileName.text = Path.GetFileName(path);
            UpdatePossibleResult();
        }
        
        public void UpdatePossibleResult()
        {
            if (mode == Mode.SelectFolder)
            {
                selectedResult = currentLocation;
            }
            else
            {
                var filename = fileName.text;
                if (filename == "" || filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    selectedResult = null;
                }
                else
                {
                    if (!filename.EndsWith("." + extension, StringComparison.OrdinalIgnoreCase))
                        filename += "." + extension;
                    selectedResult = Path.Combine(currentLocation, filename);
                    if (mode == Mode.SelectFile && !File.Exists(selectedResult))
                        selectedResult = null;
                }
            }

            selectButton.interactable = selectedResult != null && (filter == null || filter(selectedResult));
        }

        private class EntryView : SelectableElement<(EntryType type, string location)>
        {
            private readonly FontString text;

            public EntryView()
            {
                text = new FontString(Font.text);
            }

            private (Icon, string) GetDisplay()
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
            public override void Click(UiBatch batch)
            {
                var owner = batch.FindOwner<FilesystemPanel>();
                switch (data.type)
                {
                    case EntryType.Directory: case EntryType.Drive: case EntryType.ParentDirectory:
                        owner.SetLocation(data.location);
                        break;
                    case EntryType.File:
                        owner.SelectFile(data.location);
                        break;
                }
            }

            protected override void BuildContent(LayoutState state)
            {
                var (icon, elementText) = GetDisplay();
                using (state.EnterGroup(default, RectAllocator.LeftRow))
                {
                    state.batch.DrawIcon(state.AllocateRect(1f, 1f), icon, SchemeColor.BackgroundText);
                    state.spacing = 0.5f;
                    state.allocator = RectAllocator.RemainigRow;
                    text.BuildElement(elementText, state);
                }
            }
        }
    }
}