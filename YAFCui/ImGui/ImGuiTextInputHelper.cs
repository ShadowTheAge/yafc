using System;
using System.Collections.Generic;
using System.Reflection.PortableExecutable;
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

        public void SetFocus(Rect boundingRect, string setText)
        {
            if (boundingRect == prevRect)
            {
                text = prevText;
                prevRect = default;
            }
            else
            {
                editHistory.Clear();
                text = setText;
            }
            InputSystem.Instance.SetKeyboardFocus(this);
            rect = boundingRect;
            caret = selectionAnchor = 0;
        }

        private void GetTextParameters(string textToBuild, Rect textRect, FontFile.FontSize fontSize, RectAlignment alignment, out TextCache cachedText, out float scale, out float textWidth, out Rect realTextRect)
        {
            realTextRect = textRect;
            scale = 1f;
            textWidth = 0f;
            if (!string.IsNullOrEmpty(textToBuild))
            {
                cachedText = gui.textCache.GetCached((fontSize, textToBuild, uint.MaxValue));
                textWidth = gui.PixelsToUnits(cachedText.texRect.w);
                if (textWidth > realTextRect.Width)
                    scale = realTextRect.Width / textWidth;
                else realTextRect = ImGui.AlignRect(textRect, alignment, textWidth, textRect.Height);
            }
            else
            {
                realTextRect = ImGui.AlignRect(textRect, alignment, 0f, textRect.Height);
                cachedText = null;
            }
        }

        public bool BuildTextInput(string text, out string newText, string placeholder, FontFile.FontSize fontSize, bool delayed, Icon icon, Padding padding, RectAlignment alignment, SchemeColor color)
        {
            newText = text;
            Rect textRect, realTextRect;
            using (gui.EnterGroup(padding, RectAllocator.LeftRow))
            {
                var lineSize = gui.PixelsToUnits(fontSize.lineSize);
                if (icon != Icon.None)
                    gui.BuildIcon(icon, lineSize, color+3); 
                textRect = gui.RemainingRow(0.3f).AllocateRect(0, lineSize);
            }
            var boundingRect = gui.lastRect;
            var focused = rect == boundingRect;
            if (focused && this.text == null)
            {
                this.text = text ?? "";
                SetCaret(0, text.Length);
            }

            switch (gui.action)
            {
                case ImGuiAction.MouseDown:
                    if (gui.actionParameter != SDL.SDL_BUTTON_LEFT)
                        break;
                    if (gui.ConsumeMouseDown(boundingRect))
                    {
                        SetFocus(boundingRect, text ?? "");
                        GetTextParameters(this.text, textRect, fontSize, alignment, out _, out _, out _, out realTextRect);
                        SetCaret(FindCaretIndex(text, gui.mousePosition.X - realTextRect.X, fontSize, textRect.Width));
                    }
                    break;
                case ImGuiAction.MouseMove:
                    if (focused && gui.actionParameter == SDL.SDL_BUTTON_LEFT)
                    {
                        GetTextParameters(this.text, textRect, fontSize, alignment, out _, out _, out _, out realTextRect);
                        SetCaret(caret, FindCaretIndex(this.text, gui.mousePosition.X - realTextRect.X, fontSize, textRect.Width));
                    }
                    gui.ConsumeMouseOver(boundingRect, RenderingUtils.cursorCaret, false);
                    break;
                case ImGuiAction.Build:
                    var textColor = color+2;
                    string textToBuild;
                    if (focused)
                        textToBuild = this.text;
                    else if (string.IsNullOrEmpty(text))
                    {
                        textToBuild = placeholder;
                        textColor = color+3;
                    }
                    else textToBuild = text;
                    
                    GetTextParameters(textToBuild, textRect, fontSize, alignment, out var cachedText, out var scale, out var textWidth, out realTextRect);
                    if (cachedText != null)
                        gui.DrawRenderable(realTextRect, cachedText, textColor);

                    if (focused)
                    {
                        if (selectionAnchor != caret)
                        {
                            var left = GetCharacterPosition(Math.Min(selectionAnchor, caret), fontSize, textWidth) * scale;
                            var right = GetCharacterPosition(Math.Max(selectionAnchor, caret), fontSize, textWidth) * scale;
                            gui.DrawRectangle(new Rect(left + realTextRect.X, realTextRect.Y, right-left, realTextRect.Height), SchemeColor.TextSelection);
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
                                var caretPosition = GetCharacterPosition(caret, fontSize, textWidth) * scale;
                                gui.DrawRectangle(new Rect(caretPosition + realTextRect.X - 0.05f, realTextRect.Y, 0.1f, realTextRect.Height), color+2);
                            }
                        }
                    }
                    gui.DrawRectangle(boundingRect, color);
                    break;
            }
            
            if (boundingRect == prevRect)
            {
                var changed = text != prevText;
                if (changed)
                    newText = prevText;
                prevRect = default;
                prevText = null;
                return changed;
            }
            
            if (focused && !delayed && this.text != text)
            {
                newText = this.text;
                return true;
            }

            return false;
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
                rect = default;
                text = null;
                gui.Rebuild();
            }
        }
        
        // Fast operations with char* instead of strings
        [DllImport("SDL2_ttf.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int TTF_SizeUNICODE(IntPtr font, char* text, out int w, out int h);
         
        private unsafe int FindCaretIndex(string text, float position, FontFile.FontSize fontSize, float maxWidth)
        {
            if (string.IsNullOrEmpty(text) || position <= 0f)
                return 0;
            var cachedText = gui.textCache.GetCached((fontSize, text, uint.MaxValue));
            var maxW = gui.PixelsToUnits(cachedText.texRect.w);
            var scale = 1f;
            if (maxW > maxWidth)
            {
                scale = maxWidth / maxW;
                maxW = maxWidth;
            }
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
                    var midW = gui.PixelsToUnits(w) * scale;
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