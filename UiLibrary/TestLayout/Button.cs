using System;
using System.Drawing;

namespace UI.TestLayout
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

        public override RectangleF Build(RenderBatch batch, BuildLocation buildLocation)
        {
            var rect = base.Build(batch, buildLocation);
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
            set
            {
                fontString.text = value;
                SetDirty();
            }
        }

        public override RectangleF BuildContent(RenderBatch batch, BuildLocation location) => fontString.Build(batch, location);
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

        public override RectangleF BuildContent(RenderBatch batch, BuildLocation location)
        {
            var rect = location.Rect(1f, 1f);
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

        public override RectangleF BuildContent(RenderBatch batch, BuildLocation location) => _content.Build(batch, location);

        public ContentButton(IWidget content, Action<int> clickCallback) : base(clickCallback)
        {
            _content = content;
        }
    }
}