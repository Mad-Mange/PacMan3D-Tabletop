using PacMan.Core;
using UnityEngine;

namespace PacMan.Game
{
    static class ViewMath
    {
        /// <summary>Yaw (degrees about +y) that faces a grid direction. Grid up is world +z.</summary>
        public static float Yaw(Dir d)
        {
            switch (d)
            {
                case Dir.Right: return 90f;
                case Dir.Down: return 180f;
                case Dir.Left: return 270f;
                default: return 0f;
            }
        }

        /// <summary>Characters shrink into the tunnel instead of walking off the board.</summary>
        public static float TunnelScale(float x) => Mathf.Clamp01(Mathf.Min(x + 1.1f, Maze.Width + 0.1f - x) / 0.7f);

        public static float Damp(float current, float target, float rate, float dt) =>
            Mathf.LerpAngle(current, target, 1f - Mathf.Exp(-rate * dt));
    }

    public sealed class PacmanView
    {
        const float Scale = 1.3f;
        const float Radius = 0.5f * Scale;

        readonly Transform root, model, upper, lower;
        readonly Renderer[] renderers;
        float yaw = 270f;

        public Vector3 Position => root.position;

        public PacmanView(Transform parent)
        {
            root = new GameObject("PacMan").transform;
            root.SetParent(parent, false);
            model = Art.Spawn("Pacman", root).transform;
            model.localScale = Vector3.one * Scale;
            upper = model.Find("JawUpper");
            lower = model.Find("JawLower");
            renderers = root.GetComponentsInChildren<Renderer>(true);
        }

        public void SetVisible(bool visible)
        {
            foreach (var r in renderers) r.enabled = visible;
        }

        /// <param name="death">Progress of the death animation in [0, 1], or a negative value when alive.</param>
        public void Sync(PacManActor pac, float dt, bool frozen, float death)
        {
            float tunnel = ViewMath.TunnelScale(pac.Pos.x);
            root.localPosition = Board.ToWorld(pac.Pos, Radius + 0.02f);
            yaw = frozen ? ViewMath.Yaw(pac.Facing) : ViewMath.Damp(yaw, ViewMath.Yaw(pac.Facing), 22f, dt);

            if (death >= 0f)
            {
                // Turn the mouth to the sky, open up until the jaws fold flat, then shrink away.
                float lift = Mathf.SmoothStep(0f, 1f, death * 3f);
                float open = Mathf.Lerp(25f, 178f, Mathf.SmoothStep(0f, 1f, (death - 0.15f) / 0.75f));
                float shrink = 1f - Mathf.SmoothStep(0f, 1f, (death - 0.78f) / 0.2f);
                root.localRotation = Quaternion.Euler(-90f * lift, yaw + death * 200f, 0f);
                SetJaws(open);
                model.localScale = Vector3.one * Scale * shrink;
                return;
            }

            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            model.localScale = Vector3.one * Scale * tunnel;
            float angle = !pac.IsMoving || frozen ? 16f : 3f + 41f * (0.5f - 0.5f * Mathf.Cos(pac.Distance * Mathf.PI * 2f * 1.7f));
            SetJaws(angle);
        }

        /// <summary>Opens the mouth by rotating each jaw about the hinge (local x) by the given angle.</summary>
        public void SetJaws(float angle)
        {
            upper.localRotation = Quaternion.Euler(-angle, 0f, 0f);
            lower.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }

    public sealed class GhostView
    {
        const float Scale = 1.3f;

        readonly GhostId id;
        readonly Transform root, model;
        readonly Renderer bodyA, bodyB, eyes, pupils, face;
        readonly Material normal, frightened, flash, faceNormal, faceFlash;
        readonly Light glow;
        readonly Color glowColor;
        float yaw = 180f;

        public Vector3 Position => root.position;

        public GhostView(Transform parent, GhostId id)
        {
            this.id = id;
            root = new GameObject(id.ToString()).transform;
            root.SetParent(parent, false);
            model = Art.Spawn("Ghost", root).transform;
            model.localScale = Vector3.one * Scale;
            bodyA = model.Find("BodyA").GetComponent<Renderer>();
            bodyB = model.Find("BodyB").GetComponent<Renderer>();
            eyes = model.Find("Eyes").GetComponent<Renderer>();
            pupils = model.Find("Pupils").GetComponent<Renderer>();
            face = model.Find("FrightFace").GetComponent<Renderer>();

            normal = Art.Mat("Ghost" + id);
            frightened = Art.Mat("GhostFrightened");
            flash = Art.Mat("GhostFlash");
            faceNormal = Art.Mat("FrightFace");
            faceFlash = Art.Mat("FrightFaceFlash");

            glowColor = PixelArt.GhostColors[(int)id];
            glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(root, false);
            glow.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            glow.type = LightType.Point;
            glow.range = 2.6f;
            glow.intensity = 1.4f;
            glow.shadows = LightShadows.None;
        }

        public void Sync(Ghost g, GameSim sim, float time, float dt, bool frozen, bool visible)
        {
            if (!visible)
            {
                bodyA.enabled = bodyB.enabled = eyes.enabled = pupils.enabled = face.enabled = false;
                glow.enabled = false;
                return;
            }

            float tunnel = ViewMath.TunnelScale(g.Pos.x);
            float hover = 0.06f + 0.05f * Mathf.Sin(time * 4.5f + (int)id * 1.7f);
            root.localPosition = Board.ToWorld(g.Pos, frozen ? 0.06f : hover);

            // Inside the house they look out at the player instead of spinning with every bob.
            float targetYaw = g.State == GhostState.InHouse ? 180f : ViewMath.Yaw(g.Dir == Dir.None ? Dir.Down : g.Dir);
            yaw = frozen ? targetYaw : ViewMath.Damp(yaw, targetYaw, 16f, dt);
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            model.localScale = Vector3.one * Scale * tunnel;

            bool frameB = !frozen && Mathf.FloorToInt(time / 0.13f) % 2 == 1;
            bool eyesOnly = g.IsEyes;
            bool scared = g.Frightened && !eyesOnly;
            bool white = scared && sim.FrightFlashing && Mathf.FloorToInt(sim.FrightTimer / (GameSim.FlashPeriod * 0.5f)) % 2 == 0;

            bodyA.enabled = !eyesOnly && !frameB;
            bodyB.enabled = !eyesOnly && frameB;
            var body = scared ? (white ? flash : frightened) : normal;
            bodyA.sharedMaterial = body;
            bodyB.sharedMaterial = body;
            face.enabled = scared;
            face.sharedMaterial = white ? faceFlash : faceNormal;
            eyes.enabled = pupils.enabled = !scared;

            glow.enabled = !eyesOnly;
            glow.color = scared ? (white ? Color.white : PixelArt.FrightBlue) : glowColor;
        }
    }

    public sealed class FruitView
    {
        readonly Transform root;
        readonly Transform[] fruits;
        readonly Light glow;

        public Vector3 Position => root.position;

        public FruitView(Transform parent)
        {
            root = new GameObject("Fruit").transform;
            root.SetParent(parent, false);
            root.localPosition = Board.ToWorld(GameSim.FruitPos, 0.75f);
            var model = Art.Spawn("Fruits", root).transform;
            model.localScale = Vector3.one * 1.1f;
            fruits = new Transform[System.Enum.GetValues(typeof(Fruit)).Length];
            foreach (Transform child in model)
            {
                if (child.name.StartsWith("Fruit_") && System.Enum.TryParse(child.name.Substring(6), out Fruit f))
                    fruits[(int)f] = child;
            }
            glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(root, false);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.6f, 0.5f);
            glow.range = 2.5f;
            glow.intensity = 1.2f;
            glow.shadows = LightShadows.None;
            Show(false, Fruit.Cherry, 0f);
        }

        public void Show(bool visible, Fruit fruit, float time)
        {
            for (int i = 0; i < fruits.Length; i++)
                if (fruits[i] != null) fruits[i].gameObject.SetActive(visible && i == (int)fruit);
            glow.enabled = visible;
            // The fruit is flat pixel art, so it sways toward the camera instead of spinning edge-on.
            root.localRotation = Quaternion.Euler(-20f, 180f + 35f * Mathf.Sin(time * 2.2f), 0f);
            root.localPosition = Board.ToWorld(GameSim.FruitPos, 0.75f + 0.08f * Mathf.Sin(time * 3f));
        }
    }
}
