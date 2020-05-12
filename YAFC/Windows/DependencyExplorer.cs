using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SDL2;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class DependencyExplorer : PseudoScreen
    {
        private readonly VerticalScrollCustom dependencies;
        private readonly VerticalScrollCustom dependants;
        private static readonly Padding listPad = new Padding(0.5f);
        
        private readonly List<FactorioObject> history = new List<FactorioObject>();
        private FactorioObject current;

        private static readonly Dictionary<DependencyList.Flags, (string name, string missingText)> dependencyListTexts = new Dictionary<DependencyList.Flags, (string, string)>()
        {
            {DependencyList.Flags.Fuel, ("Fuel", "There is no fuel to power this entity")},
            {DependencyList.Flags.Ingredient, ("Ingredient", "There are no ingredients to this recipe")},
            {DependencyList.Flags.CraftingEntity, ("Crafter", "There are no crafters that can craft this item")},
            {DependencyList.Flags.Source, ("Source", "This item have no sources")},
            {DependencyList.Flags.TechnologyUnlock, ("Research", "This recipe is disabled and there are no technologies to unlock it")},
            {DependencyList.Flags.TechnologyPrerequisites, ("Research", "There are no technology prerequisites")},
            {DependencyList.Flags.ItemToPlace, ("Item", "This entity cannot be placed")},
            {DependencyList.Flags.SourceEntity, ("Source", "This recipe requires another entity")},
        };
        
        public DependencyExplorer() : base(60f)
        {
            dependencies = new VerticalScrollCustom(30f, DrawDependencies);
            dependants = new VerticalScrollCustom(30f, DrawDependants);
        }

        private static readonly DependencyExplorer Instance = new DependencyExplorer();

        public static void Show(FactorioObject target)
        {
            Instance.Change(target);
            Instance.history.Clear();
            MainScreen.Instance.ShowPseudoScreen(Instance);
        }

        private void DrawFactorioObject(ImGui gui, int id)
        {
            var fobj = Database.objects[id];
            using (gui.EnterGroup(listPad, RectAllocator.LeftRow))
            {
                gui.BuildFactorioObjectIcon(fobj);
                var text = fobj.locName + " (" + fobj.nameOfType + ")";
                gui.RemainingRow(0.5f).BuildText(text, null, true, color:fobj.IsAccessible() ? SchemeColor.BackgroundText : SchemeColor.BackgroundTextFaint);
            }
            var flow = fobj.ApproximateFlow();
            if (gui.BuildFactorioObjectButton(gui.lastRect, fobj, extendHeader:true, bgColor:flow > 1000f ? SchemeColor.Primary : SchemeColor.None))
                Change(fobj);
        }

        private void DrawDependencies(ImGui gui)
        {
            gui.spacing = 0f;
            foreach (var data in Dependencies.dependencyList[current])
            {
                if (!dependencyListTexts.TryGetValue(data.flags, out var dependencyType))
                    dependencyType = (data.flags.ToString(), "Missing "+data.flags);
                if (data.elements.Length > 0)
                {
                    gui.AllocateSpacing(0.5f);
                    if (data.elements.Length == 1)
                        gui.BuildText("Require this "+dependencyType.name+":");
                    else if ((data.flags & DependencyList.Flags.RequireEverything) == 0)
                        gui.BuildText("Require ANY of these " + dependencyType.name + "s:");
                    else gui.BuildText("Require ALL of these " + dependencyType.name + "s:");
                    gui.AllocateSpacing(0.5f);
                    foreach (var id in data.elements.OrderByDescending(x => CostAnalysis.Instance.flow[x]))
                        DrawFactorioObject(gui, id);
                }
                else
                {
                    var text = dependencyType.missingText;
                    if (Database.rootAccessible.Contains(current))
                        text += ", but it is inherently accessible";
                    else text += ", and it is inaccessible";
                    gui.BuildText(text, wrap:true);
                }
            }
        }

        private void DrawDependants(ImGui gui)
        {
            gui.spacing = 0f;
            foreach (var reverseDependency in Dependencies.reverseDependencies[current].OrderByDescending(x => CostAnalysis.Instance.flow[x]))
                DrawFactorioObject(gui, reverseDependency);
        }

        public override void Build(ImGui gui)
        {
            gui.allocator = RectAllocator.Center;
            BuildHeader(gui, "Dependency explorer");
            gui.BuildText(current.locName, Font.subheader);
            if (gui.BuildFactorioObjectButton(current, 3f))
                SelectObjectPanel.Select(Database.objects.all, "Select something", Change);
            using (var split = gui.EnterHorizontalSplit(2))
            {
                split.Next();
                gui.BuildText("Dependencies:", Font.subheader);
                dependencies.Build(gui);
                split.Next();
                gui.BuildText("Dependants:", Font.subheader);
                dependants.Build(gui);
            }
        }
        
        public void Change(FactorioObject target)
        {
            if (target == null)
            {
                if (current == null)
                    Close();
                return;
            }
            
            history.Add(current);
            if (history.Count > 100)
                history.RemoveRange(0, 20);
            current = target;
            dependants.Rebuild();
            dependencies.Rebuild();
            Rebuild();
        }

        public override void KeyDown(SDL.SDL_Keysym key)
        {
            if (key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_BACKSPACE && history.Count > 0)
            {
                var last = history[history.Count - 1];
                Change(last);
                history.RemoveRange(history.Count-2, 2);
            }
            base.KeyDown(key);
        }
    }
}