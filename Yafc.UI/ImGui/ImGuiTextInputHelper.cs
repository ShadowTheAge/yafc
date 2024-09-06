using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SDL2;

namespace Yafc.UI {
    internal class ImGuiTextInputHelper : IKeyboardFocus {
        private readonly ImGui gui;

        public ImGuiTextInputHelper(ImGui gui) => this.gui = gui;

        private string prevText = "";
        private Rect prevRect;
        private string text = "";
        private Rect rect;
        private readonly Stack<string> editHistory = new Stack<string>();
        private EditHistoryEvent lastEvent;

        private int caret, selectionAnchor;
        private bool caretVisible = true;
        private long nextCaretTimer;

        public void SetFocus(Rect boundingRect, string setText) {
            setText ??= "";
            if (boundingRect == prevRect) {
                text = prevText;
                prevRect = default;
            }
            else {
                editHistory.Clear();
                text = setText;
            }
            InputSystem.Instance.SetKeyboardFocus(this);
            rect = boundingRect;
            caret = selectionAnchor = setText.Length;
        }

        private void GetTextParameters(string? textToBuild, Rect textRect, FontFile.FontSize fontSize, RectAlignment alignment, out TextCache? cachedText, out float scale, out float textWidth, out Rect realTextRect) {
            realTextRect = textRect;
            scale = 1f;
            textWidth = 0f;
            if (!string.IsNullOrEmpty(textToBuild)) {
                cachedText = gui.textCache.GetCached((fontSize, textToBuild, uint.MaxValue));
                textWidth = gui.PixelsToUnits(cachedText.texRect.w);
                if (textWidth > realTextRect.Width) {
                    scale = realTextRect.Width / textWidth;
                }
                else {
                    realTextRect = ImGui.AlignRect(textRect, alignment, textWidth, textRect.Height);
                }
            }
            else {
                realTextRect = ImGui.AlignRect(textRect, alignment, 0f, textRect.Height);
                cachedText = null;
            }
        }

        public bool BuildTextInput(string? text, out string newText, string? placeholder, FontFile.FontSize fontSize, bool delayed, TextBoxDisplayStyle displayStyle) {
            newText = text ?? "";
            Rect textRect, realTextRect;
            using (gui.EnterGroup(displayStyle.Padding, RectAllocator.LeftRow)) {
                float lineSize = gui.PixelsToUnits(fontSize.lineSize);
                if (displayStyle.Icon != Icon.None) {
                    gui.BuildIcon(displayStyle.Icon, lineSize, (SchemeColor)displayStyle.ColorGroup + 3);
                }

                textRect = gui.RemainingRow(0.3f).AllocateRect(0, lineSize, RectAlignment.MiddleFullRow);
            }
            var boundingRect = gui.lastRect;
            bool focused = rect == boundingRect;
            if (focused && this.text == null) {
                this.text = text ?? "";
                SetCaret(0, this.text.Length);
            }

            switch (gui.action) {
                case ImGuiAction.MouseDown:
                    if (gui.actionParameter != SDL.SDL_BUTTON_LEFT) {
                        break;
                    }

                    if (gui.ConsumeMouseDown(boundingRect)) {
                        SetFocus(boundingRect, text ?? "");
                        GetTextParameters(this.text, textRect, fontSize, displayStyle.Alignment, out _, out _, out _, out realTextRect);
                        SetCaret(FindCaretIndex(text, gui.mousePosition.X - realTextRect.X, fontSize, textRect.Width));
                    }
                    break;
                case ImGuiAction.MouseMove:
                    if (focused && gui.actionParameter == SDL.SDL_BUTTON_LEFT) {
                        GetTextParameters(this.text, textRect, fontSize, displayStyle.Alignment, out _, out _, out _, out realTextRect);
                        SetCaret(caret, FindCaretIndex(this.text, gui.mousePosition.X - realTextRect.X, fontSize, textRect.Width));
                    }
                    _ = gui.ConsumeMouseOver(boundingRect, RenderingUtils.cursorCaret, false);
                    break;
                case ImGuiAction.Build:
                    SchemeColor textColor = (SchemeColor)displayStyle.ColorGroup + 2;
                    string? textToBuild;
                    if (focused && !string.IsNullOrEmpty(text)) {
                        textToBuild = this.text;
                    }
                    else if (string.IsNullOrEmpty(text)) {
                        textToBuild = placeholder;
                        textColor = (SchemeColor)displayStyle.ColorGroup + 3;
                    }
                    else {
                        textToBuild = text;
                    }

                    GetTextParameters(textToBuild, textRect, fontSize, displayStyle.Alignment, out TextCache? cachedText, out float scale, out float textWidth, out realTextRect);
                    if (cachedText != null) {
                        gui.DrawRenderable(realTextRect, cachedText, textColor);
                    }

                    if (focused) {
                        if (selectionAnchor != caret) {
                            float left = GetCharacterPosition(Math.Min(selectionAnchor, caret), fontSize, textWidth) * scale;
                            float right = GetCharacterPosition(Math.Max(selectionAnchor, caret), fontSize, textWidth) * scale;
                            gui.DrawRectangle(new Rect(left + realTextRect.X, realTextRect.Y, right - left, realTextRect.Height), SchemeColor.TextSelection);
                        }
                        else {
                            if (nextCaretTimer <= Ui.time) {
                                nextCaretTimer = Ui.time + 500;
                                caretVisible = !caretVisible;
                            }
                            gui.SetNextRebuild(nextCaretTimer);
                            if (caretVisible) {
                                float caretPosition = GetCharacterPosition(caret, fontSize, textWidth) * scale;
                                gui.DrawRectangle(new Rect(caretPosition + realTextRect.X - 0.05f, realTextRect.Y, 0.1f, realTextRect.Height), (SchemeColor)displayStyle.ColorGroup + 2);
                            }
                        }
                    }
                    gui.DrawRectangle(boundingRect, (SchemeColor)displayStyle.ColorGroup);
                    break;
            }

            if (boundingRect == prevRect) {
                bool changed = text != prevText;
                if (changed) {
                    newText = prevText;
                }

                prevRect = default;
                prevText = "";
                return changed;
            }

            if (focused && !delayed && this.text != text) {
                newText = this.text;
                return true;
            }

            return false;
        }

        private float GetCharacterPosition(int id, FontFile.FontSize fontSize, float max) {
            if (id == 0) {
                return 0;
            }

            if (id == text.Length) {
                return max;
            }

            _ = SDL_ttf.TTF_SizeUNICODE(fontSize.handle, text[..id], out int w, out _);
            return gui.PixelsToUnits(w);
        }

        private void DeleteSelected() {
            AddEditHistory(EditHistoryEvent.Delete);
            int pos = Math.Min(selectionAnchor, caret);
            text = text.Remove(pos, Math.Abs(selectionAnchor - caret));
            selectionAnchor = caret = pos;
            gui.Rebuild();
        }

        private void SetCaret(int position, int selection = -1) {
            position = MathUtils.Clamp(position, 0, text.Length);
            selection = selection < 0 ? position : Math.Min(selection, text.Length);
            if (caret != position || selectionAnchor != selection) {
                caret = position;
                selectionAnchor = selection;
                ResetCaret();
                gui.Rebuild();
            }
        }

        private void ResetCaret() {
            caretVisible = true;
            nextCaretTimer = Ui.time + 500;
        }

        private enum EditHistoryEvent {
            None, Delete, Input
        }

        public string selectedText => text.Substring(Math.Min(selectionAnchor, caret), Math.Abs(selectionAnchor - caret));

        private void AddEditHistory(EditHistoryEvent evt) {
            if (evt == lastEvent) {
                return;
            }

            if (editHistory.Count == 0 || editHistory.Peek() != text) {
                lastEvent = evt;
                editHistory.Push(text);
            }
        }

        public bool KeyDown(SDL.SDL_Keysym key) {
            bool ctrl = (key.mod & SDL.SDL_Keymod.KMOD_CTRL) != 0;
            bool shift = (key.mod & SDL.SDL_Keymod.KMOD_SHIFT) != 0;
            switch (key.scancode) {
                case SDL.SDL_Scancode.SDL_SCANCODE_BACKSPACE:
                    if (selectionAnchor != caret) {
                        DeleteSelected();
                    }
                    else if (caret > 0) {
                        int removeFrom = caret;
                        if (ctrl) {
                            bool stopOnNextNonLetter = false;
                            while (removeFrom > 0) {
                                removeFrom--;
                                if (char.IsLetterOrDigit(text[removeFrom])) {
                                    stopOnNextNonLetter = true;
                                }
                                else if (stopOnNextNonLetter) {
                                    removeFrom++;
                                    break;
                                }
                            }
                        }
                        else {
                            removeFrom--;
                        }

                        AddEditHistory(EditHistoryEvent.Delete);
                        text = text.Remove(removeFrom, caret - removeFrom);
                        SetCaret(removeFrom);
                    }
                    break;
                case SDL.SDL_Scancode.SDL_SCANCODE_DELETE:
                    if (selectionAnchor != caret) {
                        DeleteSelected();
                    }
                    else if (caret < text.Length) {
                        AddEditHistory(EditHistoryEvent.Delete);
                        text = text.Remove(caret, 1);
                    }
                    break;
                case SDL.SDL_Scancode.SDL_SCANCODE_RETURN:
                case SDL.SDL_Scancode.SDL_SCANCODE_RETURN2:
                case SDL.SDL_Scancode.SDL_SCANCODE_KP_ENTER:
                case SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE:
                    InputSystem.Instance.SetKeyboardFocus(null);
                    return false;
                case SDL.SDL_Scancode.SDL_SCANCODE_LEFT:
                    if (shift) {
                        SetCaret(caret - 1, selectionAnchor);
                    }
                    else {
                        SetCaret(selectionAnchor == caret ? caret - 1 : Math.Min(selectionAnchor, caret));
                    }

                    break;
                case SDL.SDL_Scancode.SDL_SCANCODE_RIGHT:
                    if (shift) {
                        SetCaret(caret + 1, selectionAnchor);
                    }
                    else {
                        SetCaret(selectionAnchor == caret ? caret + 1 : Math.Max(selectionAnchor, caret));
                    }

                    break;
                case SDL.SDL_Scancode.SDL_SCANCODE_HOME:
                    SetCaret(0, shift ? selectionAnchor : 0);
                    break;
                case SDL.SDL_Scancode.SDL_SCANCODE_END:
                    SetCaret(int.MaxValue, shift ? selectionAnchor : int.MaxValue);
                    break;
                case SDL.SDL_Scancode.SDL_SCANCODE_V when ctrl && ImGuiUtils.HasClipboardText():
                    _ = TextInput(SDL.SDL_GetClipboardText());
                    break;
                case SDL.SDL_Scancode.SDL_SCANCODE_C when ctrl && selectionAnchor != caret:
                    _ = SDL.SDL_SetClipboardText(selectedText);
                    break;
                case SDL.SDL_Scancode.SDL_SCANCODE_X when ctrl && selectionAnchor != caret:
                    _ = SDL.SDL_SetClipboardText(selectedText);
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

            return true;
        }

        public bool TextInput(string input) {
            if (input.IndexOf(' ') >= 0) {
                lastEvent = EditHistoryEvent.None;
            }

            AddEditHistory(EditHistoryEvent.Input);
            if (selectionAnchor != caret) {
                DeleteSelected();
            }

            text = text.Insert(caret, input);
            SetCaret(caret + input.Length);
            ResetCaret();
            return true;
        }

        public bool KeyUp(SDL.SDL_Keysym key) => true;

        public void FocusChanged(bool focused) {
            if (!focused) {
                prevRect = rect;
                prevText = text;
                rect = default;
                text = "";
                gui.Rebuild();
            }
        }

        // Fast operations with char* instead of strings
        [DllImport("SDL2_ttf.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int TTF_SizeUNICODE(IntPtr font, char* text, out int w, out int h);

        private unsafe int FindCaretIndex(string? text, float position, FontFile.FontSize fontSize, float maxWidth) {
            if (string.IsNullOrEmpty(text) || position <= 0f) {
                return 0;
            }

            var cachedText = gui.textCache.GetCached((fontSize, text, uint.MaxValue));
            float maxW = gui.PixelsToUnits(cachedText.texRect.w);
            float scale = 1f;
            if (maxW > maxWidth) {
                scale = maxWidth / maxW;
                maxW = maxWidth;
            }
            int min = 0, max = text.Length;
            float minW = 0f;
            if (position >= maxW) {
                return max;
            }

            nint handle = fontSize.handle;
            fixed (char* arr = text) {
                while (max > min + 1) {
                    float ratio = (maxW - position) / (maxW - minW);
                    int mid = MathUtils.Clamp(MathUtils.Round((min * ratio) + (max * (1f - ratio))), min + 1, max - 1);
                    char prev = arr[mid];
                    arr[mid] = '\0';
                    _ = TTF_SizeUNICODE(handle, arr, out int w, out _);
                    arr[mid] = prev;
                    float midW = gui.PixelsToUnits(w) * scale;
                    if (midW > position) {
                        max = mid;
                        maxW = midW;
                    }
                    else {
                        min = mid;
                        minW = midW;
                    }
                }
            }

            return maxW - position > position - minW ? min : max;
        }
    }
}
