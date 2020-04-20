using System;
using System.Numerics;
using SDL2;

namespace YAFC.UI
{
    public abstract class ButtonBase : WidgetContainer, IMouseDragHandle
    {
        public readonly SchemeColor baseColor;
        protected bool down;
        protected bool over;

        public override SchemeColor boxColor => interactable ? over ? baseColor+1 : baseColor : SchemeColor.Grey;

        protected ButtonBase(SchemeColor baseColor = SchemeColor.Primary)
        {
            this.baseColor = baseColor;
        }

        public abstract void Click(UiBatch batch);

        public void MouseClick(int button, UiBatch batch)
        {
            if (interactable && button == SDL.SDL_BUTTON_LEFT)
                Click(batch);
        }

        public void MouseEnter(HitTestResult<IMouseHandle> hitTest)
        {
            SDL.SDL_SetCursor(RenderingUtils.cursorHand);
            over = true;
            hitTest.batch.Rebuild();
        }

        public void MouseExit(UiBatch batch)
        {
            SDL.SDL_SetCursor(RenderingUtils.cursorArrow);
            over = false;
            batch.Rebuild();
        }

        public void BeginDrag(Vector2 position, int button, UiBatch batch)
        {
            if (button == SDL.SDL_BUTTON_LEFT)
            {
                down = true;
                batch.Rebuild();
            }
        }

        public void Drag(Vector2 position, UiBatch batch) {}

        public void EndDrag(Vector2 position, UiBatch batch)
        {
            if (down)
            {
                down = false;
                batch.Rebuild();
            }
        }
    }

    public class TextButton : ButtonBase
    {
        private readonly FontString fontString;
        private readonly Action<UiBatch> clickCallback;
        public TextButton(Font font, string text, Action<UiBatch> clickCallback, SchemeColor baseColor = SchemeColor.Primary) : base(baseColor)
        {
            this.clickCallback = clickCallback;
            fontString = new FontString(font, text, align:RectAlignment.Middle, color:baseColor+2);
        }

        public string text
        {
            get => fontString.text;
            set => fontString.text = value;
        }

        protected override void BuildContent(LayoutState state)
        {
            fontString.SetTransparent(!interactable);
            fontString.Build(state);
        }
        public override void Click(UiBatch batch) => clickCallback?.Invoke(batch);
    }

    public class IconButton : ButtonBase
    {
        private Icon _icon;
        private readonly Action clickCallback;
        
        public IconButton(Icon icon, Action clickCallback)
        {
            this.clickCallback = clickCallback;
            _icon = icon;
        }

        public Icon Icon
        {
            get => _icon;
            set
            {
                _icon = value;
                Rebuild();
            }
        }

        protected override void BuildContent(LayoutState state)
        {
            var rect = state.AllocateRect(1f, 1f, RectAlignment.Middle);
            state.batch.DrawIcon(rect, Icon, SchemeColor.PrimaryText);
        }
        public override void Click(UiBatch batch) => clickCallback?.Invoke();
    }

    public abstract class SelectableElement<T> : ButtonBase, IListView<T>
    {
        protected T data;
        protected SelectableElement()
        {
            padding = new Padding(1, 0.25f);
        }
        public override SchemeColor boxColor => over ? SchemeColor.Grey : SchemeColor.None;

        public virtual void BuildElement(T element, LayoutState state)
        {
            data = element;
            Build(state);
        }
    }
}