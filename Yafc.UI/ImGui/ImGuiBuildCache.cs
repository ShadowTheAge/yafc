using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Yafc.UI;
public partial class ImGui {
    public class BuildGroup {
        private readonly ImGui gui;
        private object? obj;
        private float left, right, top;
        private CopyableState state;
        private Rect lastRect;
        private bool finished;

        public BuildGroup(ImGui gui) => this.gui = gui;

        public void Update(object obj) {
            left = gui.state.left;
            right = gui.state.right;
            top = gui.state.top;
            this.obj = obj;
            finished = false;
        }

        public void Complete() {
            if (!finished) {
                finished = true;
                state = gui.state;
                lastRect = gui.lastRect;
            }
        }

        public bool CanSkip(object o) => o == obj && gui.action != ImGuiAction.Build && left == gui.state.left && right == gui.state.right && top == gui.state.top && finished &&
                   (gui.localClip.Top > state.bottom || gui.localClip.Bottom < top);

        public void Skip() {
            gui.state = state;
            gui.lastRect = lastRect;
        }
    }

    private int buildGroupsIndex = -1;
    private readonly List<BuildGroup> buildGroups = [];

    public bool ShouldBuildGroup(object o, [MaybeNullWhen(false)] out BuildGroup group) {
        buildGroupsIndex++;
        BuildGroup current;
        if (buildGroups.Count > buildGroupsIndex) {
            current = buildGroups[buildGroupsIndex];
            if (current.CanSkip(o)) {
                current.Skip();
                group = null;
                return false;
            }
        }
        else {
            current = new BuildGroup(this);
            buildGroups.Add(current);
        }
        current.Update(o);
        group = current;
        return true;
    }
}
