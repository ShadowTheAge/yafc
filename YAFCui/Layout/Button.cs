using System;
using SDL2;

namespace YAFC.UI
{
    public abstract class ButtonBase : WidgetContainer, IMouseClickHandle, IMouseEnterHandle
    {
        protected State state;
        public readonly SchemeColor baseColor;

        protected enum State
        {
            Normal,
            Over,
            Down
        }

        public override SchemeColor boxColor => interactable ? state == State.Over || state == State.Down ? baseColor+1 : baseColor : SchemeColor.Grey;

        protected ButtonBase(SchemeColor baseColor = SchemeColor.Primary)
        {
            this.baseColor = baseColor;
        }

        public void MouseClickUpdateState(bool mouseOverAndDown, int button, UiBatch batch)
        {
            var shouldState = mouseOverAndDown ? State.Down : State.Normal;
            if (state != shouldState)
            {
                state = shouldState;
                batch.Rebuild();
            }
        }

        public abstract void Click(UiBatch batch);

        public void MouseClick(int button, UiBatch batch)
        {
            if (interactable && button == SDL.SDL_BUTTON_LEFT)
                Click(batch);
        }

        public void MouseEnter(RaycastResult<IMouseEnterHandle> raycast)
        {
            SDL.SDL_SetCursor(RenderingUtils.cursorHand);
            if (state == State.Normal)
            {
                state = State.Over;
                raycast.owner.Rebuild();
            }
        }

        public void MouseExit(UiBatch batch)
        {
            SDL.SDL_SetCursor(RenderingUtils.cursorArrow);
            if (state != State.Normal)
            {
                state = State.Normal;
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
        public override SchemeColor boxColor => state == State.Normal ? SchemeColor.None : SchemeColor.BackgroundAlt;

        public virtual void BuildElement(T element, LayoutState state)
        {
            data = element;
            Build(state);
        }
    }
}