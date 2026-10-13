using System.IO;
using System.Linq;
using PacMan.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PacMan.EditorTools
{
    /// <summary>
    /// Generates everything the game loads from Assets/Resources (textures, materials, post-processing)
    /// and the main scene. Safe to re-run: assets are overwritten in place, so GUIDs stay stable.
    /// Batch: Unity -batchmode -projectPath . -executeMethod PacMan.EditorTools.ProjectSetup.RunBatch -quit
    /// </summary>
    public static class ProjectSetup
    {
        const string TexDir = "Assets/Art/Textures";
        const string RuntimeTexDir = "Assets/Resources/Textures";
        const string MatDir = "Assets/Resources/Materials";
        const string ScenePath = "Assets/Scenes/PacMan3D.unity";
        const string VolumePath = "Assets/Resources/PacManVolume.asset";

        [MenuItem("Pac-Man/Setup Project (textures, materials, scene)")]
        public static void Run()
        {
            Directory.CreateDirectory(TexDir);
            Directory.CreateDirectory(RuntimeTexDir);
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory("Assets/Scenes");

            BuildMaterials();
            var profile = BuildVolumeProfile();
            AssetDatabase.SaveAssets();
            BuildScene(profile);
            Debug.Log("Pac-Man 3D setup complete.");
        }

        public static void RunBatch() => Run();

        // ------------------------------------------------------------------ textures

        static Texture2D SaveTexture(string name, int w, int h, Color[] px, bool linear, bool normalMap = false,
            FilterMode filter = FilterMode.Trilinear, TextureWrapMode wrap = TextureWrapMode.Repeat, string dir = TexDir)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, linear);
            tex.SetPixels(px);
            tex.Apply();
            string path = $"{dir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !linear;
            importer.mipmapEnabled = filter != FilterMode.Point;
            importer.filterMode = filter;
            importer.wrapMode = wrap;
            importer.anisoLevel = filter == FilterMode.Point ? 0 : 8;
            importer.alphaIsTransparency = !linear && !normalMap;
            importer.textureCompression = filter == FilterMode.Point ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D SaveTexture(string name, int w, int h, Color32[] px, FilterMode filter)
        {
            var colors = new Color[px.Length];
            for (int i = 0; i < px.Length; i++) colors[i] = px[i];
            return SaveTexture(name, w, h, colors, false, false, filter, TextureWrapMode.Clamp);
        }

        static void DeleteTexture(string name)
        {
            string path = $"{TexDir}/{name}.png";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
        }

        // ------------------------------------------------------------------ materials

        static void BuildMaterials()
        {
            const int wallSize = 512;
            var side = TextureGen.WallSide(wallSize);
            var sideAlbedo = SaveTexture("WallSide_Albedo", wallSize, wallSize, side.Albedo, false);
            var sideMask = SaveTexture("WallSide_Mask", wallSize, wallSize, side.Mask, true);
            var sideNormal = SaveTexture("WallSide_Normal", wallSize, wallSize, side.Normal, true, true);
            var sideEmission = SaveTexture("WallSide_Emission", wallSize, wallSize, side.Emission, false);

            var top = TextureGen.WallTop(wallSize);
            var topAlbedo = SaveTexture("WallTop_Albedo", wallSize, wallSize, top.Albedo, false);
            var topMask = SaveTexture("WallTop_Mask", wallSize, wallSize, top.Mask, true);
            var topNormal = SaveTexture("WallTop_Normal", wallSize, wallSize, top.Normal, true, true);

            const int floorSize = 256;
            var floor = TextureGen.Floor(floorSize);
            var floorAlbedo = SaveTexture("Floor_Albedo", floorSize, floorSize, floor.Albedo, false);
            var floorMask = SaveTexture("Floor_Mask", floorSize, floorSize, floor.Mask, true);
            var floorNormal = SaveTexture("Floor_Normal", floorSize, floorSize, floor.Normal, true, true);

            DeleteTexture("Wood_Albedo");
            const int woodSize = 2048;
            var wood = TextureGen.TableWood(woodSize);
            var woodAlbedo = SaveTexture("Table_Albedo", woodSize, woodSize, wood.Albedo, false);
            var woodMask = SaveTexture("Table_Mask", woodSize, woodSize, wood.Mask, true);
            var woodNormal = SaveTexture("Table_Normal", woodSize, woodSize, wood.Normal, true, true);

            var decalPx = TextureGen.Decals(out int dw, out int dh);
            var decals = SaveTexture("Decals", dw, dh, decalPx, FilterMode.Point);

            // Board and table.
            // The table quad is 240 units across; one wood texture covers 36 units (four planks).
            Lit("Table", Color.white, 1f, albedo: woodAlbedo, mask: woodMask, normal: woodNormal, normalScale: 0.8f,
                tiling: new Vector2(240f / 36f, 240f / 36f));
            Lit("BoardBase", new Color(0.02f, 0.02f, 0.022f), 0.35f);
            Lit("Floor", Color.white, 1f, albedo: floorAlbedo, mask: floorMask, normal: floorNormal, normalScale: 0.6f);

            // Walls: glittery teal sides with LED strips, dark speckled tops, glossy blue rims.
            Lit("WallSide", Color.white, 1f, albedo: sideAlbedo, mask: sideMask, normal: sideNormal, normalScale: 0.7f,
                emission: new Color(2.4f, 1.9f, 0.6f), emissionMap: sideEmission);
            Lit("WallTop", Color.white, 1f, albedo: topAlbedo, mask: topMask, normal: topNormal, normalScale: 0.6f);
            Lit("WallRim", new Color(0.05f, 0.35f, 1f), 0.82f, metallic: 0.1f, emission: new Color(0.03f, 0.24f, 1.1f));
            Lit("Door", new Color(1f, 0.55f, 0.85f), 0.7f, emission: new Color(0.9f, 0.35f, 0.7f));
            var decal = Lit("Decal", Color.white, 0.5f, albedo: decals, emission: new Color(0.25f, 0.25f, 0.25f), emissionMap: decals);
            AlphaClip(decal);

            // Pellets glow like the LEDs in the photo.
            Lit("Dot", new Color(1f, 0.78f, 0.1f), 0.6f, emission: new Color(1.7f, 1.05f, 0.06f));
            Lit("Energizer", new Color(1f, 0.8f, 0.15f), 0.8f, emission: new Color(3f, 1.9f, 0.15f));

            // Characters (names match the Blender materials).
            Lit("PacYellow", new Color(1f, 0.84f, 0.08f), 0.72f, emission: new Color(0.16f, 0.12f, 0f));
            Lit("PacMouth", new Color(0.32f, 0.02f, 0.05f), 0.4f);
            Lit("PacEye", new Color(0.02f, 0.02f, 0.03f), 0.92f);
            Lit("GhostBody", Color.white, 0.5f);
            Lit("GhostBlinky", new Color(0.92f, 0.06f, 0.08f), 0.45f, emission: new Color(0.12f, 0f, 0f));
            Lit("GhostPinky", new Color(1f, 0.5f, 0.82f), 0.45f, emission: new Color(0.12f, 0.04f, 0.09f));
            var inky = Lit("GhostInky", new Color(0.3f, 0.92f, 1f, 0.62f), 0.95f, emission: new Color(0.02f, 0.14f, 0.18f));
            Transparent(inky);
            Lit("GhostClyde", new Color(1f, 0.5f, 0.1f), 0.45f, emission: new Color(0.12f, 0.05f, 0f));
            Lit("GhostFrightened", new Color(0.1f, 0.16f, 1f), 0.5f, emission: new Color(0.03f, 0.06f, 0.5f));
            Lit("GhostFlash", new Color(0.95f, 0.95f, 1f), 0.5f, emission: new Color(0.5f, 0.5f, 0.55f));
            Lit("EyeWhite", new Color(0.97f, 0.97f, 1f), 0.8f, emission: new Color(0.08f, 0.08f, 0.08f));
            Lit("Pupil", new Color(0.08f, 0.16f, 0.85f), 0.9f);
            Lit("FrightFace", new Color(1f, 0.78f, 0.65f), 0.5f, emission: new Color(0.35f, 0.25f, 0.2f));
            Lit("FrightFaceFlash", new Color(1f, 0.1f, 0.1f), 0.5f, emission: new Color(0.6f, 0f, 0f));

            // Fruit palette (see Assets/Resources/PixelArt/fruits.json).
            Lit("FruitRed", new Color(0.92f, 0.08f, 0.12f), 0.65f, emission: new Color(0.08f, 0f, 0f));
            Lit("FruitWhite", new Color(1f, 0.95f, 0.92f), 0.6f);
            Lit("FruitBrown", new Color(0.55f, 0.32f, 0.14f), 0.4f);
            Lit("FruitGreen", new Color(0.14f, 0.72f, 0.2f), 0.55f);
            Lit("FruitOrange", new Color(1f, 0.55f, 0.1f), 0.6f, emission: new Color(0.08f, 0.03f, 0f));
            Lit("FruitLightGreen", new Color(0.5f, 0.9f, 0.32f), 0.55f);
            Lit("FruitDarkGreen", new Color(0.06f, 0.45f, 0.14f), 0.55f);
            Lit("FruitYellow", new Color(1f, 0.85f, 0.1f), 0.7f, emission: new Color(0.1f, 0.08f, 0f));
            Lit("FruitBlue", new Color(0.12f, 0.3f, 1f), 0.6f);
            Lit("FruitCyan", new Color(0.2f, 0.9f, 1f), 0.7f);
            Lit("FruitSilver", new Color(0.78f, 0.8f, 0.84f), 0.8f, metallic: 0.8f);

            BuildTableMaterials(topMask, topNormal);

            var particles = Material(MatDir + "/Particles.mat", Shader.Find("Universal Render Pipeline/Particles/Lit"));
            particles.SetColor("_BaseColor", Color.white);
            particles.SetFloat("_Smoothness", 0.5f);
            EditorUtility.SetDirty(particles);
        }

        /// <summary>The props around the board (names match Blender/build_props.py), plus the light and air above the table.</summary>
        static void BuildTableMaterials(Texture2D glitterMask, Texture2D glitterNormal)
        {
            var page = SaveTexture("NotebookPage", 512, 680, TextureGen.NotebookPage(512, 680), FilterMode.Bilinear);
            var boxArt = SaveTexture("BoxArt", 512, 512, TextureGen.BoxArt(512), FilterMode.Bilinear);
            var stain = SaveTexture("CoffeeStain", 256, 256, TextureGen.CoffeeStain(256), false, wrap: TextureWrapMode.Clamp);
            var dot = SaveTexture("SoftDot", 64, 64, TextureGen.SoftDot(64), false, wrap: TextureWrapMode.Clamp);
            SaveTexture("WindowCookie", 256, 256, TextureGen.WindowCookie(256), true, wrap: TextureWrapMode.Clamp, dir: RuntimeTexDir);

            Lit("MugCeramic", new Color(0.96f, 0.93f, 0.87f), 0.85f);
            Lit("MugStripe", new Color(0.12f, 0.32f, 0.95f), 0.85f);
            Lit("Coffee", new Color(0.14f, 0.07f, 0.03f), 0.95f);
            Lit("SpoolPlastic", new Color(0.3f, 0.33f, 0.38f), 0.65f);
            Lit("SpoolHole", new Color(0.01f, 0.01f, 0.012f), 0.2f);
            Lit("SpoolLabel", new Color(0.92f, 0.92f, 0.9f), 0.3f);
            // The filament the maze was printed with: teal glitter, and a blue spool for the rims.
            Lit("Filament", new Color(0.12f, 0.55f, 0.5f), 1f, mask: glitterMask, normal: glitterNormal, tiling: new Vector2(6f, 6f));
            Lit("FilamentBlue", new Color(0.08f, 0.28f, 0.95f), 0.85f, emission: new Color(0.01f, 0.05f, 0.2f));
            Lit("ArcadeBlack", new Color(0.03f, 0.03f, 0.035f), 0.6f);
            Lit("ArcadePanel", new Color(1f, 0.75f, 0.08f), 0.7f);
            Lit("Metal", new Color(0.75f, 0.75f, 0.78f), 0.85f, metallic: 1f);
            Lit("ArcadeRed", new Color(0.9f, 0.05f, 0.07f), 0.85f, emission: new Color(0.12f, 0f, 0f));
            Lit("ArcadeBlue", new Color(0.1f, 0.3f, 1f), 0.85f, emission: new Color(0f, 0.04f, 0.15f));
            Lit("ArcadeGreen", new Color(0.1f, 0.8f, 0.25f), 0.85f, emission: new Color(0f, 0.1f, 0.02f));
            Lit("GameBox", Color.white, 0.35f, albedo: boxArt);
            Lit("GameBoxSide", new Color(0.05f, 0.08f, 0.35f), 0.35f);
            Lit("NotebookCover", new Color(0.08f, 0.14f, 0.3f), 0.3f);
            Lit("NotebookPage", Color.white, 0.15f, albedo: page);
            Lit("PageEdge", new Color(0.9f, 0.88f, 0.82f), 0.1f);
            Lit("PencilYellow", new Color(1f, 0.75f, 0.1f), 0.6f);
            Lit("PencilWood", new Color(0.85f, 0.65f, 0.45f), 0.25f);
            Lit("Graphite", new Color(0.12f, 0.12f, 0.14f), 0.7f, metallic: 0.5f);
            Lit("Eraser", new Color(0.95f, 0.5f, 0.58f), 0.2f);
            Lit("DiceWhite", new Color(0.97f, 0.96f, 0.92f), 0.8f);
            Lit("DicePip", new Color(0.02f, 0.02f, 0.03f), 0.6f);
            Lit("TokenGold", new Color(1f, 0.75f, 0.3f), 0.8f, metallic: 1f);
            Lit("PotTerracotta", new Color(0.72f, 0.36f, 0.2f), 0.25f);
            Lit("Soil", new Color(0.12f, 0.08f, 0.05f), 0.1f);
            Lit("LeafGreen", new Color(0.25f, 0.55f, 0.22f), 0.55f);
            Lit("LeafDark", new Color(0.1f, 0.33f, 0.15f), 0.55f);

            // The LED strip under the board; Tabletop recolours an instance of it.
            Lit("Underglow", Color.black, 0.5f, emission: new Color(0.2f, 0.5f, 2f));

            var stainMat = Lit("CoffeeStain", Color.white, 0.6f, albedo: stain);
            Transparent(stainMat);

            var dust = Material(MatDir + "/Dust.mat", Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            dust.SetTexture("_BaseMap", dot);
            dust.SetColor("_BaseColor", Color.white);
            ParticleBlend(dust, additive: true);
            var steam = Material(MatDir + "/Steam.mat", Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            steam.SetTexture("_BaseMap", dot);
            steam.SetColor("_BaseColor", Color.white);
            ParticleBlend(steam, additive: false);
        }

        static void ParticleBlend(Material mat, bool additive)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", additive ? 2f : 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
        }

        static Material Material(string path, Shader shader)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }
            return mat;
        }

        static Material Lit(string name, Color color, float smoothness, float metallic = 0f, Texture2D albedo = null,
            Texture2D mask = null, Texture2D normal = null, float normalScale = 1f, Color? emission = null,
            Texture2D emissionMap = null, Vector2? tiling = null)
        {
            var mat = Material($"{MatDir}/{name}.mat", Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            mat.SetTexture("_BaseMap", albedo);
            mat.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);

            mat.SetTexture("_MetallicGlossMap", mask);
            SetKeyword(mat, "_METALLICSPECGLOSSMAP", mask != null);
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", normalScale);
            SetKeyword(mat, "_NORMALMAP", normal != null);

            bool emissive = emission.HasValue;
            mat.SetColor("_EmissionColor", emissive ? emission.Value : Color.black);
            mat.SetTexture("_EmissionMap", emissionMap);
            SetKeyword(mat, "_EMISSION", emissive);
            mat.globalIlluminationFlags = emissive ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void Transparent(Material mat)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
        }

        static void AlphaClip(Material mat)
        {
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(mat);
        }

        static void SetKeyword(Material mat, string keyword, bool on)
        {
            if (on) mat.EnableKeyword(keyword);
            else mat.DisableKeyword(keyword);
        }

        // ------------------------------------------------------------------ post-processing

        static VolumeProfile BuildVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumePath);
            }
            foreach (var c in profile.components.ToArray())
            {
                profile.Remove(c.GetType());
                Object.DestroyImmediate(c, true);
            }

            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.85f);
            bloom.scatter.Override(0.62f);
            bloom.highQualityFiltering.Override(true);

            var tonemap = Add<Tonemapping>(profile);
            tonemap.mode.Override(TonemappingMode.Neutral);

            var color = Add<ColorAdjustments>(profile);
            color.postExposure.Override(0.25f);
            color.contrast.Override(12f);
            color.saturation.Override(14f);

            var vignette = Add<Vignette>(profile);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.45f);

            // A gentle miniature (tilt-shift) feel; focus distance is set every frame by the camera rig.
            var dof = Add<DepthOfField>(profile);
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(40f);
            dof.focalLength.Override(65f);
            dof.aperture.Override(6f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var c = profile.Add<T>(true);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }

        // ------------------------------------------------------------------ scene

        static void BuildScene(VolumeProfile profile)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.07f, 0.06f);
            cam.fieldOfView = 30f;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camGo.transform.SetPositionAndRotation(new Vector3(13.5f, 45f, -42f), Quaternion.Euler(60f, 0f, 0f));

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.94f, 0.85f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sunGo.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            sunGo.AddComponent<UniversalAdditionalLightData>();

            var volume = new GameObject("Post Processing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            new GameObject("Pac-Man 3D").AddComponent<GameController>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
