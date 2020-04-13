using System;
using SDL2;

namespace YAFC.UI
{
    public abstract class ButtonBase : WidgetContainer, IMouseClickHandle, IMouseEnterHandle
    {
        protected State state;

        protected enum State
        {
            Normal,
            Over,
            Down
        }

        public override SchemeColor boxColor => interactable ? state == State.Over || state == State.Down ? SchemeColor.PrimaryAlt : SchemeColor.Primary : SchemeColor.BackgroundAlt;

        public void MouseClickUpdateState(bool mouseOverAndDown, int button, RenderBatch batch)
        {
            var shouldState = mouseOverAndDown ? State.Down : State.Normal;
            if (state != shouldState)
            {
                state = shouldState;
                batch.Rebuild();
            }
        }

        public abstract void Click(RenderBatch batch);

        public void MouseClick(int button, RenderBatch batch)
        {
            if (interactable && button == SDL.SDL_BUTTON_LEFT)
                Click(batch);
        }

        public void MouseEnter(RenderBatch batch)
        {
            SDL.SDL_SetCursor(RenderingUtils.cursorHand);
            if (state == State.Normal)
            {
                state = State.Over;
                batch.Rebuild();
            }
        }

        public void MouseExit(RenderBatch batch)
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
        private readonly Action clickCallback;
        public TextButton(Font font, string text, Action clickCallback)
        {
            this.clickCallback = clickCallback;
            fontString = new FontString(font, text, false);
        }

        public string text
        {
            get => fontString.text;
            set => fontString.text = value;
        }

        protected override LayoutPosition BuildContent(RenderBatch batch, LayoutPosition location) => fontString.Build(batch, location);
        public override void Click(RenderBatch batch) => clickCallback?.Invoke();
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

        protected override LayoutPosition BuildContent(RenderBatch batch, LayoutPosition location)
        {
            var rect = location.IntoRect(1f, 1f);
            batch.DrawIcon(rect, Icon, SchemeColor.PrimaryText);
            return location;
        }
        public override void Click(RenderBatch batch) => clickCallback?.Invoke();
    }

    public abstract class SelectableElement<T> : ButtonBase, IListView<T>
    {
        protected T data;
        protected SelectableElement()
        {
            padding = new Padding(1, 0.25f);
        }
        public override SchemeColor boxColor => state == State.Normal ? SchemeColor.None : SchemeColor.BackgroundAlt;

        public virtual LayoutPosition BuildElement(T element, RenderBatch batch, LayoutPosition position)
        {
            data = element;
            return Build(batch, position);
        }
    }
}