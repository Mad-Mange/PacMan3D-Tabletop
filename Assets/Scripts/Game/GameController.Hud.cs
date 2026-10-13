using PacMan.Core;
using UnityEngine;

namespace PacMan.Game
{
    public sealed partial class GameController
    {
        static readonly Color HudWhite = new Color(0.95f, 0.95f, 1f);
        static readonly Color HudYellow = new Color(1f, 0.9f, 0.2f);
        static readonly Color HudRed = new Color(1f, 0.15f, 0.15f);
        static readonly Color HudCyan = new Color(0.35f, 0.95f, 1f);
        static readonly Color HudPink = new Color(1f, 0.6f, 0.85f);
        static readonly Color HudPeach = new Color(1f, 0.75f, 0.6f);

        static readonly string[] Nicknames = { "-SHADOW    \"BLINKY\"", "-SPEEDY    \"PINKY\"", "-BASHFUL   \"INKY\"", "-POKEY     \"CLYDE\"" };

        Texture2D lifeIcon, frightIcon, energizerIcon;
        Texture2D[] ghostIcons;

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            DrawHud(Screen.width, Screen.height);
        }

        void EnsureIcons()
        {
            if (lifeIcon != null) return;
            lifeIcon = PixelArt.MakeTexture("Life", 14, 14, PixelArt.PacMan(true));
            cursorIcon = PixelArt.MakeTexture("Cursor", 14, 14, PixelArt.PacMan(false));
            frightIcon = PixelArt.MakeTexture("Fright", 14, 14, PixelArt.FrightenedGhost());
            ghostIcons = new Texture2D[4];
            for (int i = 0; i < 4; i++) ghostIcons[i] = PixelArt.MakeTexture("Ghost" + i, 14, 14, PixelArt.Ghost(PixelArt.GhostColors[i]));
            var dot = new Color32[8 * 8];
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                float dx = x - 3.5f, dy = y - 3.5f;
                if (dx * dx + dy * dy <= 13f) dot[y * 8 + x] = HudPeach;
            }
            energizerIcon = PixelArt.MakeTexture("Energizer", 8, 8, dot);
        }

        void DrawHud(float w, float h)
        {
            EnsureIcons();
            hud.Begin(w, h);
            itemRects.Clear();
            barRects.Clear();
            float cell = 8f * hud.Px;
            bool blink = Mathf.Repeat(time, 0.5f) < 0.3f;

            // Score bar.
            float left = Mathf.Round(w * 0.05f), right = Mathf.Round(w * 0.95f);
            if (mode != Mode.Playing || blink || Sim.Phase != Phase.Playing) hud.Text("1UP", left, cell * 0.8f, HudWhite);
            hud.Text(ScoreText(Sim.Score), left, cell * 1.9f, HudWhite);
            hud.Text("HIGH SCORE", w * 0.5f, cell * 0.8f, HudWhite, Hud.Align.Center);
            hud.Text(ScoreText(Sim.HighScore), w * 0.5f, cell * 1.9f, HudWhite, Hud.Align.Center);
            if (mode != Mode.Title)
            {
                hud.Text("LEVEL", right, cell * 0.8f, HudWhite, Hud.Align.Right);
                hud.Text(Sim.Level.ToString(), right, cell * 1.9f, HudWhite, Hud.Align.Right);
            }

            if (mode == Mode.Title)
            {
                DrawTitle(w, h, cell, blink);
                return;
            }

            // Spare lives and the fruit of recent levels along the bottom.
            float icon = Mathf.Round(cell * 1.75f);
            float bottom = h - icon - cell * 0.6f;
            int spares = Mathf.Max(0, Sim.Lives - 1);
            for (int i = 0; i < Mathf.Min(spares, 6); i++)
                hud.Image(lifeIcon, new Rect(left + i * (icon + hud.Px * 2f), bottom, icon, icon), Color.white);
            int first = Mathf.Max(1, Sim.Level - 6);
            for (int lv = Sim.Level, i = 0; lv >= first; lv--, i++)
            {
                var tex = PixelArt.FruitIcon(LevelSpec.FruitFor(lv));
                if (tex != null) hud.Image(tex, new Rect(right - (i + 1) * (icon + hud.Px * 2f), bottom, icon, icon), Color.white);
            }

            // Messages anchored in the maze, like the arcade.
            var phase = Sim.Phase;
            if (mode == Mode.Demo) WorldText("GAME  OVER", GameSim.FruitPos, HudRed, 1.25f);
            else if (phase == Phase.GameOver) WorldText("GAME  OVER", GameSim.FruitPos, HudRed, 1.25f);
            else if (phase == Phase.Ready) WorldText("READY!", GameSim.FruitPos, HudYellow, 1.25f);
            if (IntroHidesActors) WorldText("PLAYER ONE", GameSim.DoorOutside, HudCyan, 1.25f);

            if (phase == Phase.GhostEaten && Sim.LastEatenGhost != null)
                WorldText(Sim.LastGhostPoints.ToString(), Sim.LastEatenGhost.Pos, HudCyan, 1f, 1.4f);
            if (Sim.FruitScoreTimer > 0f && phase != Phase.Dying)
                WorldText(Sim.LastFruitPoints.ToString(), GameSim.FruitPos, HudPink, 1f, 1.4f);

            if (mode == Mode.Demo)
            {
                if (blink) hud.Text("PRESS ENTER TO PLAY", w * 0.5f, h - cell * 2.2f, HudYellow, Hud.Align.Center);
            }
            else
            {
                const string help = "ARROWS MOVE   C CAMERA   P PAUSE   M MUTE";
                float small = hud.Px >= 2f ? Mathf.Floor(hud.Px * 0.5f) / hud.Px : 1f;
                // Only when it fits between the lives and the fruit.
                if (hud.TextWidth(help, small) < w - 2f * (left + 4f * icon))
                    hud.Text(help, w * 0.5f, h - cell * 1.3f, new Color(0.75f, 0.75f, 0.8f, 0.8f), Hud.Align.Center, small);
            }

            if (page == Page.Pause) DrawPause(w, h, cell);
            else if (page == Page.Options)
            {
                hud.Panel(new Rect(0f, 0f, w, h), new Color(0f, 0f, 0f, 0.35f));
                DrawOptions(CenterPanel(w, h, cell), cell);
            }
        }

        static Rect CenterPanel(float w, float h, float cell)
        {
            float pw = Mathf.Min(w * 0.94f, cell * 34f), ph = Mathf.Min(h * 0.8f, cell * 24f);
            return new Rect(w * 0.5f - pw * 0.5f, h * 0.5f - ph * 0.5f + cell * 0.6f, pw, ph);
        }

        void DrawTitle(float w, float h, float cell, bool blink)
        {
            if (page == Page.Options)
            {
                DrawOptions(CenterPanel(w, h, cell), cell);
                return;
            }

            float panelW = Mathf.Min(w * 0.94f, cell * 34f);
            var panel = new Rect(w * 0.5f - panelW * 0.5f, h * 0.13f, panelW, h * 0.76f);
            hud.Panel(panel, new Color(0f, 0f, 0.03f, 0.5f));

            float y = panel.y + cell;
            hud.Text("PAC-MAN", w * 0.5f, y, HudYellow, Hud.Align.Center, 3f);
            y += cell * 3.4f;
            hud.Text("3D TABLETOP EDITION", w * 0.5f, y, HudCyan, Hud.Align.Center);
            y += cell * 2.2f;

            float colIcon = w * 0.5f - cell * 13f;
            float colText = colIcon + cell * 3f;
            hud.Text("CHARACTER / NICKNAME", colText, y, HudWhite);
            y += cell * 1.8f;
            float icon = Mathf.Round(cell * 1.6f);
            for (int i = 0; i < 4; i++)
            {
                float t0 = 0.8f + i * 1.1f;
                if (modeTime > t0) hud.Image(ghostIcons[i], new Rect(colIcon, y - (icon - cell) * 0.5f, icon, icon), Color.white);
                if (modeTime > t0 + 0.5f) hud.Text(Nicknames[i], colText, y, PixelArt.GhostColors[i]);
                y += cell * 2f;
            }

            if (modeTime > 5.6f)
            {
                y += cell * 0.2f;
                float dot = hud.Px * 2f;
                hud.Panel(new Rect(colText + cell * 2.5f, y + cell * 0.5f - dot * 0.5f, dot, dot), HudPeach);
                hud.Text("10 PTS", colText + cell * 4f, y, HudWhite);
                y += cell * 1.6f;
                hud.Image(energizerIcon, new Rect(colText + cell * 2.5f - hud.Px * 2f, y - hud.Px * 0.5f, cell, cell), Color.white);
                hud.Text("50 PTS", colText + cell * 4f, y, HudWhite);
            }

            DrawItems(TitleItems, w * 0.5f, panel.yMax - cell * 7.7f, cell, cell * 1.8f);
            float small = hud.Px >= 2f ? Mathf.Floor(hud.Px * 0.5f) / hud.Px : 1f;
            hud.Text("ARROWS / WASD / GAMEPAD / MOUSE", w * 0.5f, panel.yMax - cell * 1.6f,
                new Color(0.7f, 0.7f, 0.75f), Hud.Align.Center, small);
        }

        /// <summary>Draws text centred over a maze position, projected through the 3D camera.</summary>
        void WorldText(string s, Vector2 tile, Color color, float scale, float height = 0.9f)
        {
            var sp = cam.WorldToScreenPoint(Board.ToWorld(tile, height));
            if (sp.z <= 0f) return;
            float cell = 8f * hud.Px * scale;
            hud.Text(s, sp.x, hud.Height - sp.y - cell * 0.5f, color, Hud.Align.Center, scale);
        }

        static string ScoreText(int score) => score == 0 ? "00" : score.ToString();
    }
}
