using System;
using System.Collections.Generic;
using System.Drawing;
using SDL2;

namespace UI
{
    public class InputField : WidgetContainer, IMouseClickHandle, IKeyboardFocus, IMouseEnterHandle
    {
        private readonly FontString contents;
        private int caret, selectionAnchor;
        private bool caretVisible;
        private long nextCaretTimer;
        private Stack<string> editHistory;
        private EditHistoryEvent lastEvent;
        
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
            if (editHistory.Count == 0 || editHistory.Peek() != contents.text)
            {
                lastEvent = evt;
                editHistory.Push(contents.text);
            }
        }

        public InputField(Font font)
        {
            contents = new FontString(font, false, "");
        }
        
        public override SchemeColor boxColor => SchemeColor.BackgroundAlt;

        private float GetCharacterPosition(int id)
        {
            if (id == contents.text.Length)
                return contents.textSize.Width;
            if (id == 0)
                return 0;
            SDL_ttf.TTF_SizeUNICODE(contents.font.GetFontHandle(), contents.text.Substring(0, id), out var w, out _);
            return RenderingUtils.PixelsToUnits(w);
        }
        
        protected override LayoutPosition BuildContent(RenderBatch batch, LayoutPosition location)
        {
            var textPosition = location.Align(Alignment.Left);
            textPosition = contents.Build(batch, textPosition);
            if (selectionAnchor != caret)
            {
                var left = GetCharacterPosition(Math.Min(selectionAnchor, caret));
                var right = GetCharacterPosition(Math.Max(selectionAnchor, caret));
                batch.DrawRectangle(new RectangleF(left + textPosition.x1, textPosition.y - contents.font.lineSize, right-left, contents.font.lineSize), SchemeColor.TextSelection);
                
            } else if (caretVisible)
            {
                var caretPosition = GetCharacterPosition(caret);
                batch.DrawRectangle(new RectangleF(caretPosition + textPosition.x1 - 0.05f, textPosition.y - contents.font.lineSize, 0.1f, contents.font.lineSize), contents.color);
            }
            location.Pad(textPosition);
            return location;
        }

        public void MouseClickUpdateState(bool mouseOverAndDown, int button) {}

        public void MouseClick(int button)
        {
            InputSystem.Instance.SetKeyboardFocus(this);
        }

        private void SetCaret(int position, int selection = -1)
        {
            position = Math.Min(position, contents.text.Length);
            selection = selection < 0 ? position : Math.Min(selection, contents.text.Length);
            if (caret != position || selectionAnchor != selection)
            {
                caret = position;
                selectionAnchor = selection;
                SetDirty();
            }
        }

        public string selectedText => contents.text.Substring(Math.Min(selectionAnchor, caret), Math.Abs(selectionAnchor - caret));

        private void DeleteSelected()
        {
            AddEditHistory(EditHistoryEvent.Delete);
            var pos = Math.Min(selectionAnchor, caret);
            contents.text = contents.text.Remove(pos, Math.Abs(selectionAnchor - caret));
            selectionAnchor = caret = pos;
            SetDirty();
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
                                if (char.IsLetterOrDigit(contents.text[removeFrom]))
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
                        contents.text = contents.text.Remove(removeFrom, caret - removeFrom);
                        SetCaret(removeFrom);
                    }
                    break;
                case SDL.SDL_Scancode.SDL_SCANCODE_DELETE:
                    if (selectionAnchor != caret)
                        DeleteSelected();
                    else if (caret < contents.text.Length)
                    {
                        AddEditHistory(EditHistoryEvent.Delete);
                        contents.text = contents.text.Remove(caret, 1);
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
                    contents.text = editHistory.Pop();
                    SetCaret(contents.text.Length);
                    lastEvent = EditHistoryEvent.None;
                    break;
            }
        }
        public void KeyUp(SDL.SDL_Keysym key) {}

        private void ResetCaret()
        {
            caretVisible = true;
            nextCaretTimer = InputSystem.time + 500;
        }

        public void TextInput(string input)
        {
            if (input.IndexOf(' ') >= 0)
                lastEvent = EditHistoryEvent.None;
            AddEditHistory(EditHistoryEvent.Input);
            if (selectionAnchor != caret)
                DeleteSelected();
            contents.text = contents.text.Insert(caret, input);
            SetCaret(caret + input.Length);
            ResetCaret();
        }

        public void FocusChanged(bool focused)
        {
            if (focused)
            {
                lastEvent = EditHistoryEvent.None;
                ResetCaret();
            }
            else
            {
                editHistory = null;
                caretVisible = false;
            }
            SetDirty();
        }

        public void UpdateSelected()
        {
            if (nextCaretTimer <= InputSystem.time)
            {
                nextCaretTimer = InputSystem.time + 500;
                caretVisible = !caretVisible;
                SetDirty();
            }
        }

        public void MouseEnter()
        {
            SDL.SDL_SetCursor(RenderingUtils.cursorCaret);
        }

        public void MouseExit()
        {
            SDL.SDL_SetCursor(RenderingUtils.cursorArrow);
        }
    }
}