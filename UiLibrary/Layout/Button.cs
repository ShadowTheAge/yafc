using System;
using System.Drawing;

namespace UI
{
    public abstract class ButtonBase : WidgetContainer, IMouseClickHandle, IMouseEnterHandle
    {
        private State state;
        private readonly Action<int> clickCallback;

        protected ButtonBase(Action<int> clickCallback)
        {
            this.clickCallback = clickCallback;
        }

        private enum State
        {
            Normal,
            Over,
            Down
        }

        public override RectangleF Build(RenderBatch batch, LayoutPosition layoutPosition, Alignment align)
        {
            var rect = base.Build(batch, layoutPosition, align);
            batch.DrawRectangle(rect, state == State.Over || state == State.Down ? SchemeColor.PrimaryAlt : SchemeColor.Primary);
            return rect;
        }

        public void MouseClickUpdateState(bool mouseOverAndDown, int button)
        {
            var shouldState = mouseOverAndDown ? State.Down : State.Normal;
            if (state != shouldState)
            {
                state = shouldState;
                SetDirty();
            }
        }

        public void MouseClick(int button) => clickCallback?.Invoke(button);

        public void MouseEnter()
        {
            if (state == State.Normal)
            {
                state = State.Over;
                SetDirty();
            }
        }

        public void MouseExit()
        {
            if (state != State.Normal)
            {
                state = State.Normal;
                SetDirty();
            }
        }
    }

    public class TextButton : ButtonBase
    {
        private readonly FontString fontString;
        public TextButton(Font font, string text, Action<int> clickCallback) : base(clickCallback)
        {
            fontString = new FontString(font, false, text);
        }

        public string text
        {
            get => fontString.text;
            set => fontString.text = value;
        }

        public override RectangleF BuildContent(RenderBatch batch, LayoutPosition location, Alignment align) => fontString.Build(batch, location, align);
    }

    public class IconButton : ButtonBase
    {
        private Sprite _sprite;
        
        public IconButton(Sprite sprite, Action<int> clickCallback) : base(clickCallback)
        {
            _sprite = sprite;
        }

        public Sprite sprite
        {
            get => _sprite;
            set
            {
                _sprite = value;
                SetDirty();
            }
        }

        public override RectangleF BuildContent(RenderBatch batch, LayoutPosition location, Alignment align)
        {
            var rect = location.Rect(1f, 1f, align);
            batch.DrawSprite(rect, sprite, SchemeColor.PrimaryText);
            return rect;
        }
    }

    public class ContentButton : ButtonBase
    {
        private IWidget _content;
        public IWidget content
        {
            get => _content;
            set
            {
                _content = value;
                SetDirty();
            }
        }

        public override RectangleF BuildContent(RenderBatch batch, LayoutPosition location, Alignment align) => _content.Build(batch, location, align);

        public ContentButton(IWidget content, Action<int> clickCallback) : base(clickCallback)
        {
            _content = content;
        }
    }
}