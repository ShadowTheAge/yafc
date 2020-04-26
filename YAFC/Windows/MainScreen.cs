using YAFC.UI;

namespace YAFC
{
    public class MainScreen : WindowMain
    {
        public static MainScreen Instance { get; private set; }

        private ProjectPage activePage = new WorkspacePage();
        
        public MainScreen(int display) : base(default)
        {
            Instance = this;
            Create("Factorio Calculator", display);
        }

        public override void Build(ImGui gui)
        {
            if (activePage != null)
            {
                activePage.Build(gui, 50);
            }
        }
    }
}