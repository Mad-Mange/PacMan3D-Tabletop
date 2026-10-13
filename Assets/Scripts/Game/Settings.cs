using UnityEngine;

namespace PacMan.Game
{
    /// <summary>Player options from the OPTIONS menu, kept in PlayerPrefs between sessions.</summary>
    public static class Settings
    {
        const string Prefix = "PacMan3D.";
        public const int VolumeSteps = 10;

        /// <summary>Volumes in 0..1, in steps of 1/<see cref="VolumeSteps"/>.</summary>
        public static float Master = 1f, Effects = 0.8f, Music = 0.6f, Siren = 0.6f;
        public static bool FollowCamera;

        /// <summary>False for scripted runs (snapshots, -autoplay): nothing is written to PlayerPrefs.</summary>
        public static bool Persist = true;

        public static void Load()
        {
            if (!Persist) return;
            Master = PlayerPrefs.GetFloat(Prefix + "Master", Master);
            Effects = PlayerPrefs.GetFloat(Prefix + "Effects", Effects);
            Music = PlayerPrefs.GetFloat(Prefix + "Music", Music);
            Siren = PlayerPrefs.GetFloat(Prefix + "Siren", Siren);
            FollowCamera = PlayerPrefs.GetInt(Prefix + "FollowCamera", 0) == 1;
        }

        public static void Save()
        {
            if (!Persist) return;
            PlayerPrefs.SetFloat(Prefix + "Master", Master);
            PlayerPrefs.SetFloat(Prefix + "Effects", Effects);
            PlayerPrefs.SetFloat(Prefix + "Music", Music);
            PlayerPrefs.SetFloat(Prefix + "Siren", Siren);
            PlayerPrefs.SetInt(Prefix + "FollowCamera", FollowCamera ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Snaps a volume to the menu's steps and keeps it in range.</summary>
        public static float Step(float volume, int delta) =>
            Mathf.Clamp(Mathf.Round(volume * VolumeSteps) + delta, 0, VolumeSteps) / VolumeSteps;
    }
}
