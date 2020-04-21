using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using SDL2;

namespace YAFC.UI
{
    public class InputField : WidgetContainer, IKeyboardFocus, IMouseDragHandle
    {
        private readonly FontString contents;
        private string _text = "";
        private int caret, selectionAnchor;
        private bool caretVisible;
        private bool focused;
        private long nextCaretTimer;
        private Stack<string> editHistory;
        private EditHistoryEvent lastEvent;
        private Vector2 textWindowOffset;
        private string _placeholder;
        public Action onChange;
        private readonly Icon icon;

        public string text
        {
            get => _text;
            set
            {
                if (value == null)
                    value = string.Empty;
                if (_text == value)
                    return;
                _text = value;
                onChange?.Invoke();
                Rebuild();
            }
        }

        public string placeholder
        {
            get => _placeholder;
            set
            {
                _placeholder = value;
                if (string.IsNullOrEmpty(text))
                    Rebuild();
            }
        }
        
        private enum EditHistoryEvent
        {
            None, Delete, Input
        }

        private void AddEditHistory(EditHistoryEvent evt)
        {
            if (evt == lastEvent)
                return;
            if (editHistory == null)
                editHistory = new Stack<string>();
            if (editHistory.Count == 0 || editHistory.Peek() != text)
            {
                lastEvent = evt;
                editHistory.Push(text);
            }
        }

        public InputField(Font font, Icon icon = Icon.None)
        {
            contents = new FontString(font, "");
            this.icon = icon;
            padding = new Padding(0.8f, 0.5f);
        }
        
        public override SchemeColor boxColor => SchemeColor.Grey;

        private float GetCharacterPosition(int id, UiBatch batch)
        {
            if (id == 0)
                return 0;
            if (id == text.Length)
                return contents.textSize.X;
            SDL_ttf.TTF_SizeUNICODE(contents.font.GetHandle(batch), text.Substring(0, id), out var w, out _);
            return batch.PixelsToUnits(w);
        }
        
        protected override void BuildContent(LayoutState state)
        {
            if (string.IsNullOrEmpty(_text) && !focused)
            {
                contents.text = placeholder;
                contents.SetTransparent(true);
            }
            else
            {
                contents.text = _text;
                contents.SetTransparent(false);
            }

            if (icon != Icon.None)
            {
                state.allocator = RectAllocator.LeftRow;
                state.BuildIcon(icon, SchemeColor.BlackTransparent, contents.font.size);
                state.AllocateSpacing(0.5f);
                state.allocator = RectAllocator.RemainigRow;
            }
            
            contents.Build(state);
            var textPosition = state.lastRect;
            textWindowOffset = state.batch.offset + new Vector2(textPosition.X, textPosition.Y);
            var lineSize = contents.font.GetLineSize(state.batch);
            if (selectionAnchor != caret)
            {
                var left = GetCharacterPosition(Math.Min(selectionAnchor, caret), state.batch);
                var right = GetCharacterPosition(Math.Max(selectionAnchor, caret), state.batch);
                state.batch.DrawRectangle(new Rect(left + textPosition.X, textPosition.Bottom - lineSize, right-left, lineSize), SchemeColor.TextSelection);
            } 
            else if (caretVisible)
            {
                var caretPosition = GetCharacterPosition(caret, state.batch);
                state.batch.DrawRectangle(new Rect(caretPosition + textPosition.X - 0.05f, textPosition.Bottom - lineSize, 0.1f, lineSize), contents.color);
            }
            
            if (focused && selectionAnchor == caret)
                state.batch.SetNextRebuild(nextCaretTimer);
        }

        public void BeginDrag(Vector2 position, int button, UiBatch batch)
        {
            var pos = FindCaretIndex((position - textWindowOffset).X, batch);
            SetCaret(pos);
        }

        public void MouseClick(int button, UiBatch batch)
        {
            InputSystem.Instance.SetKeyboardFocus(this);
        }

        private void SetCaret(int position, int selection = -1)
        {
            position = Math.Min(position, text.Length);
            selection = selection < 0 ? position : Math.Min(selection, text.Length);
            if (caret != position || selectionAnchor != selection)
            {
                caret = position;
                selectionAnchor = selection;
                ResetCaret();
                Rebuild();
            }
        }

        public string selectedText => text.Substring(Math.Min(selectionAnchor, caret), Math.Abs(selectionAnchor - caret));

        private void DeleteSelected()
        {
            AddEditHistory(EditHistoryEvent.Delete);
            var pos = Math.Min(selectionAnchor, caret);
            text = text.Remove(pos, Math.Abs(selectionAnchor - caret));
            selectionAnchor = caret = pos;
            Rebuild();
        }

        public void KeyDown(SDL.SDL_Keysym key)
        {
            var ctrl = (key.mod & SDL.SDL_Keymod.KMOD_CTRL) != 0;
            var shift = (key.mod & SDL.SDL_Keymod.KMOD_SHIFT) != 0;
            switch (key.scancode)
            {
                case SDL.SDL_Scancode.SDL_SCANCODE_BACKSPACE:
                    if (selectionAnchor != caret)
                        DeleteSelected();
                    else if (caret > 0)
                    {
                        var removeFrom = caret;
                        if (ctrl)
                        {
                            var stopOnNextNonLetter = false;
                            while (removeFrom > 0)
                            {
                                removeFrom--;
                                if (char.IsLetterOrDigit(text[removeFrom]))
                                    stopOnNextNonLetter = true;
                                else if (stopOnNextNonLetter)
                                {
                                    removeFrom++;
                                    break;
                                }
                            }
                        }
                        else
                            removeFrom--;
                        AddEditHistory(EditHistoryEvent.Delete);
                        text = text.Remove(removeFrom, caret - removeFrom);
                        SetCaret(removeFrom);
                    }
                    break;
                case SDL.SDL_Scancode.SDL_SCANCODE_DELETE:
                    if (selectionAnchor != caret)
                        DeleteSelected();
                    else if (caret < text.Length)
                    {
                        AddEditHistory(EditHistoryEvent.Delete);
                        text = text.Remove(caret, 1);
                    }
                    break;
                case SDL.SDL_Scancode.SDL_SCANCODE_RETURN: case SDL.SDL_Scancode.SDL_SCANCODE_RETURN2:
                    InputSystem.Instance.SetKeyboardFocus(null);
                    break;
                case SDL.SDL_Scancode.SDL_SCANCODE_LEFT:
                    if (shift)
                        SetCaret(caret-1, selectionAnchor);
                    else SetCaret(selectionAnchor == caret ? caret-1 : Math.Min(selectionAnchor, caret));
                    break;
                case SDL.SDL_Scancode.SDL_SCANCODE_RIGHT:
                    if (shift)
                        SetCaret(caret+1, selectionAnchor);
                    else SetCaret(selectionAnchor == caret ? caret + 1 : Math.Max(selectionAnchor, caret));
                    break;
                case SDL.SDL_Scancode.SDL_SCANCODE_HOME:
                    SetCaret(0, shift ? selectionAnchor : 0);
                    break;
                case SDL.SDL_Scancode.SDL_SCANCODE_END:
                    SetCaret(int.MaxValue, shift ? selectionAnchor : int.MaxValue);
                    break;
                case SDL.SDL_Scancode.SDL_SCANCODE_V when ctrl && SDL.SDL_HasClipboardText() == SDL.SDL_bool.SDL_TRUE: 
                    TextInput(SDL.SDL_GetClipboardText());
                    break;
                case SDL.SDL_Scancode.SDL_SCANCODE_C when ctrl && selectionAnchor != caret:
                    SDL.SDL_SetClipboardText(selectedText);
                    break;
                case SDL.SDL_Scancode.SDL_SCANCODE_X when ctrl && selectionAnchor != caret:
                    SDL.SDL_SetClipboardText(selectedText);
                    DeleteSelected();
                    break;
                case SDL.SDL_Scancode.SDL_SCANCODE_Z when ctrl && editHistory != null && editHistory.Count > 0:
                    text = editHistory.Pop();
                    SetCaret(text.Length);
                    lastEvent = EditHistoryEvent.None;
                    break;
                case SDL.SDL_Scancode.SDL_SCANCODE_A when ctrl:
                    SetCaret(text.Length, 0);
                    break;
            }
        }
        public void KeyUp(SDL.SDL_Keysym key) {}

        private void ResetCaret()
        {
            caretVisible = true;
            nextCaretTimer = Ui.time + 500;
        }

        public void TextInput(string input)
        {
            if (input.IndexOf(' ') >= 0)
                lastEvent = EditHistoryEvent.None;
            AddEditHistory(EditHistoryEvent.Input);
            if (selectionAnchor != caret)
                DeleteSelected();
            text = text.Insert(caret, input);
            SetCaret(caret + input.Length);
            ResetCaret();
        }

        public void FocusChanged(bool focused)
        {
            this.focused = focused;
            if (focused)
            {
                lastEvent = EditHistoryEvent.None;
                ResetCaret();
            }
            else
            {
                editHistory = null;
                selectionAnchor = caret;
                caretVisible = false;
            }
            Rebuild();
        }

        public void UpdateSelected()
        {
            if (nextCaretTimer <= Ui.time)
            {
                nextCaretTimer = Ui.time + 500;
                caretVisible = !caretVisible;
                Rebuild();
            }
        }

        public void MouseEnter(HitTestResult<IMouseHandle> hitTest)
        {
            SDL.SDL_SetCursor(RenderingUtils.cursorCaret);
        }

        public void MouseExit(UiBatch batch)
        {
            SDL.SDL_SetCursor(RenderingUtils.cursorArrow);
        }

        // Fast operations with char* instead of strings
        [DllImport("SDL2_ttf.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int TTF_SizeUNICODE(IntPtr font, char* text, out int w, out int h);
         
        private unsafe int FindCaretIndex(float position, UiBatch batch)
        {
            int min = 0, max = text.Length;
            if (position <= 0f || max == 0)
                return 0;
            float minW = 0f, maxW = contents.textSize.X;
            if (position >= maxW)
                return max;
            
            var handle = contents.font.GetHandle(batch);
            fixed (char* arr = text)
            {
                while (max > min + 1)
                {
                    var ratio = (maxW - position) / (maxW - minW);
                    var mid = MathUtils.Clamp(MathUtils.Round(min * ratio + max * (1f - ratio)) , min+1, max-1);
                    var prev = arr[mid];
                    arr[mid] = '\0';
                    TTF_SizeUNICODE(handle, arr, out var w, out _);
                    arr[mid] = prev;
                    var midW = batch.PixelsToUnits(w);
                    if (midW > position)
                    {
                        max = mid;
                        maxW = midW;
                    }
                    else
                    {
                        min = mid;
                        minW = midW;
                    }
                }
            }

            return maxW - position > position - minW ? min : max;
        }

        public void Drag(Vector2 position, int button, UiBatch batch)
        {
            var pos = FindCaretIndex((position - textWindowOffset).X, batch);
            SetCaret(pos, selectionAnchor);
        }

        public void EndDrag(Vector2 position, int button, UiBatch batch)
        {
            InputSystem.Instance.SetKeyboardFocus(this);
        }
    }
}