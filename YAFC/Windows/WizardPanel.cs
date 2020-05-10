using System;
using System.Collections.Generic;
using YAFC.UI;

namespace YAFC
{
    public class WizardPanel : PseudoScreen
    {
        private readonly Builder[] pages;
        private readonly string header;
        private readonly Action finish;
        private int page;

        public delegate void Builder(ImGui gui, ref bool valid);
        
        public WizardPanel(string header, Action finish, params Builder[] pages)
        {
            this.pages = pages;
            this.header = header;
            this.finish = finish;
        }

        public override void Open()
        {
            page = 0;
            base.Open();
        }

        public void Show() => MainScreen.Instance.ShowPseudoScreen(this);
        public override void Build(ImGui gui)
        {
            BuildHeader(gui, header);
            var valid = true;
            pages[page](gui, ref valid);
            using (gui.EnterRow(allocator:RectAllocator.RightRow))
            {
                if (gui.BuildButton(page >= pages.Length - 1 ? "Finish" : "Next", active: valid))
                {
                    if (page < pages.Length - 1)
                        page++;
                    else
                    {
                        Close();
                        finish();
                    }
                }
                if (page > 0 && gui.BuildButton("Previous"))
                    page--;
                gui.BuildText("Step " + (page + 1) + " of " + pages.Length);
            }
        }
    }
}