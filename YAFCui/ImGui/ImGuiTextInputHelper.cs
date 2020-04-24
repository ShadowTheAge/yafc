using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SDL2;

namespace YAFC.UI
{
    internal class ImGuiTextInputHelper : IKeyboardFocus
    {
        private readonly ImGui gui;
        
        public ImGuiTextInputHelper(ImGui gui)
        {
            this.gui = gui;
        }

        private string prevText;
        private Rect prevRect;
        private string text;
        private Rect rect;
        private readonly Stack<string> editHistory = new Stack<string>();
        private EditHistoryEvent lastEvent;
        
        private int caret, selectionAnchor;
        private Font font => Font.text;
        private bool caretVisible = true;
        private long nextCaretTimer;

        public bool BuildTextInput(string text, out string newText, string placeholder, FontFile.FontSize fontSize)
        {
            newText = text;
            var changed = false;
            Rect textRect;
            using (gui.EnterGroup(new Padding(0.8f, 0.5f), RectAllocator.Stretch))
            {
                textRect = gui.AllocateRect(0, gui.PixelsToUnits(fontSize.lineSize));
            }
            var boundingRect = gui.lastRect;
            var focused = rect == boundingRect;
            
            switch (gui.action)
            {
                case ImGuiAction.MouseDown:
                    if (focused)
                    {
                        if (gui.ConsumeMouseDown(boundingRect))
                        {
                            InputSystem.Instance.SetKeyboardFocus(this);
                            SetCaret(FindCaretIndex(gui.mousePosition.X - textRect.X, fontSize));
                        } 
                        else if (text != this.text)
                        {
                            newText = this.text;
                            changed = true;
                        }
                        rect = default;
                    }
                    else
                    {
                        if (gui.ConsumeMouseDown(boundingRect))
                        {
                            editHistory.Clear();
                            InputSystem.Instance.SetKeyboardFocus(this);
                            prevRect = rect;
                            prevText = this.text;
                            rect = boundingRect;
                            this.text = text ?? "";
                            SetCaret(FindCaretIndex(gui.mousePosition.X - textRect.X, fontSize));
                        }
                    }
                    break;
                case ImGuiAction.MouseMove:
                    if (focused && gui.eventArg == SDL.SDL_BUTTON_LEFT)
                        SetCaret(caret, FindCaretIndex(gui.mousePosition.X - textRect.X, fontSize));
                    gui.ConsumeMouseOver(boundingRect, RenderingUtils.cursorCaret, false);
                    break;
                case ImGuiAction.Build:
                    var textToBuild = focused ? this.text : string.IsNullOrEmpty(text) ? placeholder : text;
                    var realTextRect = textRect;
                    if (!string.IsNullOrEmpty(textToBuild))
                    {
                        var cachedText = gui.textCache.GetCached((fontSize, textToBuild, uint.MaxValue));
                        realTextRect.Width = gui.PixelsToUnits(cachedText.texRect.w);
                        gui.DrawRenderable(realTextRect, cachedText, SchemeColor.GreyText);
                    }
                    else realTextRect.Width = 0;

                    if (focused)
                    {
                        if (selectionAnchor != caret)
                        {
                            var left = GetCharacterPosition(Math.Min(selectionAnchor, caret), fontSize, realTextRect.Width);
                            var right = GetCharacterPosition(Math.Max(selectionAnchor, caret), fontSize, realTextRect.Width);
                            gui.DrawRectangle(new Rect(left + textRect.X, textRect.Y, right-left, textRect.Height), SchemeColor.TextSelection);
                        } 
                        else {
                            if (nextCaretTimer <= Ui.time)
                            {
                                nextCaretTimer = Ui.time + 500;
                                caretVisible = !caretVisible;
                            }
                            gui.SetNextRebuild(nextCaretTimer);
                            if (caretVisible)
                            {
                                var caretPosition = GetCharacterPosition(caret, fontSize, realTextRect.Width);
                                gui.DrawRectangle(new Rect(caretPosition + textRect.X - 0.05f, textRect.Y, 0.1f, textRect.Height), SchemeColor.GreyText);
                            }
                        }
                    }
                    gui.DrawRectangle(boundingRect, SchemeColor.Grey);
                    break;
            }
            
            if (boundingRect == prevRect)
            {
                changed = text != prevText;
                if (changed)
                    newText = prevText;
                prevRect = default;
                prevText = null;
                return changed;
            }

            return changed;
        }

        private float GetCharacterPosition(int id, FontFile.FontSize fontSize, float max)
        {
            if (id == 0)
                return 0;
            if (id == text.Length)
                return max;
            SDL_ttf.TTF_SizeUNICODE(fontSize.handle, text.Substring(0, id), out var w, out _);
            return gui.PixelsToUnits(w);
        }
        
        private void DeleteSelected()
        {
            AddEditHistory(EditHistoryEvent.Delete);
            var pos = Math.Min(selectionAnchor, caret);
            text = text.Remove(pos, Math.Abs(selectionAnchor - caret));
            selectionAnchor = caret = pos;
            gui.Rebuild();
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
                gui.Rebuild();
            }
        }
        
        private void ResetCaret()
        {
            caretVisible = true;
            nextCaretTimer = Ui.time + 500;
        }
        
        private enum EditHistoryEvent
        {
            None, Delete, Input
        }
        
        public string selectedText => text.Substring(Math.Min(selectionAnchor, caret), Math.Abs(selectionAnchor - caret));

        private void AddEditHistory(EditHistoryEvent evt)
        {
            if (evt == lastEvent)
                return;
            if (editHistory.Count == 0 || editHistory.Peek() != text)
            {
                lastEvent = evt;
                editHistory.Push(text);
            }
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

        public void KeyUp(SDL.SDL_Keysym key) {}

        public void FocusChanged(bool focused)
        {
            if (!focused)
            {
                prevRect = rect;
                prevText = text;
                gui.Rebuild();
            }
        }
        
        // Fast operations with char* instead of strings
        [DllImport("SDL2_ttf.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int TTF_SizeUNICODE(IntPtr font, char* text, out int w, out int h);
         
        private unsafe int FindCaretIndex(float position, FontFile.FontSize fontSize)
        {
            if (string.IsNullOrEmpty(text) || position <= 0f)
                return 0;
            var cachedText = gui.textCache.GetCached((fontSize, text, uint.MaxValue));
            var maxW = gui.PixelsToUnits(cachedText.texRect.w);
            int min = 0, max = text.Length;
            var minW = 0f;
            if (position >= maxW)
                return max;

            var handle = fontSize.handle;
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
                    var midW = gui.PixelsToUnits(w);
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
    }
}