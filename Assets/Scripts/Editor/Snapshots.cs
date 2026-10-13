using System;
using System.IO;
using PacMan.Core;
using PacMan.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PacMan.EditorTools
{
    /// <summary>
    /// Renders a set of PNGs of the running game without entering play mode: the title screen, the intro,
    /// normal play, frightened ghosts, a ghost being eaten, a death and the follow camera.
    /// Batch: Unity -batchmode -projectPath . -executeMethod PacMan.EditorTools.Snapshots.RunBatch -snapshotDir &lt;dir&gt;
    /// </summary>
    public static class Snapshots
    {
        const float Frame = 1f / 60f;
        const int Width = 1600, Height = 900;

        [MenuItem("Pac-Man/Render Snapshots")]
        public static void RenderMenu() => Render(Path.GetFullPath("Snapshots"));

        public static void RunBatch()
        {
            string dir = Path.GetFullPath("Snapshots");
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-snapshotDir") dir = args[i + 1];
            try
            {
                Render(dir);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void Render(string dir)
        {
            Directory.CreateDirectory(dir);
            bool async = EditorSettings.asyncShaderCompilation;
            EditorSettings.asyncShaderCompilation = false;
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/PacMan3D.unity");
                // Start from defaults and leave the editor's saved options and high score alone.
                Settings.Persist = false;
                Settings.FollowCamera = false;
                var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
                game.Initialize();
                var sim = game.Sim;
                // Diagnostics: -noShadows, -noSun and -noDof switch those off to see what causes an artefact.
                var args = Environment.GetCommandLineArgs();
                foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (Array.IndexOf(args, "-noShadows") >= 0) l.shadows = LightShadows.None;
                    if (Array.IndexOf(args, "-noSun") >= 0 && l.type == LightType.Directional) l.enabled = false;
                }
                if (Array.IndexOf(args, "-noDof") >= 0
                    && UnityEngine.Object.FindAnyObjectByType<UnityEngine.Rendering.Volume>().profile.TryGet<UnityEngine.Rendering.Universal.DepthOfField>(out var dof))
                    dof.active = false;

                Run(game, 6f);
                game.Capture(Path.Combine(dir, "01_title.png"), Width, Height);
                game.Capture(Path.Combine(dir, "10_table_a.png"), Width, Height, false);
                game.OpenOptions();
                game.Capture(Path.Combine(dir, "01b_options.png"), Width, Height);
                game.CloseMenu();
                Run(game, 9f);
                game.Capture(Path.Combine(dir, "01c_title_orbit.png"), Width, Height);
                game.Capture(Path.Combine(dir, "10_table_b.png"), Width, Height, false);

                game.StartScripted(true);
                Run(game, 1.2f);
                game.Capture(Path.Combine(dir, "02_intro.png"), Width, Height);
                Run(game, 2.2f);
                game.Capture(Path.Combine(dir, "03_ready.png"), Width, Height);
                Run(game, 7f);
                game.Capture(Path.Combine(dir, "04_play.png"), Width, Height);
                game.PauseGame();
                game.Capture(Path.Combine(dir, "04b_pause.png"), Width, Height);
                game.CloseMenu();

                bool frightShot = false, eatenShot = false, deathShot = false;
                for (float t = 0f; t < 120f && !(frightShot && eatenShot && deathShot); t += Frame)
                {
                    game.Step(Frame);
                    if (!frightShot && sim.Phase == Phase.Playing && sim.IsFrightened && sim.FrightTimer < sim.Spec.FrightTime - 1f)
                    {
                        frightShot = true;
                        game.Capture(Path.Combine(dir, "05_frightened.png"), Width, Height);
                    }
                    if (!eatenShot && sim.Phase == Phase.GhostEaten && sim.PhaseTime > 0.3f)
                    {
                        eatenShot = true;
                        game.Capture(Path.Combine(dir, "06_ghost_eaten.png"), Width, Height);
                    }
                    if (!deathShot && sim.Phase == Phase.Dying && sim.PhaseTime > GameSim.CaughtFreezeTime + 0.7f)
                    {
                        deathShot = true;
                        game.Capture(Path.Combine(dir, "07_death.png"), Width, Height);
                    }
                }

                game.ToggleCamera();
                for (int i = 0; i < 3; i++)
                {
                    Run(game, i == 0 ? 3f : 1.3f);
                    game.Capture(Path.Combine(dir, $"08_follow_cam_{i}.png"), Width, Height);
                }
                game.Capture(Path.Combine(dir, "09_portrait.png"), 900, 1200);
                Debug.Log($"Snapshots written to {dir} (score {sim.Score}, level {sim.Level}, phase {sim.Phase})");
            }
            finally
            {
                Settings.Persist = true;
                Settings.Load();
                EditorSettings.asyncShaderCompilation = async;
            }
        }

        static void Run(GameController game, float seconds)
        {
            for (float t = 0f; t < seconds; t += Frame) game.Step(Frame);
        }
    }
}
