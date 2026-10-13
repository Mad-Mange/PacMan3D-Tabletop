using System;
using System.IO;
using PacMan.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PacMan.Game
{
    /// <summary>
    /// Runs the game: steps the <see cref="GameSim"/> at a fixed rate, mirrors it onto the 3D board,
    /// plays the sounds and draws the HUD. Flow: Title (orbiting board) -> Demo (AI plays) or Playing.
    /// </summary>
    /// <remarks>
    /// Command line: -autoplay lets the AI play a real game; -shots &lt;dir&gt; saves screenshots
    /// during the first minute and then quits; -record &lt;dir&gt; &lt;start&gt; &lt;frames&gt; saves every frame
    /// (30 fps game time) from a point in time; -camera follow starts in the follow view; -mute starts silent.
    /// </remarks>
    public sealed partial class GameController : MonoBehaviour
    {
        enum Mode { Title, Demo, Playing }

        const float SimStep = 1f / 120f;
        const float TitleToDemoTime = 20f;
        const float DemoMaxTime = 75f;
        const string HighScoreKey = "PacMan3D.HighScore";

        [Tooltip("Let the AI play the real game (also: -autoplay on the command line).")]
        public bool autoplay;

        public GameSim Sim { get; private set; }

        Mode mode;
        float modeTime, time, accumulator;
        bool paused;
        Dir steer;
        int demoDeaths;
        bool deathBurstDone;

        Board board;
        PacmanView pac;
        GhostView[] ghosts;
        FruitView fruit;
        Effects fx;
        Sfx sfx;
        Hud hud;
        CameraRig rig;
        Camera cam;
        Light sun;
        Tabletop tabletop;
        DepthOfField dof;
        readonly Autopilot pilot = new Autopilot();

        ShotPlan shots;
        RecordPlan recording;

        void Awake()
        {
            Initialize();
            ParseCommandLine();
        }

        public void Initialize()
        {
            if (Sim != null) return;
            Sim = new GameSim(Environment.TickCount);
            Sim.HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
            Sim.Events += OnGameEvent;
            Settings.Load();

            SetupScene();
            board = new Board(transform);
            pac = new PacmanView(transform);
            ghosts = new GhostView[Sim.Ghosts.Length];
            for (int i = 0; i < ghosts.Length; i++) ghosts[i] = new GhostView(transform, Sim.Ghosts[i].Id);
            fruit = new FruitView(transform);
            fx = new Effects(transform);
            sfx = new Sfx(gameObject);
            hud = new Hud();
            rig = new CameraRig(cam, board.PlayBounds);
            rig.SetFollow(Settings.FollowCamera);
            tabletop = new Tabletop(transform, sun);
            SetupReflections();
            EnterTitle();
        }

        // ------------------------------------------------------------------ scene setup

        void SetupScene()
        {
            cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
                cam.gameObject.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.07f, 0.06f);
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();

            sun = FindAnyObjectByType<Light>();
            if (sun == null || sun.type != LightType.Directional)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
                sun.color = new Color(1f, 0.94f, 0.85f);
                sun.intensity = 1.5f;
                sun.shadows = LightShadows.Soft;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.46f, 0.52f);
            RenderSettings.ambientEquatorColor = new Color(0.36f, 0.33f, 0.29f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.15f, 0.12f);

            var volume = FindAnyObjectByType<Volume>();
            if (volume == null)
            {
                volume = new GameObject("Post Processing").AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = Resources.Load<VolumeProfile>("PacManVolume");
            }
            if (volume.sharedProfile != null)
            {
                // Work on a copy so tweaking focus at runtime never edits the asset.
                volume.profile = Instantiate(volume.sharedProfile);
                volume.profile.TryGet(out dof);
            }
        }

        void SetupReflections()
        {
            // The glitter walls need something to reflect: capture the table and board once.
            var probe = new GameObject("Reflections").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(transform, false);
            probe.transform.position = new Vector3(13.5f, 4f, -15f);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(120f, 40f, 120f);
            probe.resolution = 128;
            probe.RenderProbe();
        }

        // ------------------------------------------------------------------ flow

        void EnterTitle()
        {
            mode = Mode.Title;
            modeTime = 0f;
            idleTime = 0f;
            paused = false;
            page = Page.Title;
            selected = 0;
            Sim.Reset();
            sfx.StopAll();
            sfx.TitleMusic();
        }

        void StartGame(bool demo)
        {
            mode = demo ? Mode.Demo : Mode.Playing;
            modeTime = 0f;
            paused = false;
            demoDeaths = 0;
            accumulator = 0f;
            page = Page.None;
            if (!demo)
            {
                sfx.Coin();
                tabletop.PressButton();
                tabletop.ClearFruits();
            }
            Sim.StartGame(demo);
        }

        void SaveHighScore()
        {
            if (!Settings.Persist || Sim.HighScore <= PlayerPrefs.GetInt(HighScoreKey, 0)) return;
            PlayerPrefs.SetInt(HighScoreKey, Sim.HighScore);
            PlayerPrefs.Save();
        }

        void Update()
        {
            Cursor.visible = page != Page.None;
            HandleInput();
            // A recording advances in fixed steps so every captured frame is evenly spaced.
            float dt = recording != null ? RecordPlan.FrameTime : Time.unscaledDeltaTime;
            Step(dt);
            shots?.Tick(this, dt);
            recording?.Tick(dt);
        }

        void OnApplicationQuit() => SaveHighScore();

        void HandleInput()
        {
            steer = Dir.None;
            if (Controls.MutePressed()) sfx.Muted = !sfx.Muted;
            if (page != Page.None)
            {
                HandleMenu();
                return;
            }
            if (Controls.CameraPressed()) ToggleCamera();

            switch (mode)
            {
                case Mode.Demo:
                    if (Controls.StartPressed()) StartGame(false);
                    else if (Controls.CancelPressed()) EnterTitle();
                    break;
                case Mode.Playing:
                    if (Controls.PausePressed())
                    {
                        PauseGame();
                        return;
                    }
                    steer = Controls.Direction();
                    if (steer != Dir.None && !autoplay) Sim.Pac.Wanted = steer;
                    break;
            }
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Advances everything by one frame. Public so the editor snapshot tool can drive it.</summary>
        public void Step(float dt)
        {
            dt = Mathf.Min(dt, 0.1f);
            time += dt;
            modeTime += dt;
            idleTime += dt;

            switch (mode)
            {
                case Mode.Title:
                    if (page == Page.Title && idleTime > TitleToDemoTime) StartGame(true);
                    break;
                case Mode.Demo:
                    if (Sim.Phase == Phase.Idle || modeTime > DemoMaxTime || (demoDeaths > 0 && Sim.Phase != Phase.Dying)) EnterTitle();
                    break;
                case Mode.Playing:
                    if (Sim.Phase == Phase.Idle)
                    {
                        SaveHighScore();
                        EnterTitle();
                    }
                    break;
            }

            if (!paused && mode != Mode.Title)
            {
                accumulator += dt;
                for (int guard = 0; accumulator >= SimStep && guard < 24; guard++)
                {
                    if ((mode == Mode.Demo || autoplay) && Sim.Phase == Phase.Playing) Sim.Pac.Wanted = pilot.Choose(Sim);
                    Sim.Tick(SimStep);
                    accumulator -= SimStep;
                }
            }

            SyncViews(dt);
            UpdateTable(dt);
            UpdateAudio();
            UpdateCamera(dt, false);
        }

        void UpdateTable(float dt)
        {
            bool live = mode != Mode.Title && Sim.Phase == Phase.Playing && !paused;
            var stick = mode == Mode.Playing && !autoplay ? steer : live ? Sim.Pac.Dir : Dir.None;
            int spares = mode == Mode.Title ? GameSim.StartLives - 1 : Mathf.Max(0, Sim.Lives - 1);
            float progress = 1f - (float)Sim.Maze.PelletsLeft / Sim.Maze.PelletTotal;
            tabletop.Tick(dt, Mood(), progress, spares, pac.Position, stick);
        }

        Tabletop.Mood Mood()
        {
            if (mode == Mode.Title) return Tabletop.Mood.Attract;
            if (paused) return Tabletop.Mood.Paused;
            switch (Sim.Phase)
            {
                case Phase.Ready: return Tabletop.Mood.Ready;
                case Phase.GhostEaten: return Tabletop.Mood.GhostEaten;
                case Phase.Dying: return Tabletop.Mood.Dying;
                case Phase.LevelComplete: return Tabletop.Mood.LevelClear;
                case Phase.Playing:
                    return Sim.FrightFlashing ? Tabletop.Mood.FrightFlash : Sim.IsFrightened ? Tabletop.Mood.Fright : Tabletop.Mood.Play;
                default: return Tabletop.Mood.GameOver;
            }
        }

        void UpdateCamera(float dt, bool snap)
        {
            var view = mode == Mode.Title ? CameraRig.View.Orbit : rig.Preferred;
            rig.Tick(dt, view, pac.Position, snap);
            if (dof != null) dof.focusDistance.value = rig.FocusDistance;
        }

        // ------------------------------------------------------------------ views

        bool FrozenPhase
        {
            get
            {
                var p = Sim.Phase;
                return paused || mode == Mode.Title || p == Phase.Ready || p == Phase.Idle || p == Phase.GameOver
                       || p == Phase.LevelComplete || (p == Phase.Dying && Sim.PhaseTime < GameSim.CaughtFreezeTime);
            }
        }

        /// <summary>At the very start of a game only "PLAYER ONE" and "READY!" show, like the arcade.</summary>
        bool IntroHidesActors => Sim.IsIntro && Sim.PhaseTime < 2.2f;

        bool DeathAnimRunning => Sim.Phase == Phase.Dying && Sim.PhaseTime >= GameSim.CaughtFreezeTime;

        bool LevelFlashing => Sim.Phase == Phase.LevelComplete && Sim.PhaseTime > GameSim.ClearFreezeTime;

        void SyncViews(float dt)
        {
            var phase = Sim.Phase;
            bool frozen = FrozenPhase;

            board.SyncPellets(Sim.Maze);
            board.AnimateEnergizers(time, !frozen || mode == Mode.Title);
            board.SetFlash(LevelFlashing && Mathf.FloorToInt((Sim.PhaseTime - GameSim.ClearFreezeTime) / 0.25f) % 2 == 0);
            board.SetDoorVisible(!LevelFlashing);

            float death = DeathAnimRunning ? Mathf.Clamp01((Sim.PhaseTime - GameSim.CaughtFreezeTime) / GameSim.DeathAnimTime) : -1f;
            bool pacVisible = !IntroHidesActors && phase != Phase.GhostEaten && phase != Phase.GameOver && death < 1f;
            pac.SetVisible(pacVisible);
            pac.Sync(Sim.Pac, dt, frozen, death);
            if (death < 0f) deathBurstDone = false;
            else if (death > 0.85f && !deathBurstDone)
            {
                deathBurstDone = true;
                fx.Burst(pac.Position, PixelArt.PacYellow, 60);
            }

            for (int i = 0; i < ghosts.Length; i++)
            {
                var g = Sim.Ghosts[i];
                bool visible = !IntroHidesActors && !DeathAnimRunning && !LevelFlashing && phase != Phase.GameOver
                               && !(phase == Phase.GhostEaten && g == Sim.LastEatenGhost);
                ghosts[i].Sync(g, Sim, time, dt, frozen || phase == Phase.GhostEaten, visible);
            }

            bool fruitPhase = phase == Phase.Playing || phase == Phase.GhostEaten;
            fruit.Show(Sim.FruitActive && fruitPhase, Sim.Spec.Fruit, time);
        }

        void UpdateAudio()
        {
            if (mode != Mode.Playing || paused || Sim.Phase != Phase.Playing)
            {
                sfx.StopLoop();
                return;
            }
            bool eyes = false;
            foreach (var g in Sim.Ghosts) eyes |= g.IsEyes;
            var loop = eyes ? Sfx.Loop.Eyes : Sim.IsFrightened ? Sfx.Loop.Fright : Sfx.Loop.Siren;
            sfx.SetLoop(loop, 1f - (float)Sim.Maze.PelletsLeft / Sim.Maze.PelletTotal);
        }

        void OnGameEvent(GameEvent e)
        {
            bool audible = mode == Mode.Playing;
            switch (e)
            {
                case GameEvent.GameStarted:
                    if (!Sim.Demo) sfx.Intro();
                    break;
                case GameEvent.DotEaten:
                    if (audible) sfx.Chomp();
                    break;
                case GameEvent.EnergizerEaten:
                    if (audible) sfx.Chomp();
                    fx.Burst(Board.ToWorld(Sim.Pac.Pos, 0.3f), new Color(1f, 0.85f, 0.25f), 30);
                    break;
                case GameEvent.GhostEaten:
                    if (audible) sfx.EatGhost();
                    tabletop.Cheer();
                    if (Sim.LastEatenGhost != null)
                    {
                        var c = PixelArt.GhostColors[(int)Sim.LastEatenGhost.Id];
                        fx.Burst(Board.ToWorld(Sim.LastEatenGhost.Pos, 0.6f), Color.Lerp(c, PixelArt.FrightBlue, 0.5f), 45);
                    }
                    break;
                case GameEvent.FruitEaten:
                    if (audible) sfx.EatFruit();
                    if (mode == Mode.Playing) tabletop.AddFruit(Sim.Spec.Fruit);
                    tabletop.Cheer();
                    fx.Burst(fruit.Position, new Color(1f, 0.3f, 0.35f), 35);
                    break;
                case GameEvent.PacCaught:
                    sfx.StopLoop();
                    if (mode == Mode.Demo) demoDeaths++;
                    break;
                case GameEvent.DeathAnimStarted:
                    if (audible) sfx.Death();
                    break;
                case GameEvent.ExtraLife:
                    if (audible) sfx.ExtraLife();
                    break;
                case GameEvent.LevelCleared:
                    sfx.StopLoop();
                    tabletop.Cheer();
                    fx.Burst(pac.Position, Color.white, 50);
                    break;
                case GameEvent.GameOver:
                    SaveHighScore();
                    break;
            }
        }

        // ------------------------------------------------------------------ capture

        /// <summary>Renders the camera (plus the HUD unless <paramref name="withHud"/> is false) into a PNG, for the editor snapshot tool.</summary>
        public void Capture(string path, int width, int height, bool withHud = true)
        {
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 4);
            var previous = cam.targetTexture;
            cam.targetTexture = rt;
            UpdateCamera(0f, true);
            if (!Application.isPlaying)
            {
                tabletop.SimulateParticles(12f);
                // Outside play mode the first render runs before post-processing has its colour LUT: warm it up.
                cam.Render();
            }
            cam.Render();

            var active = RenderTexture.active;
            RenderTexture.active = rt;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, width, height, 0f);
            if (withHud) DrawHud(width, height);
            GL.PopMatrix();

            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            DestroyImmediate(tex);

            RenderTexture.active = active;
            cam.targetTexture = previous;
            RenderTexture.ReleaseTemporary(rt);
        }

        /// <summary>Switches between the overview and the follow camera (the C key).</summary>
        public void ToggleCamera()
        {
            rig.ToggleFollow();
            Settings.FollowCamera = rig.Preferred == CameraRig.View.Follow;
            Settings.Save();
        }

        /// <summary>Starts a game for scripted runs (snapshots, -autoplay).</summary>
        public void StartScripted(bool aiPlays)
        {
            autoplay = aiPlays;
            // An AI-played game must not overwrite the player's high score or options.
            if (aiPlays) Settings.Persist = false;
            StartGame(false);
        }

        void ParseCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-autoplay") StartScripted(true);
                if (args[i] == "-mute") sfx.Muted = true;
                if (args[i] == "-shots" && i + 1 < args.Length) shots = new ShotPlan(args[i + 1]);
                if (args[i] == "-camera" && i + 1 < args.Length) rig.SetFollow(args[i + 1] == "follow");
                if (args[i] == "-record" && i + 3 < args.Length)
                    recording = new RecordPlan(args[i + 1], float.Parse(args[i + 2], System.Globalization.CultureInfo.InvariantCulture), int.Parse(args[i + 3]));
            }
        }

        /// <summary>Saves numbered frames (for GIFs) from a given game time, then quits.</summary>
        sealed class RecordPlan
        {
            public const float FrameTime = 1f / 30f;
            readonly string dir;
            readonly float start;
            readonly int frames;
            float t;
            int saved;

            public RecordPlan(string dir, float start, int frames)
            {
                this.dir = dir;
                this.start = start;
                this.frames = frames;
                Directory.CreateDirectory(dir);
                // Particles and other engine-timed effects follow the same fixed step.
                Time.captureDeltaTime = FrameTime;
            }

            public void Tick(float dt)
            {
                t += dt;
                if (t < start) return;
                if (saved < frames) ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"frame_{saved++:0000}.png"));
                else Application.Quit();
            }
        }

        /// <summary>Saves a few screenshots over the first minute of play, then quits.</summary>
        sealed class ShotPlan
        {
            static readonly float[] Times = { 2.5f, 6f, 9f, 14f, 22f, 32f, 45f };
            readonly string dir;
            float t;
            int next;

            public ShotPlan(string dir)
            {
                this.dir = dir;
                Directory.CreateDirectory(dir);
            }

            public void Tick(GameController game, float dt)
            {
                t += dt;
                if (next < Times.Length && t >= Times[next])
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"shot_{next:00}.png"));
                    next++;
                }
                if (t > Times[Times.Length - 1] + 1.5f) Application.Quit();
            }
        }
    }
}
