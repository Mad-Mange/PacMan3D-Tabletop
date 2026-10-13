using System.Collections.Generic;
using PacMan.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PacMan.Game
{
    /// <summary>
    /// Everything around the board that makes the table feel lived in:
    /// - props from Blender/build_props.py (mug, filament spools, arcade stick, game box, notebook, dice, tokens, plant)
    /// - sunlight through a window (a light cookie) with clouds drifting past, dust in the light, steam off the coffee
    /// - an LED underglow below the board that follows the game's mood
    /// - the spare lives as Pac-Man figures that watch the game, cheer and hop onto the board,
    ///   the fruit you have won lined up beside the board, and an arcade stick that mirrors the steering.
    /// </summary>
    public sealed class Tabletop
    {
        public const float TableY = -0.4f;
        const float CookieSize = 74f;
        static readonly Vector3 WindowCenter = new Vector3(14f, 0f, -13f);

        public enum Mood { Attract, Ready, Play, Fright, FrightFlash, GhostEaten, Dying, LevelClear, GameOver, Paused }

        sealed class Figure
        {
            public Transform Root, Model, Upper, Lower;
            public Vector3 Home;
            public float Born, Leaving = -1f, Seed;
        }

        readonly Transform root;
        readonly Light sun;
        readonly float sunIntensity;
        readonly Transform stick, button, leaves;
        readonly Quaternion stickRest, leavesRest;
        readonly Vector3 buttonRest;
        readonly ParticleSystem steam, dust;
        readonly Material glowMaterial;
        readonly List<Light> glowLights = new List<Light>();
        readonly List<Figure> lives = new List<Figure>();
        readonly List<Figure> fruits = new List<Figure>();

        float time;
        float cheer = -10f, pressed = -10f;
        Color glow = Color.black;
        Vector3 stickTilt;

        public Tabletop(Transform parent, Light sun)
        {
            root = new GameObject("Tabletop").transform;
            root.SetParent(parent, false);
            this.sun = sun;
            sunIntensity = sun != null ? sun.intensity : 1f;

            var props = Art.Spawn("Props", root).transform;
            Transform Take(string name) => props.Find(name);
            Transform Clone(Transform t) => Object.Instantiate(t.gameObject, root).transform;

            // Left: the arcade stick and the notebook with the maze sketch, a plant in the far corner.
            var joystick = Group("ArcadeStick", new Vector3(-9.6f, TableY, -15.5f), -12f);
            Take("Joystick_Base").SetParent(joystick, false);
            stick = Take("Joystick_Stick");
            stick.SetParent(joystick, false);
            stickRest = stick.localRotation;
            button = Take("Joystick_Button");
            button.SetParent(joystick, false);
            buttonRest = button.localPosition;

            var notebook = Place(Take("Notebook"), new Vector3(-11f, TableY, -2.5f), 12f);
            var pencil = Take("Pencil");
            pencil.SetParent(notebook, false);
            pencil.localPosition = new Vector3(1.6f, 0.42f, 1.2f);
            pencil.localRotation = Quaternion.Euler(0f, 28f, 0f);

            var plant = Group("Plant", new Vector3(-7.5f, TableY, 8f), 20f);
            Take("Plant_Pot").SetParent(plant, false);
            leaves = Take("Plant_Leaves");
            leaves.SetParent(plant, false);
            leavesRest = leaves.localRotation;

            // Far side: the filament the maze was printed with.
            var spool = Place(Take("Spool"), new Vector3(4.5f, TableY, 13f), 70f);
            var blueSpool = Place(Clone(spool), new Vector3(16f, TableY, 13.8f), 105f);
            Swap(blueSpool, "Filament", Art.Mat("FilamentBlue"));

            // Right: coffee, the game box, dice and a few arcade tokens.
            var mug = Place(Take("Mug"), new Vector3(37f, TableY, -11f), 200f);
            Place(Take("GameBox"), new Vector3(39.5f, TableY, 3.5f), -16f);
            var die = Take("Dice");
            var token = Take("Token");
            var die2 = Clone(die);
            var token2 = Clone(token);
            var looseToken = Clone(token);
            Die(die, new Vector3(34.8f, TableY, -22f), 23f, Vector3.zero);
            Die(die2, new Vector3(37f, TableY, -23.4f), 64f, new Vector3(90f, 0f, 0f));
            Tokens(token, new Vector3(33.2f, TableY, -16.6f), 6);
            Tokens(token2, new Vector3(34.9f, TableY, -15.7f), 3);
            Place(looseToken, new Vector3(36.3f, TableY, -17.2f), 40f);
            Stain(new Vector3(33.4f, TableY + 0.004f, -5.6f), 5.2f, 20f);
            Stain(new Vector3(35.4f, TableY + 0.005f, -3.6f), 4.3f, 150f);
            Art.Kill(props.gameObject);

            steam = Steam(mug.position + new Vector3(0f, 4.3f, 0f));
            dust = Dust();
            glowMaterial = new Material(Art.Mat("Underglow")) { name = "Underglow (live)" };
            BuildUnderglow();
            SetupWindowLight();
        }

        // ------------------------------------------------------------------ per frame

        public void Tick(float dt, Mood mood, float progress, int spareLives, Vector3 pac, Dir steer)
        {
            time += dt;
            UpdateLight();
            UpdateGlow(dt, mood, progress);
            UpdateStick(dt, steer);
            UpdateFigures(spareLives, pac);

            leaves.localRotation = leavesRest * Quaternion.Euler(
                1.6f * Mathf.Sin(time * 0.8f) + 0.6f * Mathf.Sin(time * 2.1f), 0f, 1.2f * Mathf.Cos(time * 0.6f));
        }

        /// <summary>The figures on the table hop for joy (ghost eaten, fruit, level cleared).</summary>
        public void Cheer() => cheer = time;

        /// <summary>The big red button goes down when a game starts.</summary>
        public void PressButton() => pressed = time;

        public void AddFruit(Fruit fruit)
        {
            var go = Art.Spawn("Fruits", root);
            var others = new List<GameObject>();
            foreach (Transform child in go.transform)
                if (child.name != "Fruit_" + fruit) others.Add(child.gameObject);
            foreach (var o in others) Art.Kill(o);
            go.name = "WonFruit";
            int i = fruits.Count;
            var f = new Figure
            {
                Root = go.transform,
                Model = go.transform,
                Home = new Vector3(31.8f + (i % 5) * 2.1f, TableY + 0.8f, -29.6f + (i / 5) * 2.3f),
                Born = time,
                Seed = Random.value * 10f,
            };
            fruits.Add(f);
            if (fruits.Count > 15)
            {
                Art.Kill(fruits[0].Root.gameObject);
                fruits.RemoveAt(0);
                for (int k = 0; k < fruits.Count; k++)
                    fruits[k].Home = new Vector3(31.8f + (k % 5) * 2.1f, TableY + 0.8f, -29.6f + (k / 5) * 2.3f);
            }
        }

        public void ClearFruits()
        {
            foreach (var f in fruits) Art.Kill(f.Root.gameObject);
            fruits.Clear();
        }

        /// <summary>Runs the particle systems forward, for snapshots taken outside play mode.</summary>
        public void SimulateParticles(float seconds)
        {
            dust.Simulate(seconds, true, true);
            steam.Simulate(seconds, true, true);
        }

        // ------------------------------------------------------------------ light

        void SetupWindowLight()
        {
            if (sun == null) return;
            var cookie = Resources.Load<Texture2D>("Textures/WindowCookie");
            if (cookie == null) return;
            sun.cookie = cookie;
            var data = sun.GetUniversalAdditionalLightData();
            data.lightCookieSize = new Vector2(CookieSize, CookieSize);
            // Centre the sunlit window on the board.
            var local = sun.transform.InverseTransformPoint(WindowCenter);
            data.lightCookieOffset = new Vector2(CookieSize * 0.5f - local.x, CookieSize * 0.5f - local.y);
        }

        /// <summary>Clouds drifting past the window: the sunlight slowly dims and brightens.</summary>
        void UpdateLight()
        {
            if (sun == null) return;
            float c = Mathf.PerlinNoise(time * 0.045f, 0.37f);
            sun.intensity = sunIntensity * Mathf.Lerp(0.68f, 1.06f, Mathf.SmoothStep(0.2f, 0.75f, c));
        }

        // ------------------------------------------------------------------ underglow

        void BuildUnderglow()
        {
            float x0 = -0.5f - Board.BoardMargin, x1 = Maze.Width - 0.5f + Board.BoardMargin;
            float z0 = -(Maze.Height - 0.5f) - Board.BoardMargin, z1 = 0.5f + Board.BoardMargin;
            const float o = 0.08f, h = 0.07f;
            var mb = new MeshBuilder(1);
            mb.Box(new Vector3(x0 - o, TableY, z0 - o), new Vector3(x1 + o, TableY + h, z0), 0, 0);
            mb.Box(new Vector3(x0 - o, TableY, z1), new Vector3(x1 + o, TableY + h, z1 + o), 0, 0);
            mb.Box(new Vector3(x0 - o, TableY, z0), new Vector3(x0, TableY + h, z1), 0, 0);
            mb.Box(new Vector3(x1, TableY, z0), new Vector3(x1 + o, TableY + h, z1), 0, 0);
            var go = new GameObject("Underglow");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Underglow");
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = glowMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            float mx = (x0 + x1) * 0.5f, mz = (z0 + z1) * 0.5f;
            var spots = new[]
            {
                new Vector3(x0, 0f, z0), new Vector3(mx, 0f, z0), new Vector3(x1, 0f, z0),
                new Vector3(x1, 0f, mz), new Vector3(x1, 0f, z1), new Vector3(mx, 0f, z1),
                new Vector3(x0, 0f, z1), new Vector3(x0, 0f, mz),
            };
            var center = new Vector3(mx, 0f, mz);
            foreach (var p in spots)
            {
                var light = new GameObject("GlowLight").AddComponent<Light>();
                light.transform.SetParent(root, false);
                light.transform.localPosition = p + (p - center).normalized * 1.1f + Vector3.up * (TableY + 0.35f);
                light.type = LightType.Point;
                light.range = 5.5f;
                light.shadows = LightShadows.None;
                glowLights.Add(light);
            }
        }

        void UpdateGlow(float dt, Mood mood, float progress)
        {
            Color target;
            bool snap = false;
            switch (mood)
            {
                case Mood.Attract:
                    target = Color.HSVToRGB(Mathf.Repeat(time * 0.035f, 1f), 0.55f, 0.75f);
                    break;
                case Mood.Ready:
                    target = new Color(1f, 0.72f, 0.18f);
                    break;
                case Mood.Fright:
                    target = new Color(0.12f, 0.2f, 1f) * 1.2f;
                    break;
                case Mood.FrightFlash:
                    target = Mathf.Repeat(time, 0.4f) < 0.2f ? Color.white : new Color(0.12f, 0.2f, 1f);
                    snap = true;
                    break;
                case Mood.GhostEaten:
                    target = new Color(0.3f, 1f, 1f) * 1.3f;
                    snap = true;
                    break;
                case Mood.Dying:
                    target = new Color(1f, 0.08f, 0.06f);
                    break;
                case Mood.LevelClear:
                    target = Mathf.Repeat(time, 0.25f) < 0.125f ? Color.white : Color.HSVToRGB(Mathf.Repeat(time * 2f, 1f), 0.6f, 1f);
                    snap = true;
                    break;
                case Mood.GameOver:
                    target = new Color(0.5f, 0.04f, 0.04f);
                    break;
                case Mood.Paused:
                    target = new Color(0.1f, 0.12f, 0.3f);
                    break;
                default:
                    // Breathes with the siren, faster as the maze empties.
                    float rate = Mathf.Lerp(4f, 9f, progress);
                    target = new Color(0.12f, 0.32f, 1f) * (0.7f + 0.3f * Mathf.Sin(time * rate));
                    break;
            }
            glow = snap ? target : Color.Lerp(glow, target, 1f - Mathf.Exp(-6f * dt));
            glowMaterial.SetColor("_EmissionColor", glow * 2.4f);
            float strength = Mathf.Max(glow.r, Mathf.Max(glow.g, glow.b));
            foreach (var l in glowLights)
            {
                l.color = strength > 0.001f ? glow / strength : Color.black;
                l.intensity = 1.5f * strength;
            }
        }

        // ------------------------------------------------------------------ arcade stick

        void UpdateStick(float dt, Dir steer)
        {
            var v = steer.Vec();
            // Grid up is world +z (north).
            var target = new Vector3(v.x, 0f, -v.y);
            stickTilt = Vector3.Lerp(stickTilt, target, 1f - Mathf.Exp(-18f * dt));
            var tilt = Quaternion.FromToRotation(Vector3.up, (Vector3.up + stickTilt * 0.5f).normalized);
            stick.rotation = tilt * stick.parent.rotation * stickRest;

            float t = time - pressed;
            float down = t < 0.35f ? Mathf.Sin(Mathf.Clamp01(t / 0.35f) * Mathf.PI) : 0f;
            button.localPosition = buttonRest + Vector3.down * 0.18f * down;
        }

        // ------------------------------------------------------------------ figures

        static Vector3 LifeSlot(int i) => new Vector3(-4.6f - i * 2.4f, TableY + 0.66f, -29.6f);

        void UpdateFigures(int spareLives, Vector3 pac)
        {
            // Match the number of standing figures to the spare lives: new ones pop in, a lost one hops onto the board.
            int standing = 0;
            foreach (var f in lives) if (f.Leaving < 0f) standing++;
            if (standing < spareLives) lives.Add(NewLife(standing));
            else if (standing > spareLives)
                for (int i = lives.Count - 1; i >= 0; i--)
                    if (lives[i].Leaving < 0f)
                    {
                        lives[i].Leaving = time;
                        break;
                    }

            for (int i = lives.Count - 1; i >= 0; i--)
            {
                var f = lives[i];
                if (f.Leaving >= 0f && time - f.Leaving > 0.9f)
                {
                    Art.Kill(f.Root.gameObject);
                    lives.RemoveAt(i);
                    continue;
                }
                AnimateLife(f, i, pac);
            }

            for (int i = 0; i < fruits.Count; i++)
            {
                var f = fruits[i];
                float pop = PopScale(time - f.Born);
                float hop = Hop(i);
                f.Root.localPosition = f.Home + Vector3.up * (hop + 0.05f * Mathf.Sin(time * 2f + f.Seed));
                f.Root.localRotation = Quaternion.Euler(-15f, 180f + 18f * Mathf.Sin(time * 0.9f + f.Seed), 0f);
                f.Root.localScale = Vector3.one * 1.5f * pop;
            }
        }

        Figure NewLife(int slot)
        {
            var figure = new GameObject("SpareLife").transform;
            figure.SetParent(root, false);
            var model = Art.Spawn("Pacman", figure).transform;
            model.localScale = Vector3.one * 1.3f;
            var f = new Figure
            {
                Root = figure,
                Model = model,
                Upper = model.Find("JawUpper"),
                Lower = model.Find("JawLower"),
                Home = LifeSlot(slot),
                Born = time,
                Seed = Random.value * 10f,
            };
            figure.localPosition = f.Home;
            return f;
        }

        void AnimateLife(Figure f, int index, Vector3 pac)
        {
            // Watch the live Pac-Man.
            var look = pac - f.Root.position;
            float yaw = look.sqrMagnitude > 0.01f ? Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg : 180f;
            float chomp = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * 0.9f + f.Seed)), 12f) * (0.5f + 0.5f * Mathf.Sin(time * 16f));
            float jaw = 8f + 30f * chomp;

            if (f.Leaving >= 0f)
            {
                // Hop onto the board, toward Pac-Man's start, and vanish into it.
                float t = Mathf.Clamp01((time - f.Leaving) / 0.9f);
                var target = Board.ToWorld(GameSim.PacStart, 0.66f);
                f.Root.localPosition = Vector3.Lerp(f.Home, target, Mathf.SmoothStep(0f, 1f, t)) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 3f;
                f.Model.localScale = Vector3.one * 1.3f * (1f - Mathf.SmoothStep(0.65f, 1f, t));
                jaw = 40f;
            }
            else
            {
                f.Root.localPosition = f.Home + Vector3.up * (Hop(index) + 0.04f * Mathf.Sin(time * 2.2f + f.Seed));
                f.Model.localScale = Vector3.one * 1.3f * PopScale(time - f.Born);
            }
            f.Root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            f.Upper.localRotation = Quaternion.Euler(-jaw, 0f, 0f);
            f.Lower.localRotation = Quaternion.Euler(jaw, 0f, 0f);
        }

        float Hop(int index)
        {
            float t = time - cheer - index * 0.08f;
            return t > 0f && t < 0.55f ? Mathf.Sin(t / 0.55f * Mathf.PI) * 1.1f : 0f;
        }

        /// <summary>0 to 1 with a little overshoot over the first 0.45 s.</summary>
        static float PopScale(float age)
        {
            float t = Mathf.Clamp01(age / 0.45f);
            return t >= 1f ? 1f : Mathf.Sin(t * Mathf.PI * 0.5f) * (1f + 0.25f * Mathf.Sin(t * Mathf.PI));
        }

        // ------------------------------------------------------------------ building

        Transform Group(string name, Vector3 position, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.localPosition = position;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        Transform Place(Transform t, Vector3 position, float yaw)
        {
            t.SetParent(root, false);
            t.localPosition = position;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        /// <summary>A die rolled onto some face: it pivots about its centre, which sits half its size up.</summary>
        void Die(Transform die, Vector3 position, float yaw, Vector3 tumble)
        {
            var holder = Group("Die", position + Vector3.up * 0.9f, 0f);
            holder.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(tumble);
            die.SetParent(holder, false);
            die.localPosition = Vector3.down * 0.9f;
            die.localRotation = Quaternion.identity;
        }

        void Tokens(Transform first, Vector3 position, int count)
        {
            var rng = new System.Random(count * 7);
            for (int i = 0; i < count; i++)
            {
                var t = i == 0 ? first : Object.Instantiate(first.gameObject, root).transform;
                var jitter = new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 0.08f;
                Place(t, position + jitter + Vector3.up * (i * 0.125f), (float)rng.NextDouble() * 360f);
            }
        }

        void Stain(Vector3 position, float size, float yaw)
        {
            var quad = Art.Primitive(PrimitiveType.Quad, "CoffeeStain", root, Art.Mat("CoffeeStain"), false);
            quad.transform.localPosition = position;
            quad.transform.localRotation = Quaternion.Euler(90f, yaw, 0f);
            quad.transform.localScale = Vector3.one * size;
        }

        static void Swap(Transform t, string from, Material to)
        {
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && mats[i].name == from) mats[i] = to;
                r.sharedMaterials = mats;
            }
        }

        ParticleSystem Particles(string name, Material material, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        ParticleSystem Steam(Vector3 position)
        {
            var ps = Particles("Steam", Art.Mat("Steam"), position);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.6f, 3.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(1f, 1f, 1f, 0.17f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            var emission = ps.emission;
            emission.rateOverTime = 7f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 6f;
            shape.radius = 1f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.9f));
            Fade(ps, 0.15f, 0.55f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.4f;
            noise.scrollSpeed = 0.2f;
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            ps.Play();
            return ps;
        }

        ParticleSystem Dust()
        {
            var ps = Particles("Dust", Art.Mat("Dust"), new Vector3(14f, 6f, -13f));
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 16f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.17f);
            main.startColor = new Color(1f, 0.93f, 0.78f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;
            var emission = ps.emission;
            emission.rateOverTime = 34f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(62f, 13f, 56f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.22f;
            noise.frequency = 0.12f;
            noise.scrollSpeed = 0.04f;
            Fade(ps, 0.2f, 0.8f);
            ps.Play();
            return ps;
        }

        static void Fade(ParticleSystem ps, float inEnd, float outStart)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inEnd), new GradientAlphaKey(1f, outStart), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }
    }
}
