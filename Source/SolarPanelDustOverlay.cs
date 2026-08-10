using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace WeatherDrivenSolarPanel
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public sealed class WDSPDustShaderLoader : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            WDSPDustShaderLibrary.EnsureLoaded();
        }
    }

    internal static class WDSPDustShaderLibrary
    {
        internal const string ShaderName = "WeatherDrivenSolarPanel/DustOverlay";

        private static Shader dustShader;
        private static Material dustMaterial;
        private static Texture2D fallbackNoise;
        private static bool loadAttempted;
        private static bool warnedAboutFallback;

        internal static Material SharedMaterial
        {
            get
            {
                EnsureLoaded();
                return dustMaterial;
            }
        }

        internal static bool UsesCustomShader
        {
            get
            {
                EnsureLoaded();
                return dustShader != null && dustShader.name == ShaderName;
            }
        }

        internal static void EnsureLoaded()
        {
            if (dustMaterial != null)
            {
                return;
            }

            if (!loadAttempted)
            {
                loadAttempted = true;
                LoadBundle();
            }

            if (dustShader == null)
            {
                dustShader = Shader.Find(ShaderName);
            }

            if (dustShader != null)
            {
                dustMaterial = new Material(dustShader)
                {
                    name = "WDSP Dust Overlay (Shared)",
                    hideFlags = HideFlags.HideAndDontSave
                };
                return;
            }

            Shader fallbackShader = Shader.Find("KSP/Alpha/Translucent")
                ?? Shader.Find("Legacy Shaders/Transparent/Diffuse")
                ?? Shader.Find("Unlit/Transparent Colored")
                ?? Shader.Find("Unlit/Transparent");
            if (fallbackShader == null)
            {
                Debug.LogWarning("[WDSP] Dust overlay disabled: no compatible transparent shader was found.");
                return;
            }

            dustMaterial = new Material(fallbackShader)
            {
                name = "WDSP Dust Overlay Fallback (Shared)",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = CreateFallbackNoise()
            };

            if (!warnedAboutFallback)
            {
                warnedAboutFallback = true;
                Debug.LogWarning(
                    "[WDSP] Dust shader bundle was not found; using the lower-detail built-in shader fallback.");
            }
        }

        private static void LoadBundle()
        {
            try
            {
                string path = Path.Combine(
                    KSPUtil.ApplicationRootPath,
                    "GameData/WeatherDrivenSolarPanel/Shaders/weatherdrivensolarpanel.bundle");
                if (!File.Exists(path))
                {
                    return;
                }

                AssetBundle bundle = AssetBundle.LoadFromFile(path);
                if (bundle == null)
                {
                    Debug.LogWarning("[WDSP] Failed to load dust shader bundle: " + path);
                    return;
                }

                Shader[] shaders = bundle.LoadAllAssets<Shader>();
                for (int i = 0; i < shaders.Length; i++)
                {
                    Shader shader = shaders[i];
                    if (shader != null && shader.name == ShaderName)
                    {
                        dustShader = shader;
                        break;
                    }
                }
                bundle.Unload(false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WDSP] Dust shader bundle load failed: " + ex.Message);
            }
        }

        private static Texture2D CreateFallbackNoise()
        {
            if (fallbackNoise != null)
            {
                return fallbackNoise;
            }

            const int size = 64;
            fallbackNoise = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "WDSP Procedural Dust Noise",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float broad = Mathf.PerlinNoise(x * 0.085f + 3.7f, y * 0.085f + 8.1f);
                    float fine = Mathf.PerlinNoise(x * 0.31f + 17.3f, y * 0.31f + 2.4f);
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Lerp(35f, 230f, broad * 0.72f + fine * 0.28f));
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            fallbackNoise.SetPixels32(pixels);
            fallbackNoise.Apply(true, true);
            return fallbackNoise;
        }
    }

    internal sealed class SolarPanelDustOverlay : IDisposable
    {
        private const string OverlayPrefix = "WDSP_DustOverlay_";
        /// <summary>Booms / hinges dust a bit less than primary faces to limit transparent overdraw.</summary>
        private const float StructureDustScale = 0.55f;
        private static readonly int DustAmountId = Shader.PropertyToID("_DustAmount");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int LightScaleId = Shader.PropertyToID("_LightScale");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly List<OverlayRenderer> overlays = new List<OverlayRenderer>();
        private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        private readonly float seed;
        private readonly Part part;

        private float lastDustAmount = -1f;
        private float lastLightScale = -1f;
        private bool lastVisible;

        private sealed class OverlayRenderer
        {
            internal Renderer Source;
            internal Renderer Overlay;
            internal GameObject GameObject;
            internal float DustScale = 1f;
        }

        private SolarPanelDustOverlay(Part part)
        {
            this.part = part;
            seed = part != null ? (part.flightID % 10000u) * 0.137f : 0f;
        }

        /// <param name="panelAnchors">Ignored; kept so call sites stay compatible. Dust covers all geometry meshes.</param>
        internal static SolarPanelDustOverlay Create(Part part, IEnumerable<Transform> panelAnchors)
        {
            if (part == null || WDSPDustShaderLibrary.SharedMaterial == null)
            {
                return null;
            }

            SolarPanelDustOverlay controller = new SolarPanelDustOverlay(part);
            controller.Build(part);
            if (controller.overlays.Count == 0)
            {
                controller.Dispose();
                return null;
            }
            return controller;
        }

        /// <summary>Legacy helper still used by panel modules; dust selection no longer depends on anchors.</summary>
        internal static List<Transform> FindPanelAnchors(Part part, PartModule targetModule)
        {
            List<Transform> anchors = new List<Transform>();
            if (part == null)
            {
                return anchors;
            }

            if (targetModule is ModuleDeployableSolarPanel stockPanel)
            {
                AddTransformsByName(part, stockPanel.secondaryTransformName, anchors);
                AddTransformsByName(part, stockPanel.raycastTransformName, anchors);
                if (anchors.Count == 0)
                {
                    AddTransformsByName(part, stockPanel.pivotName, anchors);
                }
                return anchors;
            }

            if (targetModule != null)
            {
                string[] memberNames =
                {
                    "secondaryTransformName",
                    "pivotName",
                    "PanelTransformName",
                    "panelTransformName",
                    "suncatcherTransformName",
                    "suncatcherTransforms"
                };
                for (int i = 0; i < memberNames.Length; i++)
                {
                    object value = ReadMember(targetModule, memberNames[i]);
                    if (value is string transformNames)
                    {
                        string[] names = transformNames.Split(
                            new[] { ',', ';', ' ' },
                            StringSplitOptions.RemoveEmptyEntries);
                        for (int n = 0; n < names.Length; n++)
                        {
                            AddTransformsByName(part, names[n], anchors);
                        }
                    }
                }
            }

            return anchors;
        }

        internal void Update(float dustAmount, bool visible)
        {
            dustAmount = Mathf.Clamp01(dustAmount);
            float lightScale = EvaluateLightScale();
            bool parametersChanged = Mathf.Abs(lastDustAmount - dustAmount) > 0.001f
                || Mathf.Abs(lastLightScale - lightScale) > 0.01f;
            bool visibilityChanged = lastVisible != visible;

            bool wantsDust = visible && dustAmount > 0.001f;
            if (!parametersChanged && !visibilityChanged)
            {
                SyncSourceVisibility(wantsDust);
                return;
            }

            lastDustAmount = dustAmount;
            lastLightScale = lightScale;
            lastVisible = visible;

            // Fallback translucent shaders are nearly unlit; keep opacity, only dim colour in umbra.
            float fallbackBrightness = Mathf.Lerp(0.32f, 1f, lightScale);

            for (int i = 0; i < overlays.Count; i++)
            {
                OverlayRenderer pair = overlays[i];
                if (pair.Overlay == null)
                {
                    continue;
                }

                float scaledDust = Mathf.Clamp01(dustAmount * pair.DustScale);
                float fallbackAlpha = Mathf.Clamp01(scaledDust * 0.68f);

                // Per-renderer seed from part + world position so cloned meshes diverge.
                Vector3 center = pair.Source != null ? pair.Source.bounds.center : Vector3.zero;
                float segmentSeed = seed
                    + i * 1.713f
                    + center.x * 0.19f
                    + center.y * 0.37f
                    + center.z * 0.23f;
                propertyBlock.Clear();
                propertyBlock.SetFloat(DustAmountId, scaledDust);
                propertyBlock.SetFloat(SeedId, segmentSeed);
                propertyBlock.SetFloat(LightScaleId, lightScale);
                propertyBlock.SetColor(
                    ColorId,
                    new Color(0.74f * fallbackBrightness, 0.59f * fallbackBrightness, 0.40f * fallbackBrightness, fallbackAlpha));

                SyncSkinnedBones(pair);
                pair.Overlay.enabled = wantsDust && IsSourceActive(pair.Source);
                pair.Overlay.SetPropertyBlock(propertyBlock);
            }
        }

        private float EvaluateLightScale()
        {
            Vessel vessel = part != null ? part.vessel : null;
            if (vessel == null || !HighLogic.LoadedSceneIsFlight)
            {
                return 1f;
            }

            // Planet umbra: stock parts go dark, but directional light still exists for N·L.
            return vessel.directSunlight ? 1f : 0.08f;
        }

        public void Dispose()
        {
            for (int i = 0; i < overlays.Count; i++)
            {
                GameObject overlayObject = overlays[i].GameObject;
                if (overlayObject != null)
                {
                    UnityEngine.Object.Destroy(overlayObject);
                }
            }
            overlays.Clear();
        }

        private void Build(Part part)
        {
            List<Renderer> renderers = part.FindModelComponents<Renderer>();
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];
                if (!ShouldDustRenderer(renderer))
                {
                    continue;
                }

                AddOverlay(renderer, EvaluateDustScale(renderer));
            }
        }

        /// <summary>
        /// Rebuild if B9 / deploy animation reveals geometry that was missing at first build.
        /// </summary>
        internal bool TryRebuildIfIncomplete(Part part)
        {
            if (part == null)
            {
                return false;
            }

            int expected = CountDustableRenderers(part);
            if (expected <= overlays.Count)
            {
                return false;
            }

            Dispose();
            Build(part);
            lastDustAmount = -1f;
            lastLightScale = -1f;
            lastVisible = false;
            return overlays.Count > 0;
        }

        private static int CountDustableRenderers(Part part)
        {
            int count = 0;
            List<Renderer> renderers = part.FindModelComponents<Renderer>();
            for (int i = 0; i < renderers.Count; i++)
            {
                if (ShouldDustRenderer(renderers[i]))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Universal path: dust every geometry mesh on the solar-panel part.
        /// Only FX / utility names are skipped — never drop thin flat panel sheets.
        /// </summary>
        private static bool ShouldDustRenderer(Renderer renderer)
        {
            if (renderer == null
                || (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                || renderer.gameObject == null
                || renderer.gameObject.name.StartsWith(OverlayPrefix, StringComparison.Ordinal)
                || IsExcludedUtilityName(renderer.gameObject.name)
                || IsExcludedUtilityName(renderer.name)
                || !HasRenderableMesh(renderer))
            {
                return false;
            }

            return true;
        }

        private static bool HasRenderableMesh(Renderer renderer)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null && filter.sharedMesh.subMeshCount > 0)
            {
                return true;
            }

            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
            return skinned != null && skinned.sharedMesh != null && skinned.sharedMesh.subMeshCount > 0;
        }

        private static float EvaluateDustScale(Renderer renderer)
        {
            // Full strength on cell-like / flat faces; dimmer on booms / true rods.
            if (LooksLikePrimarySurface(renderer))
            {
                return 1f;
            }
            return StructureDustScale;
        }

        private static bool LooksLikePrimarySurface(Renderer renderer)
        {
            if (NameOrMaterialSuggestsPanel(renderer))
            {
                return true;
            }

            return IsReasonablyFlatSurface(renderer);
        }

        private static bool NameOrMaterialSuggestsPanel(Renderer renderer)
        {
            if (ContainsPanelKeyword(renderer.name) || ContainsPanelKeyword(renderer.gameObject.name))
            {
                return true;
            }

            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && ContainsPanelKeyword(materials[i].name))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsPanelKeyword(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string lower = value.ToLowerInvariant();
            return lower.Contains("solar")
                || lower.Contains("panel")
                || lower.Contains("photovoltaic")
                || lower.Contains("suncatcher")
                || lower.Contains("solarcell")
                || lower.Contains("pvcell")
                || lower.Contains("gaas")
                || lower.Contains("blanket")
                || lower.Contains("array")
                || lower.Contains("nfs-panel")
                || (lower.Contains("cell") && !lower.Contains("cancel"));
        }

        private static bool IsReasonablyFlatSurface(Renderer renderer)
        {
            Vector3 extents = renderer.bounds.extents;
            float[] edges = { extents.x * 2f, extents.y * 2f, extents.z * 2f };
            Array.Sort(edges);
            float thickness = Mathf.Max(edges[0], 0.001f);
            float mid = Mathf.Max(edges[1], 0.001f);
            float longest = Mathf.Max(edges[2], 0.001f);
            if (longest / mid > 8f)
            {
                return false;
            }
            return mid / thickness >= 2.2f;
        }

        private static bool IsExcludedUtilityName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string lower = value.ToLowerInvariant();
            // Narrow blacklist only — never used as the primary "find the panel" heuristic.
            return lower.Contains("collider")
                || lower.Contains("trigger")
                || lower.Contains("icon")
                || lower.Contains("thumb")
                || lower.Contains("decal")
                || lower.Contains("flag")
                || lower.Contains("emissive")
                || lower.Contains("spotlight")
                || lower.Contains("pointlight")
                || lower.Contains("flare")
                || lower.Contains("thruster")
                || lower.Contains("exhaust")
                || lower.Contains("antenna")
                || lower.Contains("fairing");
        }

        private void AddOverlay(Renderer source, float dustScale)
        {
            Mesh mesh = null;
            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            SkinnedMeshRenderer sourceSkinned = source as SkinnedMeshRenderer;
            if (sourceFilter != null)
            {
                mesh = sourceFilter.sharedMesh;
            }
            else if (sourceSkinned != null)
            {
                mesh = sourceSkinned.sharedMesh;
            }

            if (mesh == null || mesh.subMeshCount == 0)
            {
                return;
            }

            GameObject overlayObject = new GameObject(OverlayPrefix + source.gameObject.name)
            {
                layer = source.gameObject.layer,
                hideFlags = HideFlags.DontSave
            };
            Transform overlayTransform = overlayObject.transform;
            overlayTransform.SetParent(source.transform, false);
            overlayTransform.localPosition = Vector3.zero;
            overlayTransform.localRotation = Quaternion.identity;
            overlayTransform.localScale = Vector3.one;

            Renderer overlayRenderer;
            if (sourceSkinned != null)
            {
                SkinnedMeshRenderer skinned = overlayObject.AddComponent<SkinnedMeshRenderer>();
                skinned.sharedMesh = sourceSkinned.sharedMesh;
                skinned.bones = sourceSkinned.bones;
                skinned.rootBone = sourceSkinned.rootBone;
                skinned.localBounds = sourceSkinned.localBounds;
                skinned.updateWhenOffscreen = sourceSkinned.updateWhenOffscreen;
                overlayRenderer = skinned;
            }
            else
            {
                MeshFilter overlayFilter = overlayObject.AddComponent<MeshFilter>();
                overlayFilter.sharedMesh = mesh;
                overlayRenderer = overlayObject.AddComponent<MeshRenderer>();
            }

            overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            overlayRenderer.receiveShadows = false;
            overlayRenderer.lightProbeUsage = LightProbeUsage.Off;
            overlayRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            overlayRenderer.sortingLayerID = source.sortingLayerID;
            overlayRenderer.sortingOrder = source.sortingOrder + 1;

            Material[] materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = WDSPDustShaderLibrary.SharedMaterial;
            }
            overlayRenderer.sharedMaterials = materials;
            overlayRenderer.enabled = false;

            overlays.Add(new OverlayRenderer
            {
                Source = source,
                Overlay = overlayRenderer,
                GameObject = overlayObject,
                DustScale = Mathf.Clamp(dustScale, 0.05f, 1f)
            });
        }

        private void SyncSourceVisibility(bool wantsDust)
        {
            for (int i = 0; i < overlays.Count; i++)
            {
                OverlayRenderer pair = overlays[i];
                if (pair.Overlay != null)
                {
                    SyncSkinnedBones(pair);
                    pair.Overlay.enabled = wantsDust && IsSourceActive(pair.Source);
                }
            }
        }

        private static bool IsSourceActive(Renderer source)
        {
            return source != null
                && source.enabled
                && source.gameObject != null
                && source.gameObject.activeInHierarchy;
        }

        private static void SyncSkinnedBones(OverlayRenderer pair)
        {
            SkinnedMeshRenderer sourceSkinned = pair.Source as SkinnedMeshRenderer;
            SkinnedMeshRenderer overlaySkinned = pair.Overlay as SkinnedMeshRenderer;
            if (sourceSkinned == null || overlaySkinned == null)
            {
                return;
            }

            overlaySkinned.bones = sourceSkinned.bones;
            overlaySkinned.rootBone = sourceSkinned.rootBone;
            overlaySkinned.localBounds = sourceSkinned.localBounds;
        }

        private static void AddTransformsByName(Part part, string transformName, List<Transform> transforms)
        {
            if (string.IsNullOrEmpty(transformName))
            {
                return;
            }

            Transform[] found = part.FindModelTransforms(transformName);
            if (found == null || found.Length == 0)
            {
                found = part.FindModelTransforms(transformName + "s");
            }
            if (found == null)
            {
                return;
            }

            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && !transforms.Contains(found[i]))
                {
                    transforms.Add(found[i]);
                }
            }
        }

        private static object ReadMember(object instance, string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = instance.GetType();
            FieldInfo field = type.GetField(name, flags);
            if (field != null)
            {
                return field.GetValue(instance);
            }

            PropertyInfo property = type.GetProperty(name, flags);
            return property != null && property.CanRead ? property.GetValue(instance, null) : null;
        }
    }
}
