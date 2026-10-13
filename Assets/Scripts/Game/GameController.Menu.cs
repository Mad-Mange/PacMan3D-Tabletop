using System.Collections.Generic;
using PacMan.Core;
using UnityEngine;

namespace PacMan.Game
{
    /// <summary>
    /// The title menu, the pause menu and the OPTIONS page (volumes, camera, fullscreen). Keyboard, gamepad and
    /// mouse all work: arrows/d-pad move and change values, Enter/Space/A pick, Esc/B go back, clicks pick or set a bar.
    /// </summary>
    public sealed partial class GameController
    {
        enum Page { None, Title, Pause, Options }

        static readonly string[] TitleItems = { "START GAME", "OPTIONS", "QUIT" };
        static readonly string[] PauseItems = { "RESUME", "OPTIONS", "QUIT TO TITLE" };
        static readonly string[] OptionItems = { "MASTER", "EFFECTS", "MUSIC", "SIREN", "CAMERA", "FULLSCREEN", "BACK" };
        const int VolumeRows = 4, CameraRow = 4, FullscreenRow = 5, BackRow = 6;

        Page page, optionsReturn;
        int selected;
        float idleTime;
        /// <summary>Hit areas from the last drawn menu, in GUI pixels, indexed by item.</summary>
        readonly List<Rect> itemRects = new List<Rect>();
        readonly List<Rect> barRects = new List<Rect>();
        Texture2D cursorIcon;

        int ItemCount => page == Page.Title ? TitleItems.Length : page == Page.Pause ? PauseItems.Length : OptionItems.Length;

        // ------------------------------------------------------------------ input

        void HandleMenu()
        {
            int before = selected;
            var d = Controls.MenuDirection();
            bool acted = d != Dir.None;
            if (d == Dir.Up) selected = (selected + ItemCount - 1) % ItemCount;
            else if (d == Dir.Down) selected = (selected + 1) % ItemCount;
            else if (page == Page.Options && (d == Dir.Left || d == Dir.Right)) Adjust(selected, d == Dir.Right ? 1 : -1, false);

            var pointer = Controls.Pointer;
            int hovered = -1;
            if (pointer.HasValue)
                for (int i = 0; i < itemRects.Count; i++)
                    if (itemRects[i].Contains(pointer.Value)) hovered = i;
            if (hovered >= 0 && Controls.PointerMoved) selected = hovered;
            if (selected != before) sfx.Blip();

            if (hovered >= 0 && Controls.PointerClicked)
            {
                acted = true;
                selected = hovered;
                if (page == Page.Options && hovered < VolumeRows && barRects[hovered].Contains(pointer.Value))
                    SetVolume(hovered, (pointer.Value.x - barRects[hovered].x) / barRects[hovered].width);
                else Activate(hovered);
            }
            else if (Controls.ConfirmPressed())
            {
                acted = true;
                Activate(selected);
            }
            else if (Controls.CancelPressed() || (page == Page.Pause && Controls.PausePressed()))
            {
                acted = true;
                Back();
            }
            if (acted || Controls.PointerMoved) idleTime = 0f;
        }

        void Activate(int item)
        {
            switch (page)
            {
                case Page.Title:
                    if (item == 0) StartGame(false);
                    else if (item == 1) OpenOptions(Page.Title);
                    else Quit();
                    break;
                case Page.Pause:
                    if (item == 0) Resume();
                    else if (item == 1) OpenOptions(Page.Pause);
                    else
                    {
                        SaveHighScore();
                        EnterTitle();
                    }
                    break;
                case Page.Options:
                    if (item == BackRow) Back();
                    else Adjust(item, 1, true);
                    break;
            }
        }

        void Back()
        {
            sfx.Blip();
            if (page == Page.Options)
            {
                page = optionsReturn;
                selected = 1;
            }
            else if (page == Page.Pause)
            {
                Resume();
            }
        }

        /// <summary>Opens the pause menu (P, Esc or Start during a game).</summary>
        public void PauseGame()
        {
            if (mode != Mode.Playing) return;
            paused = true;
            page = Page.Pause;
            selected = 0;
            sfx.StopLoop();
        }

        /// <summary>Steps back out of the current menu page, like Esc.</summary>
        public void CloseMenu() => Back();

        void Resume()
        {
            paused = false;
            page = Page.None;
        }

        /// <summary>Opens the OPTIONS page from wherever the player is (title or pause).</summary>
        public void OpenOptions()
        {
            if (page == Page.None) PauseGame();
            OpenOptions(page == Page.None ? Page.Title : page);
        }

        void OpenOptions(Page from)
        {
            optionsReturn = from;
            page = Page.Options;
            selected = 0;
            sfx.Blip();
        }

        void Adjust(int row, int delta, bool wrap)
        {
            if (row < VolumeRows)
            {
                float v = Volume(row);
                float next = Settings.Step(v, delta);
                if (wrap && Mathf.Approximately(next, v)) next = 0f;
                SetVolume(row, next);
                return;
            }
            if (row == CameraRow)
            {
                ToggleCamera();
                sfx.Blip();
            }
            else if (row == FullscreenRow)
            {
                SetFullscreen(!IsFullscreen);
                sfx.Blip();
            }
        }

        static float Volume(int row)
        {
            switch (row)
            {
                case 0: return Settings.Master;
                case 1: return Settings.Effects;
                case 2: return Settings.Music;
                default: return Settings.Siren;
            }
        }

        void SetVolume(int row, float value)
        {
            value = Settings.Step(Mathf.Clamp01(value), 0);
            switch (row)
            {
                case 0: Settings.Master = value; break;
                case 1: Settings.Effects = value; break;
                case 2: Settings.Music = value; break;
                default: Settings.Siren = value; break;
            }
            Settings.Save();
            sfx.ApplyVolumes();
            sfx.Blip();
        }

        static bool IsFullscreen => Screen.fullScreenMode != FullScreenMode.Windowed;

        static void SetFullscreen(bool on)
        {
            if (on) Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
            else Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
        }

        // ------------------------------------------------------------------ drawing

        /// <summary>Menu items centred under each other; the selected one is yellow with a Pac-Man cursor.</summary>
        void DrawItems(string[] items, float centerX, float y, float cell, float spacing)
        {
            for (int i = 0; i < items.Length; i++)
            {
                bool on = i == selected;
                float w = hud.TextWidth(items[i]);
                var rect = new Rect(centerX - w * 0.5f - cell * 1.5f, y + i * spacing - cell * 0.45f, w + cell * 3f, cell * 1.8f);
                itemRects.Add(rect);
                barRects.Add(Rect.zero);
                hud.Text(items[i], centerX, y + i * spacing, on ? HudYellow : HudWhite, Hud.Align.Center);
                if (on) hud.Image(cursorIcon, new Rect(rect.x - cell * 0.3f, y + i * spacing - cell * 0.25f, cell * 1.4f, cell * 1.4f), Color.white);
            }
        }

        void DrawPause(float w, float h, float cell)
        {
            float pw = Mathf.Min(w * 0.94f, cell * 26f), ph = cell * 13f;
            var panel = new Rect(w * 0.5f - pw * 0.5f, h * 0.5f - ph * 0.5f, pw, ph);
            hud.Panel(new Rect(0f, 0f, w, h), new Color(0f, 0f, 0f, 0.35f));
            hud.Panel(panel, new Color(0f, 0f, 0.04f, 0.82f));
            hud.Text("PAUSED", w * 0.5f, panel.y + cell * 1.2f, HudYellow, Hud.Align.Center, 2f);
            DrawItems(PauseItems, w * 0.5f, panel.y + cell * 5.4f, cell, cell * 2.2f);
        }

        void DrawOptions(Rect panel, float cell)
        {
            hud.Panel(panel, new Color(0f, 0f, 0.04f, 0.85f));
            float cx = panel.center.x;
            hud.Text("OPTIONS", cx, panel.y + cell * 1.2f, HudYellow, Hud.Align.Center, 2f);

            float left = panel.x + cell * 2.5f, right = panel.xMax - cell * 2.5f;
            float y = panel.y + cell * 5f, spacing = cell * 2.1f;
            for (int i = 0; i < OptionItems.Length; i++, y += spacing)
            {
                bool on = i == selected;
                var color = on ? HudYellow : HudWhite;
                var row = new Rect(left - cell, y - cell * 0.5f, right - left + cell * 2f, cell * 1.9f);
                var bar = Rect.zero;
                if (on) hud.Panel(row, new Color(1f, 0.85f, 0.2f, 0.08f));

                if (i == BackRow)
                {
                    y += cell * 0.6f;
                    row.y += cell * 0.6f;
                    hud.Text("BACK", cx, y, color, Hud.Align.Center);
                    if (on) hud.Image(cursorIcon, new Rect(cx - hud.TextWidth("BACK") * 0.5f - cell * 1.8f, y - cell * 0.25f, cell * 1.4f, cell * 1.4f), Color.white);
                }
                else
                {
                    hud.Text(OptionItems[i], left, y, color);
                    if (i < VolumeRows)
                    {
                        // Ten blocks, like a sound meter.
                        float block = cell * 0.85f, gap = hud.Px * 2f;
                        float barW = Settings.VolumeSteps * (block + gap) - gap;
                        bar = new Rect(right - barW, y - hud.Px, barW, cell + hud.Px * 2f);
                        int filled = Mathf.RoundToInt(Volume(i) * Settings.VolumeSteps);
                        for (int b = 0; b < Settings.VolumeSteps; b++)
                        {
                            var c = b < filled ? (on ? HudYellow : new Color(0.3f, 0.75f, 1f)) : new Color(0.2f, 0.2f, 0.28f);
                            hud.Panel(new Rect(bar.x + b * (block + gap), y, block, cell), c);
                        }
                    }
                    else
                    {
                        string value = i == CameraRow ? (rig.Preferred == CameraRig.View.Follow ? "FOLLOW" : "OVERVIEW") : (IsFullscreen ? "ON" : "OFF");
                        hud.Text(on ? "< " + value + " >" : value, right, y, color, Hud.Align.Right);
                    }
                    if (on) hud.Image(cursorIcon, new Rect(left - cell * 2.2f, y - cell * 0.25f, cell * 1.4f, cell * 1.4f), Color.white);
                }
                itemRects.Add(row);
                barRects.Add(bar);
            }

            float small = hud.Px >= 2f ? Mathf.Floor(hud.Px * 0.5f) / hud.Px : 1f;
            hud.Text("ARROWS CHANGE   ENTER SELECT   ESC BACK", cx, panel.yMax - cell * 1.5f,
                new Color(0.7f, 0.7f, 0.78f), Hud.Align.Center, small);
        }
    }
}
