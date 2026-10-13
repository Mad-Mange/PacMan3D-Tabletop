using UnityEngine;

namespace PacMan.Game
{
    /// <summary>
    /// The tabletop camera. Overview frames the whole board from the south like the reference photo,
    /// Follow tracks Pac-Man up close, and Orbit slowly circles the board behind the title screen.
    /// The overview distance is refitted whenever the aspect ratio changes.
    /// </summary>
    public sealed class CameraRig
    {
        public enum View { Overview, Follow, Orbit }

        const float Fov = 30f;
        const float OverviewPitch = 54f;
        static readonly Vector3 BoardCenter = new Vector3(13.5f, 0f, -15.2f);

        readonly Camera cam;
        readonly Bounds bounds;
        Vector3 pos, focus;
        Quaternion rot;
        bool placed;
        float orbitYaw = -25f;
        float fittedAspect = -1f, fittedDistance;

        public View Preferred { get; private set; } = View.Overview;
        public float FocusDistance => Vector3.Distance(pos, focus);

        public CameraRig(Camera cam, Bounds bounds)
        {
            this.cam = cam;
            this.bounds = bounds;
            cam.fieldOfView = Fov;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 400f;
        }

        public void ToggleFollow() => Preferred = Preferred == View.Follow ? View.Overview : View.Follow;

        public void SetFollow(bool follow) => Preferred = follow ? View.Follow : View.Overview;

        public void Tick(float dt, View view, Vector3 pacPosition, bool snap = false)
        {
            float yaw, pitch, distance;
            Vector3 center;
            switch (view)
            {
                case View.Follow:
                    yaw = 0f;
                    pitch = 55f;
                    distance = 22f;
                    center = new Vector3(Mathf.Clamp(pacPosition.x, 6f, 21f), 0f, Mathf.Clamp(pacPosition.z - 0.5f, -25f, -5f));
                    break;
                case View.Orbit:
                    orbitYaw += dt * 7f;
                    yaw = orbitYaw;
                    pitch = 44f;
                    distance = OverviewDistance() * 0.98f;
                    center = BoardCenter;
                    break;
                default:
                    yaw = 0f;
                    pitch = OverviewPitch;
                    distance = OverviewDistance();
                    center = BoardCenter;
                    break;
            }

            var targetRot = Quaternion.Euler(pitch, yaw, 0f);
            var targetPos = center - targetRot * Vector3.forward * distance;
            if (snap || !placed)
            {
                pos = targetPos;
                rot = targetRot;
                focus = center;
                placed = true;
            }
            else
            {
                float k = 1f - Mathf.Exp(-(view == View.Follow ? 5f : 2.5f) * dt);
                pos = Vector3.Lerp(pos, targetPos, k);
                rot = Quaternion.Slerp(rot, targetRot, k);
                focus = Vector3.Lerp(focus, center, k);
            }
            cam.transform.SetPositionAndRotation(pos, rot);
        }

        /// <summary>Closest distance at which the board fits, leaving room for the HUD above and below.</summary>
        float OverviewDistance()
        {
            float aspect = cam.aspect;
            if (Mathf.Abs(aspect - fittedAspect) < 1e-3f) return fittedDistance;

            var r = Quaternion.Euler(OverviewPitch, 0f, 0f);
            float lo = 5f, hi = 300f;
            var savedPos = cam.transform.position;
            var savedRot = cam.transform.rotation;
            for (int i = 0; i < 30; i++)
            {
                float mid = (lo + hi) * 0.5f;
                cam.transform.SetPositionAndRotation(BoardCenter - r * Vector3.forward * mid, r);
                if (Fits()) hi = mid;
                else lo = mid;
            }
            cam.transform.SetPositionAndRotation(savedPos, savedRot);
            fittedAspect = aspect;
            fittedDistance = hi;
            return hi;
        }

        bool Fits()
        {
            var min = bounds.min;
            var max = bounds.max;
            for (int i = 0; i < 8; i++)
            {
                var p = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                var v = cam.WorldToViewportPoint(p);
                if (v.z <= 0f || v.x < 0.02f || v.x > 0.98f || v.y < 0.075f || v.y > 0.885f) return false;
            }
            return true;
        }
    }
}
