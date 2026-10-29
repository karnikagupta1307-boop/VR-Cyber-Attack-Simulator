using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CyberDefenseVR.EditorTools
{
    /// <summary>
    /// Generates every material, prefab and scene of the project and configures URP / Player / Build settings.
    /// Menu: CyberDefense > Generate Project Assets. Also runs once automatically the first time the project is opened.
    /// Batch: Unity -batchmode -projectPath . -executeMethod CyberDefenseVR.EditorTools.CyberDefenseProjectBuilder.GenerateBatch  (do NOT pass -quit)
    /// </summary>
    public static class CyberDefenseProjectBuilder
    {
        const string MenuScene = "Assets/Scenes/MainMenu.unity";
        const string TrainScene = "Assets/Scenes/CyberDefenseTraining.unity";
        const string DesktopScene = "Assets/Scenes/DesktopTest.unity";
        const string Mats = "Assets/Art/Materials/";
        const string Prefabs = "Assets/Prefabs/";

        static readonly Color Navy = new Color(0.035f, 0.06f, 0.11f, 0.96f);
        static readonly Color Cyan = new Color(0.1f, 0.85f, 1f, 1f);
        static readonly Color Orange = new Color(0.9f, 0.4f, 0.08f, 1f);
        static readonly Color Slate = new Color(0.16f, 0.23f, 0.33f, 1f);
        static readonly Color Teal = new Color(0.05f, 0.45f, 0.6f, 1f);
        static readonly Color Muted = new Color(0.62f, 0.7f, 0.8f, 1f);

        const string UiActionsPath = "Assets/Input/CyberDefenseUI.inputactions";
        static bool overwrite;                      // false = only create what is missing (safe to rerun)
        static UnityEngine.InputSystem.InputActionAsset uiActions;
        static UnityEditor.PackageManager.Requests.AddAndRemoveRequest pendingPackages;

        [MenuItem("CyberDefense/Generate Project Assets (missing only)")]
        public static void GenerateMenu() { Generate(false); }

        [MenuItem("CyberDefense/Regenerate Everything (overwrites generated scenes, prefabs, materials)")]
        public static void RegenerateMenu()
        {
            if (EditorUtility.DisplayDialog("Cyber Defense VR",
                "This overwrites MainMenu, CyberDefenseTraining, DesktopTest and the generated prefabs/materials.\n\nAny edits you made to those assets will be lost. Continue?",
                "Overwrite", "Cancel"))
                Generate(true);
        }

        [MenuItem("CyberDefense/Install XR Packages (OpenXR, Meta)")]
        public static void InstallXrPackagesMenu() { InstallXrPackages(); }

        public static void GenerateBatch() { Generate(false); }

        public static bool AllScenesExist()
        {
            return File.Exists(MenuScene) && File.Exists(TrainScene) && File.Exists(DesktopScene);
        }

        // ------------------------------------------------------------------ orchestration
        static void Fail(string msg)
        {
            Debug.LogError("[CyberDefenseVR] " + msg);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else EditorUtility.DisplayDialog("Cyber Defense VR - generation problem", msg, "OK");
        }

        static void Generate(bool overwriteExisting)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { Fail("Unity is still compiling/importing. Wait for it to finish, then run the generator again."); return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Fail("Exit Play Mode first."); return; }
            overwrite = overwriteExisting;
            EnsureFolders();
            if (Shader.Find("Universal Render Pipeline/Lit") == null)
            {
                Fail("URP shaders not found. Open Window > Package Manager and make sure 'Universal RP' is installed with no errors, then run the generator again.");
                return;
            }
            WarnIfXrPackagesMissing();
            ConfigureRenderPipeline();
            ConfigureProjectSettings();
            if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset")) { ImportTmpThen(BuildAll); return; }
            BuildAll();
        }

        static System.Collections.Generic.List<string> MissingXrPackages()
        {
            var have = new System.Collections.Generic.HashSet<string>();
            foreach (var p in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()) have.Add(p.name);
            var need = new System.Collections.Generic.List<string>();
            foreach (var n in new[] { "com.unity.xr.management", "com.unity.xr.openxr", "com.unity.xr.meta-openxr" })
                if (!have.Contains(n)) need.Add(n);
            return need;
        }

        static void WarnIfXrPackagesMissing()
        {
            var need = MissingXrPackages();
            if (need.Count > 0)
                Debug.LogWarning("[CyberDefenseVR] XR packages not installed (" + string.Join(", ", need) + "). Scenes are generated anyway; for headset builds run CyberDefense > Install XR Packages (OpenXR, Meta) as a separate step, after generation has finished.");
        }

        /// <summary>Separate, opt-in step: installs XR packages by NAME so Package Manager picks versions verified for this editor.</summary>
        static void InstallXrPackages()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { Debug.LogWarning("[CyberDefenseVR] Wait for compilation/import to finish first."); return; }
            if (pendingPackages != null) { Debug.LogWarning("[CyberDefenseVR] An XR package install is already running."); return; }
            var need = MissingXrPackages();
            if (need.Count == 0) { Debug.Log("[CyberDefenseVR] XR packages are already installed."); return; }
            Debug.Log("[CyberDefenseVR] Installing XR packages: " + string.Join(", ", need) + " (needs access to the Unity package registry)");
            pendingPackages = UnityEditor.PackageManager.Client.AddAndRemove(need.ToArray());
            EditorApplication.update += WatchPackages;
        }

        static void WatchPackages()
        {
            if (pendingPackages == null || !pendingPackages.IsCompleted) return;
            EditorApplication.update -= WatchPackages;
            var r = pendingPackages; pendingPackages = null;
            if (r.Status == UnityEditor.PackageManager.StatusCode.Failure)
                Debug.LogError("[CyberDefenseVR] XR package install failed: " + r.Error.message);
            else
                Debug.Log("[CyberDefenseVR] XR packages installed.");
        }

        static void ImportTmpThen(Action next)
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui/package.json");
            string pkg = info == null ? null : Path.Combine(info.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
            if (pkg == null || !File.Exists(pkg))
            {
                Fail("TMP Essential Resources not found. Use Window > TextMeshPro > Import TMP Essential Resources, then run the generator again.");
                return;
            }
            Debug.Log("[CyberDefenseVR] Importing TMP Essential Resources...");
            afterTmpImport = next;
            AssetDatabase.importPackageCompleted += OnTmpImportCompleted;
            AssetDatabase.ImportPackage(pkg, false);
        }

        static Action afterTmpImport;

        // Signature matches AssetDatabase's import-completed callback: void (string packageName).
        static void OnTmpImportCompleted(string packageName)
        {
            AssetDatabase.importPackageCompleted -= OnTmpImportCompleted;
            var next = afterTmpImport;
            afterTmpImport = null;
            if (next != null) EditorApplication.delayCall += () => next();
        }

        static void BuildAll()
        {
            try
            {
                uiActions = EnsureUiActionsAsset();
                BuildMaterials();
                BuildPrefabs();
                if (overwrite || !File.Exists(MenuScene)) BuildMenuScene(); else Debug.Log("[CyberDefenseVR] Keeping existing " + MenuScene);
                if (overwrite || !File.Exists(TrainScene)) BuildTrainingScene(true, TrainScene); else Debug.Log("[CyberDefenseVR] Keeping existing " + TrainScene);
                if (overwrite || !File.Exists(DesktopScene)) BuildTrainingScene(false, DesktopScene); else Debug.Log("[CyberDefenseVR] Keeping existing " + DesktopScene);
                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene(MenuScene, true),
                    new EditorBuildSettingsScene(TrainScene, true),
                    new EditorBuildSettingsScene(DesktopScene, true)
                };
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (Exception e) { Fail("Generation threw an exception:\n" + e); return; }

            int fails = ProjectValidator.Run();
            EditorSceneManager.OpenScene(MenuScene);
            string msg = fails == 0
                ? "Generation complete. Validation passed. Open Assets/Scenes/MainMenu.unity and press Play."
                : "Generation finished but validation found " + fails + " problem(s). See ValidationReport.txt and the Console.";
            Debug.Log("[CyberDefenseVR] " + msg);
            if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 1);
            else EditorUtility.DisplayDialog("Cyber Defense VR", msg, "OK");
        }

        // ------------------------------------------------------------------ UI input actions (serialized InputActionAsset)
        [Serializable] public class SchemeJson { public string name = ""; }
        [Serializable] public class ActionJson
        {
            public string name = "", type = "", id = "", expectedControlType = "", processors = "", interactions = "";
            public bool initialStateCheck;
        }
        [Serializable] public class BindingJson
        {
            public string name = "", id = "", path = "", interactions = "", processors = "", groups = "", action = "";
            public bool isComposite, isPartOfComposite;
        }
        [Serializable] public class MapJson { public string name = ""; public string id = ""; public ActionJson[] actions; public BindingJson[] bindings; }
        [Serializable] public class AssetJson { public string name = "CyberDefenseUI"; public MapJson[] maps; public SchemeJson[] controlSchemes = new SchemeJson[0]; }

        static readonly string[] RequiredUiActions =
            { "Navigate", "Submit", "Cancel", "Point", "Click", "RightClick", "MiddleClick", "ScrollWheel", "TrackedDevicePosition", "TrackedDeviceOrientation" };

        static string BuildUiActionsJson()
        {
            var actions = new System.Collections.Generic.List<ActionJson>();
            var bindings = new System.Collections.Generic.List<BindingJson>();
            Action<string, string, string, bool> act = (n, t, ctl, init) =>
                actions.Add(new ActionJson { name = n, type = t, expectedControlType = ctl, id = Guid.NewGuid().ToString(), initialStateCheck = init });
            Action<string, string> bind = (a, path) =>
                bindings.Add(new BindingJson { id = Guid.NewGuid().ToString(), path = path, action = a });

            act("Navigate", "PassThrough", "Vector2", true);
            act("Submit", "Button", "Button", false);
            act("Cancel", "Button", "Button", false);
            act("Point", "PassThrough", "Vector2", true);
            act("Click", "PassThrough", "Button", true);
            act("RightClick", "PassThrough", "Button", false);
            act("MiddleClick", "PassThrough", "Button", false);
            act("ScrollWheel", "PassThrough", "Vector2", false);
            act("TrackedDevicePosition", "PassThrough", "Vector3", false);
            act("TrackedDeviceOrientation", "PassThrough", "Quaternion", false);

            bind("Navigate", "<Gamepad>/leftStick");
            bind("Navigate", "<Gamepad>/dpad");
            bindings.Add(new BindingJson { name = "Arrow Keys", id = Guid.NewGuid().ToString(), path = "2DVector(mode=2)", action = "Navigate", isComposite = true });
            string[][] parts = { new[] { "up", "<Keyboard>/upArrow" }, new[] { "down", "<Keyboard>/downArrow" }, new[] { "left", "<Keyboard>/leftArrow" }, new[] { "right", "<Keyboard>/rightArrow" } };
            foreach (var pt in parts)
                bindings.Add(new BindingJson { name = pt[0], id = Guid.NewGuid().ToString(), path = pt[1], action = "Navigate", isPartOfComposite = true });
            bind("Submit", "<Keyboard>/enter"); bind("Submit", "<Gamepad>/buttonSouth");
            bind("Cancel", "<Keyboard>/escape"); bind("Cancel", "<Gamepad>/buttonEast");
            bind("Point", "<Mouse>/position"); bind("Point", "<Pen>/position"); bind("Point", "<Touchscreen>/primaryTouch/position");
            bind("Click", "<Mouse>/leftButton"); bind("Click", "<Pen>/tip"); bind("Click", "<Touchscreen>/primaryTouch/press");
            bind("Click", "<XRController>/{TriggerButton}");
            bind("RightClick", "<Mouse>/rightButton");
            bind("MiddleClick", "<Mouse>/middleButton");
            bind("ScrollWheel", "<Mouse>/scroll");
            bind("TrackedDevicePosition", "<XRController>/pointerPosition");
            bind("TrackedDeviceOrientation", "<XRController>/pointerRotation");

            var root = new AssetJson { maps = new[] { new MapJson { name = "UI", id = Guid.NewGuid().ToString(), actions = actions.ToArray(), bindings = bindings.ToArray() } } };
            return JsonUtility.ToJson(root, true);
        }

        /// <summary>Creates (only if missing) and imports the .inputactions asset; the importer exposes each action as a persistent InputActionReference sub-asset.</summary>
        static UnityEngine.InputSystem.InputActionAsset EnsureUiActionsAsset()
        {
            Directory.CreateDirectory("Assets/Input");
            if (!File.Exists(UiActionsPath)) File.WriteAllText(UiActionsPath, BuildUiActionsJson());
            AssetDatabase.ImportAsset(UiActionsPath, ImportAssetOptions.ForceSynchronousImport);
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(UiActionsPath);
            if (asset == null)
                throw new InvalidOperationException("'" + UiActionsPath + "' could not be imported as an InputActionAsset. Check the Console for importer errors (is the Input System package installed and compiled?).");
            return asset;
        }

        static System.Collections.Generic.Dictionary<string, UnityEngine.InputSystem.InputActionReference> UiReferences()
        {
            var d = new System.Collections.Generic.Dictionary<string, UnityEngine.InputSystem.InputActionReference>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(UiActionsPath))
            {
                var r = o as UnityEngine.InputSystem.InputActionReference;
                if (r == null || r.action == null || r.action.actionMap == null || r.action.actionMap.name != "UI") continue;
                d[r.action.name] = r;
            }
            var missing = new System.Collections.Generic.List<string>();
            foreach (var n in RequiredUiActions) if (!d.ContainsKey(n)) missing.Add(n);
            if (missing.Count > 0)
                throw new InvalidOperationException("No persistent InputActionReference found in '" + UiActionsPath + "' for UI action(s): " + string.Join(", ", missing) +
                    ". Delete that file (and its .meta) and run the generator again, or check the Console for importer errors.");
            return d;
        }

        static void EnsureFolders()
        {
            string[] dirs = { "Assets/Scenes", "Assets/Art/Materials", "Assets/Prefabs/Player", "Assets/Prefabs/Network", "Assets/Prefabs/Attacks",
                              "Assets/Prefabs/Defense", "Assets/Prefabs/UI", "Assets/Settings" };
            foreach (var d in dirs) Directory.CreateDirectory(d);
            AssetDatabase.Refresh();
        }

        // ------------------------------------------------------------------ project settings
        static void ConfigureRenderPipeline()
        {
            const string rendererPath = "Assets/Settings/CyberDefense_Renderer.asset";
            const string assetPath = "Assets/Settings/CyberDefense_URP.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null)
            {
                var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rd, rendererPath);
                asset = UniversalRenderPipelineAsset.Create(rd);
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            asset.supportsHDR = false;
            asset.msaaSampleCount = 4;
            EditorUtility.SetDirty(asset);
            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = asset; }
            QualitySettings.SetQualityLevel(current, false);
            PlayerSettings.colorSpace = ColorSpace.Linear;
        }

        static void ConfigureProjectSettings()
        {
            PlayerSettings.companyName = "CyberDefense Labs";
            PlayerSettings.productName = "Cyber Defense VR";
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, "com.cyberdefense.vr");
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });

            // Active Input Handling = Input System Package (New)
            var all = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (Application.isBatchMode) Debug.LogWarning("[CyberDefenseVR] Batch mode: make sure Project Settings > Player > Active Input Handling = Input System Package (New).");
            else if (all != null && all.Length > 0)
            {
                var so = new SerializedObject(all[0]);
                var p = so.FindProperty("activeInputHandler");
                if (p != null && p.intValue != 1)
                {
                    p.intValue = 1; so.ApplyModifiedProperties();
                    Debug.LogWarning("[CyberDefenseVR] Active Input Handling set to 'Input System Package (New)'. Restart the Editor if prompted.");
                }
            }
        }

        // ------------------------------------------------------------------ materials
        static Material Mat(string name, Color baseC, Color? emis = null, float metallic = 0f, float smooth = 0.5f, bool transparent = false, bool unlit = false)
        {
            string path = Mats + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null && !overwrite) return m;
            if (m == null)
            {
                m = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", baseC);
            if (!unlit) { m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smooth); }
            if (emis.HasValue && !unlit)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emis.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            if (transparent)
            {
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material M(string n) { return AssetDatabase.LoadAssetAtPath<Material>(Mats + n + ".mat"); }

        static void BuildMaterials()
        {
            Mat("Floor", new Color(0.05f, 0.06f, 0.09f), null, 0.4f, 0.75f);
            Mat("Wall", new Color(0.07f, 0.09f, 0.13f), null, 0.1f, 0.35f);
            Mat("Metal", new Color(0.13f, 0.15f, 0.19f), null, 0.8f, 0.55f);
            Mat("Graphite", new Color(0.09f, 0.1f, 0.13f), null, 0.6f, 0.6f);
            Mat("NodeGlow", new Color(0.02f, 0.05f, 0.08f), Color.black, 0f, 0.4f);
            Mat("GlowCyan", new Color(0.02f, 0.1f, 0.14f), new Color(0.1f, 0.8f, 1f) * 2f);
            Mat("GlowBlue", new Color(0.02f, 0.04f, 0.14f), new Color(0.15f, 0.3f, 1f) * 2f);
            Mat("GlowOrange", new Color(0.12f, 0.05f, 0.01f), new Color(1f, 0.5f, 0.1f) * 3f);
            Mat("BeaconPanel", new Color(0.02f, 0.05f, 0.08f), Color.black);
            Mat("HoloDisk", new Color(0.05f, 0.5f, 0.7f, 0.22f), new Color(0.05f, 0.4f, 0.6f), 0f, 0.5f, true);
            Mat("Shield", new Color(0.1f, 0.8f, 1f, 0.28f), new Color(0.1f, 0.7f, 1f) * 1.2f, 0f, 0.5f, true);
            Mat("Beam", new Color(0.1f, 0.8f, 1f, 0.15f), new Color(0.1f, 0.8f, 1f) * 0.6f, 0f, 0.5f, true);
            Mat("LineUnlit", new Color(0.1f, 0.6f, 0.9f), null, 0, 0, false, true);
            Mat("PointerLine", new Color(0.3f, 0.95f, 1f), null, 0, 0, false, true);
            Mat("Controller", new Color(0.08f, 0.09f, 0.12f), null, 0.6f, 0.5f);
            Mat("MenuNode", new Color(0.02f, 0.1f, 0.14f), new Color(0.1f, 0.7f, 1f) * 1.5f);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ primitive / UI helpers
        static GameObject Prim(PrimitiveType t, string n, Transform parent, Vector3 pos, Vector3 scale, string mat)
        {
            var g = GameObject.CreatePrimitive(t);
            g.name = n;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale;
            var c = g.GetComponent<Collider>(); if (c != null) UnityEngine.Object.DestroyImmediate(c);
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = M(mat);
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return g;
        }

        static TextMeshPro Text3D(Transform parent, string n, string txt, float size, Vector3 pos, Vector2 box)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false); go.transform.localPosition = pos;
            var t = go.AddComponent<TextMeshPro>();
            t.text = txt; t.fontSize = size; t.alignment = TextAlignmentOptions.Center; t.color = Color.white;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = box;
            return t;
        }

        static RectTransform Anchor(GameObject go, Vector2 min, Vector2 max)
        {
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = min; r.anchorMax = max; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            return r;
        }

        static GameObject UIObj(string n, Transform parent)
        {
            var go = new GameObject(n, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static Image Panel(Transform parent, string n, Vector2 min, Vector2 max, Color c)
        {
            var go = UIObj(n, parent); Anchor(go, min, max);
            var img = go.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
            return img;
        }

        static TextMeshProUGUI Label(Transform parent, string n, string txt, float size, Color c, TextAlignmentOptions a, Vector2 min, Vector2 max, bool bold = false, bool wrap = false)
        {
            var go = UIObj(n, parent); Anchor(go, min, max);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = txt; t.fontSize = size; t.color = c; t.alignment = a; t.raycastTarget = false;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        static Button MakeButton(Transform parent, string n, string txt, Color bg, Vector2 min, Vector2 max, float size)
        {
            var go = UIObj(n, parent); Anchor(go, min, max);
            var img = go.AddComponent<Image>(); img.color = Color.white;
            var b = go.AddComponent<Button>(); b.targetGraphic = img;
            var cb = b.colors;
            cb.normalColor = bg; cb.highlightedColor = Color.Lerp(bg, Color.white, 0.35f); cb.pressedColor = Color.Lerp(bg, Color.black, 0.35f);
            cb.selectedColor = bg; cb.disabledColor = new Color(bg.r * 0.35f, bg.g * 0.35f, bg.b * 0.35f, 0.7f);
            cb.colorMultiplier = 1f; cb.fadeDuration = 0.08f; b.colors = cb;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            Label(go.transform, "Label", txt, size, Color.white, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, true);
            return b;
        }

        static Canvas MakeWorldCanvas(GameObject root, float w, float h, float scale)
        {
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h); rt.localScale = Vector3.one * scale;
            root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
            root.AddComponent<GraphicRaycaster>();
            Panel(root.transform, "Border", Vector2.zero, Vector2.one, Cyan);
            var inner = Panel(root.transform, "Background", Vector2.zero, Vector2.one, Navy);
            inner.rectTransform.offsetMin = new Vector2(4, 4); inner.rectTransform.offsetMax = new Vector2(-4, -4);
            return canvas;
        }

        // ------------------------------------------------------------------ prefabs
        static GameObject SavePrefab(GameObject go, string path)
        {
            var p = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return p;
        }

        static void Prefab(string path, Func<GameObject> make)
        {
            if (!overwrite && File.Exists(path)) return;
            SavePrefab(make(), path);
        }

        static void BuildPrefabs()
        {
            Prefab(Prefabs + "Network/Node_Attacker.prefab", () => NodeObject("Attacker", "ATTACKER", "IDLE", PrimitiveType.Cube, new Vector3(0.55f, 0.55f, 0.55f), new Vector3(0.4f, 0f, 0f), true));
            Prefab(Prefabs + "Network/Node_Router.prefab", () => NodeObject("Router", "ROUTER", "ONLINE", PrimitiveType.Cylinder, new Vector3(0.7f, 0.12f, 0.7f), Vector3.zero, false));
            Prefab(Prefabs + "Network/Node_Server.prefab", () => NodeObject("Server", "PROTECTED SERVER", "ONLINE", PrimitiveType.Cube, new Vector3(0.45f, 0.8f, 0.4f), new Vector3(-0.5f, 0f, 0f), false));
            Prefab(Prefabs + "Attacks/AttackPacket.prefab", () => Prim(PrimitiveType.Sphere, "AttackPacket", null, Vector3.zero, Vector3.one * 0.14f, "GlowOrange"));
            Prefab(Prefabs + "Defense/DefenseShield.prefab", () => Prim(PrimitiveType.Sphere, "DefenseShield", null, Vector3.zero, Vector3.one * 2.2f, "Shield"));
            Prefab(Prefabs + "Player/XROrigin_Rig.prefab", BuildRigObject);
            Prefab(Prefabs + "UI/Dashboard.prefab", BuildDashboardObject);
        }

        static GameObject NodeObject(string key, string title, string idle, PrimitiveType bodyType, Vector3 bodyScale, Vector3 anchorOffset, bool diamond)
        {
            var root = new GameObject("Node_" + key);
            var node = root.AddComponent<NetworkNode>();
            node.displayName = title; node.idleWord = "ONLINE";

            var body = Prim(bodyType, "Body", root.transform, Vector3.zero, bodyScale, "Graphite");
            if (diamond) body.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
            var ring = Prim(PrimitiveType.Cylinder, "GlowRing", root.transform, new Vector3(0f, -0.45f, 0f), new Vector3(0.9f, 0.012f, 0.9f), "NodeGlow");
            var core = Prim(PrimitiveType.Sphere, "GlowCore", root.transform, Vector3.zero, bodyScale * 0.4f + Vector3.one * 0.12f, "NodeGlow");
            if (key == "Server")
                for (int i = 0; i < 3; i++)
                    Prim(PrimitiveType.Cube, "Led" + i, root.transform, new Vector3(-0.001f - 0.226f, -0.25f + i * 0.25f, 0f), new Vector3(0.01f, 0.05f, 0.3f), "NodeGlow")
                        .transform.SetParent(root.transform, false);
            if (key == "Router") Prim(PrimitiveType.Cylinder, "Antenna", root.transform, new Vector3(0f, 0.2f, 0f), new Vector3(0.03f, 0.2f, 0.03f), "Metal");

            var rends = new System.Collections.Generic.List<Renderer> { ring.GetComponent<Renderer>(), core.GetComponent<Renderer>() };
            foreach (Transform c in root.transform) if (c.name.StartsWith("Led")) rends.Add(c.GetComponent<Renderer>());
            node.glowRenderers = rends.ToArray();

            var label = Text3D(root.transform, "Label", title, 2.2f, new Vector3(0f, 0.85f, 0f), new Vector2(3f, 1f));
            label.gameObject.AddComponent<Billboard>();
            node.label = label;
            var anchor = new GameObject("Anchor"); anchor.transform.SetParent(root.transform, false); anchor.transform.localPosition = anchorOffset;
            node.anchor = anchor.transform;
            var am = core.AddComponent<AmbientMotion>(); am.spinDegreesPerSecond = 0f; am.bobAmplitude = 0.03f;
            return root;
        }

        static GameObject BuildRigObject()
        {
            var root = new GameObject("XR Rig (CyberDefenseVR)");
            var offset = new GameObject("Camera Offset"); offset.transform.SetParent(root.transform, false);
            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera"; camGo.transform.SetParent(offset.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.015f, 0.025f, 0.05f);
            camGo.AddComponent<AudioListener>();

            var driver = root.AddComponent<VRRigDriver>();
            driver.head = camGo.transform;
            driver.leftGrip = MakeGrip("LeftHand Grip", offset.transform); driver.rightGrip = MakeGrip("RightHand Grip", offset.transform);
            driver.leftAim = MakeAim("LeftHand Aim", offset.transform, Hand.Left); driver.rightAim = MakeAim("RightHand Aim", offset.transform, Hand.Right);
            return root;
        }

        static Transform MakeGrip(string n, Transform parent)
        {
            var g = new GameObject(n); g.transform.SetParent(parent, false);
            var body = Prim(PrimitiveType.Capsule, "ControllerBody", g.transform, new Vector3(0f, 0f, 0.03f), new Vector3(0.035f, 0.06f, 0.035f), "Controller");
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Sphere, "ControllerRing", g.transform, new Vector3(0f, 0.01f, 0.08f), new Vector3(0.05f, 0.015f, 0.05f), "GlowCyan");
            g.SetActive(false);
            return g.transform;
        }

        static Transform MakeAim(string n, Transform parent, Hand hand)
        {
            var g = new GameObject(n); g.transform.SetParent(parent, false);
            var line = g.AddComponent<LineRenderer>();
            line.sharedMaterial = M("PointerLine"); line.widthMultiplier = 0.006f; line.positionCount = 2; line.useWorldSpace = true;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false; line.enabled = false;
            var reticle = Prim(PrimitiveType.Sphere, "Reticle", parent, Vector3.zero, Vector3.one * 0.025f, "PointerLine");
            reticle.SetActive(false);
            var p = g.AddComponent<VRUIPointer>(); p.hand = hand; p.line = line; p.reticle = reticle.transform;
            return g.transform;
        }

        static GameObject BuildDashboardObject()
        {
            var root = new GameObject("Dashboard", typeof(RectTransform));
            var canvas = MakeWorldCanvas(root, 1200f, 720f, 0.001f);
            var dc = root.AddComponent<DashboardController>();
            dc.canvas = canvas;

            Label(root.transform, "Title", "CYBER DEFENSE OPS CENTER", 42, Color.white, TextAlignmentOptions.MidlineLeft, new Vector2(0.04f, 0.86f), new Vector2(0.6f, 0.97f), true);
            Label(root.transform, "Scenario", "Scenario: Suspicious traffic detected", 26, Cyan, TextAlignmentOptions.MidlineRight, new Vector2(0.5f, 0.86f), new Vector2(0.96f, 0.97f));
            Panel(root.transform, "Divider", new Vector2(0.03f, 0.855f), new Vector2(0.97f, 0.858f), Cyan);

            // status rows
            var left = Panel(root.transform, "StatusPanel", new Vector2(0.03f, 0.27f), new Vector2(0.55f, 0.83f), new Color(1f, 1f, 1f, 0.05f));
            string[] heads = { "NETWORK STATUS", "ACTIVE THREAT", "ATTACK STAGE", "DEFENSE" };
            TMP_Text[] values = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
            {
                float top = 1f - i * 0.25f;
                Label(left.transform, "Head" + i, heads[i], 20, Muted, TextAlignmentOptions.BottomLeft, new Vector2(0.04f, top - 0.12f), new Vector2(0.96f, top - 0.01f));
                values[i] = Label(left.transform, "Value" + i, "-", 32, Color.white, TextAlignmentOptions.TopLeft, new Vector2(0.04f, top - 0.24f), new Vector2(0.96f, top - 0.11f), true);
            }
            dc.networkStatusValue = values[0]; dc.threatValue = values[1]; dc.stageValue = values[2]; dc.defenseValue = values[3];

            // coach panel
            var right = Panel(root.transform, "CoachPanel", new Vector2(0.58f, 0.27f), new Vector2(0.97f, 0.83f), new Color(0.1f, 0.85f, 1f, 0.08f));
            Label(right.transform, "CoachTitle", "ANALYST BRIEFING", 22, Cyan, TextAlignmentOptions.TopLeft, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f), true);
            dc.coachBody = Label(right.transform, "CoachBody", "", 25, Color.white, TextAlignmentOptions.TopLeft, new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.87f), false, true);

            // buttons
            dc.startButton = MakeButton(root.transform, "StartAttackButton", "START ATTACK", Orange, new Vector2(0.03f, 0.04f), new Vector2(0.26f, 0.22f), 28);
            dc.defenseButton = MakeButton(root.transform, "ActivateDefenseButton", "ACTIVATE DEFENSE", Teal, new Vector2(0.275f, 0.04f), new Vector2(0.505f, 0.22f), 28);
            dc.resetButton = MakeButton(root.transform, "ResetButton", "RESET", Slate, new Vector2(0.52f, 0.04f), new Vector2(0.745f, 0.22f), 28);
            dc.menuButton = MakeButton(root.transform, "MainMenuButton", "MAIN MENU", Slate, new Vector2(0.76f, 0.04f), new Vector2(0.97f, 0.22f), 28);
            return root;
        }

        // ------------------------------------------------------------------ scenes
        static void SetupLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.1f, 0.13f, 0.2f);
            RenderSettings.skybox = null;
            var d = new GameObject("Directional Light"); var l = d.AddComponent<Light>();
            l.type = LightType.Directional; l.color = new Color(0.6f, 0.7f, 1f); l.intensity = 0.35f; l.shadows = LightShadows.None;
            d.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        static Light PointLight(string n, Vector3 pos, Color c, float intensity, float range)
        {
            var g = new GameObject(n); g.transform.position = pos;
            var l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            return l;
        }

        static void AddEventSystem()
        {
            // Never create a second EventSystem / input module in a scene.
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;

            var es = new GameObject("EventSystem", typeof(EventSystem));
            var mod = es.AddComponent<InputSystemUIInputModule>();
            var r = UiReferences();            // persistent sub-assets of CyberDefenseUI.inputactions
            mod.actionsAsset = uiActions;
            mod.point = r["Point"];
            mod.leftClick = r["Click"];
            mod.rightClick = r["RightClick"];
            mod.middleClick = r["MiddleClick"];
            mod.scrollWheel = r["ScrollWheel"];
            mod.move = r["Navigate"];
            mod.submit = r["Submit"];
            mod.cancel = r["Cancel"];
            mod.trackedDevicePosition = r["TrackedDevicePosition"];
            mod.trackedDeviceOrientation = r["TrackedDeviceOrientation"];
        }

        static void BuildMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLighting();
            PointLight("Fill Light", new Vector3(0f, 3f, 2f), new Color(0.3f, 0.7f, 1f), 1.5f, 10f);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Player/XROrigin_Rig.prefab"));
            rig.transform.position = Vector3.zero;

            // ambient network backdrop
            var bg = new GameObject("NetworkBackdrop").transform;
            var rng = new System.Random(7);
            var pts = new Transform[12];
            for (int i = 0; i < pts.Length; i++)
            {
                float ang = (-70f + i * (140f / (pts.Length - 1))) * Mathf.Deg2Rad;
                float r = 6f + (float)rng.NextDouble() * 3f;
                var pos = new Vector3(Mathf.Sin(ang) * r, 0.8f + (float)rng.NextDouble() * 3f, Mathf.Cos(ang) * r);
                var n = Prim(PrimitiveType.Sphere, "Node" + i, bg, pos, Vector3.one * (0.18f + (float)rng.NextDouble() * 0.15f), "MenuNode");
                var am = n.AddComponent<AmbientMotion>(); am.bobAmplitude = 0.12f; am.spinDegreesPerSecond = 0f; am.bobSpeed = 0.5f + (float)rng.NextDouble() * 0.5f;
                pts[i] = n.transform;
            }
            for (int i = 0; i < pts.Length - 1; i++) MakeLine("Link" + i, bg, pts[i], pts[i + 1]);
            for (int i = 0; i < pts.Length - 3; i += 2) MakeLine("Cross" + i, bg, pts[i], pts[i + 3]);
            Prim(PrimitiveType.Cube, "Floor", bg, new Vector3(0f, -0.05f, 4f), new Vector3(24f, 0.1f, 24f), "Floor");

            // menu canvas
            var root = new GameObject("MenuCanvas", typeof(RectTransform));
            root.transform.position = new Vector3(0f, 1.5f, 2.2f);
            var canvas = MakeWorldCanvas(root, 1000f, 900f, 0.0012f);
            var mc = root.AddComponent<MainMenuController>(); mc.canvas = canvas;

            var main = UIObj("MainPanel", root.transform); Anchor(main, Vector2.zero, Vector2.one); mc.mainPanel = main;
            Label(main.transform, "Title", "CYBER DEFENSE VR", 84, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 0.76f), new Vector2(1f, 0.94f), true);
            Label(main.transform, "Subtitle", "Interactive Cybersecurity Training", 36, Cyan, TextAlignmentOptions.Center, new Vector2(0f, 0.68f), new Vector2(1f, 0.77f));
            string[] names = { "START TRAINING", "DESKTOP TEST", "SETTINGS", "EXIT" };
            Color[] cols = { Teal, Slate, Slate, Slate };
            Button[] btns = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                float max = 0.62f - i * 0.14f;
                btns[i] = MakeButton(main.transform, names[i].Replace(" ", "") + "Button", names[i], cols[i], new Vector2(0.2f, max - 0.11f), new Vector2(0.8f, max), 40);
            }
            mc.startButton = btns[0]; mc.desktopButton = btns[1]; mc.settingsButton = btns[2]; mc.exitButton = btns[3];
            Label(main.transform, "Footer", "Educational simulation. All attacks are fictional and visual only.", 22, Muted, TextAlignmentOptions.Center, new Vector2(0f, 0.01f), new Vector2(1f, 0.07f));

            var set = UIObj("SettingsPanel", root.transform); Anchor(set, Vector2.zero, Vector2.one); mc.settingsPanel = set;
            Label(set.transform, "Title", "SETTINGS", 64, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 0.82f), new Vector2(1f, 0.95f), true);
            string[] rows = { "Master Volume", "Sound Effects", "Music", "Dominant Hand" };
            for (int i = 0; i < 4; i++)
            {
                float max = 0.78f - i * 0.15f, min = max - 0.11f;
                Label(set.transform, "RowLabel" + i, rows[i], 34, Color.white, TextAlignmentOptions.MidlineLeft, new Vector2(0.08f, min), new Vector2(0.48f, max));
                if (i < 3)
                {
                    var minus = MakeButton(set.transform, "Minus" + i, "-", Slate, new Vector2(0.52f, min), new Vector2(0.62f, max), 44);
                    var val = Label(set.transform, "Value" + i, "80%", 36, Cyan, TextAlignmentOptions.Center, new Vector2(0.63f, min), new Vector2(0.77f, max), true);
                    var plus = MakeButton(set.transform, "Plus" + i, "+", Slate, new Vector2(0.78f, min), new Vector2(0.88f, max), 44);
                    if (i == 0) { mc.masterMinus = minus; mc.masterPlus = plus; mc.masterValue = val; }
                    else if (i == 1) { mc.sfxMinus = minus; mc.sfxPlus = plus; mc.sfxValue = val; }
                    else { mc.musicMinus = minus; mc.musicPlus = plus; mc.musicValue = val; }
                }
                else
                {
                    mc.handButton = MakeButton(set.transform, "HandButton", "RIGHT", Teal, new Vector2(0.52f, min), new Vector2(0.88f, max), 36);
                    mc.handValue = mc.handButton.GetComponentInChildren<TMP_Text>();
                }
            }
            mc.backButton = MakeButton(set.transform, "BackButton", "BACK", Slate, new Vector2(0.25f, 0.06f), new Vector2(0.75f, 0.17f), 38);

            AddEventSystem();
            EditorSceneManager.SaveScene(scene, MenuScene);
        }

        static void MakeLine(string n, Transform parent, Transform a, Transform b)
        {
            var g = new GameObject(n); g.transform.SetParent(parent, false);
            var lr = g.AddComponent<LineRenderer>();
            lr.sharedMaterial = M("LineUnlit"); lr.widthMultiplier = 0.015f; lr.positionCount = 2;
            lr.shadowCastingMode = ShadowCastingMode.Off; lr.receiveShadows = false;
            var nc = g.AddComponent<NetworkConnection>(); nc.from = a; nc.to = b; nc.Refresh();
        }

        static void BuildRoom(Transform env)
        {
            Prim(PrimitiveType.Cube, "Floor", env, new Vector3(0f, -0.05f, 4f), new Vector3(16f, 0.1f, 14f), "Floor");
            Prim(PrimitiveType.Cube, "Ceiling", env, new Vector3(0f, 4.25f, 4f), new Vector3(16f, 0.1f, 14f), "Wall");
            Prim(PrimitiveType.Cube, "BackWall", env, new Vector3(0f, 2.1f, 11f), new Vector3(16f, 4.2f, 0.2f), "Wall");
            Prim(PrimitiveType.Cube, "FrontWall", env, new Vector3(0f, 2.1f, -3f), new Vector3(16f, 4.2f, 0.2f), "Wall");
            Prim(PrimitiveType.Cube, "LeftWall", env, new Vector3(-8f, 2.1f, 4f), new Vector3(0.2f, 4.2f, 14f), "Wall");
            Prim(PrimitiveType.Cube, "RightWall", env, new Vector3(8f, 2.1f, 4f), new Vector3(0.2f, 4.2f, 14f), "Wall");

            // floor guide lines + wall trim
            for (int i = 0; i < 7; i++)
                Prim(PrimitiveType.Cube, "FloorLine" + i, env, new Vector3(-6f + i * 2f, 0.005f, 4f), new Vector3(0.03f, 0.01f, 13f), "GlowBlue");
            Prim(PrimitiveType.Cube, "TrimBack", env, new Vector3(0f, 0.1f, 10.88f), new Vector3(15.6f, 0.06f, 0.04f), "GlowCyan");
            Prim(PrimitiveType.Cube, "TrimLeft", env, new Vector3(-7.88f, 0.1f, 4f), new Vector3(0.04f, 0.06f, 13.6f), "GlowCyan");
            Prim(PrimitiveType.Cube, "TrimRight", env, new Vector3(7.88f, 0.1f, 4f), new Vector3(0.04f, 0.06f, 13.6f), "GlowCyan");
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cube, "CeilingBar" + i, env, new Vector3(-4.5f + i * 3f, 4.15f, 4f), new Vector3(0.25f, 0.04f, 10f), "GlowCyan");

            // server racks along side walls
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 4; i++)
                {
                    float z = 3f + i * 1.7f; float x = side * 7.3f;
                    Prim(PrimitiveType.Cube, "Rack", env, new Vector3(x, 1.1f, z), new Vector3(0.7f, 2.2f, 0.9f), "Metal");
                    Prim(PrimitiveType.Cube, "RackLed", env, new Vector3(x - side * 0.36f, 1.1f + (i % 2) * 0.3f, z), new Vector3(0.02f, 0.9f, 0.04f), "GlowCyan");
                }

            // control desk (under dashboard)
            Prim(PrimitiveType.Cube, "ConsoleDesk", env, new Vector3(0f, 0.32f, 1.45f), new Vector3(1.6f, 0.64f, 0.6f), "Metal");
            Prim(PrimitiveType.Cube, "ConsoleEdge", env, new Vector3(0f, 0.645f, 1.15f), new Vector3(1.6f, 0.012f, 0.02f), "GlowCyan");

            // holographic platform
            Prim(PrimitiveType.Cylinder, "PlatformColumn", env, new Vector3(0f, 0.6f, 4.6f), new Vector3(0.7f, 0.6f, 0.7f), "Metal");
            Prim(PrimitiveType.Cylinder, "HoloDisk", env, new Vector3(0f, 1.2f, 4.6f), new Vector3(5.6f, 0.02f, 5.6f), "HoloDisk");
            Prim(PrimitiveType.Cylinder, "HoloRim", env, new Vector3(0f, 1.19f, 4.6f), new Vector3(5.7f, 0.015f, 5.7f), "GlowCyan");
        }

        static void BuildTrainingScene(bool xr, string scenePath)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLighting();
            PointLight("Room Light", new Vector3(0f, 3.6f, 4.6f), new Color(0.3f, 0.7f, 1f), 2.2f, 10f);
            PointLight("Console Light", new Vector3(0f, 2.4f, 1.0f), new Color(0.5f, 0.8f, 1f), 1f, 5f);

            var env = new GameObject("Environment").transform;
            BuildRoom(env);

            // player
            if (xr)
            {
                var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Player/XROrigin_Rig.prefab"));
                rig.transform.position = Vector3.zero;
            }
            else
            {
                var camGo = new GameObject("Desktop Camera"); camGo.tag = "MainCamera";
                camGo.transform.position = new Vector3(0f, 1.6f, 0f);
                var cam = camGo.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.015f, 0.025f, 0.05f);
                cam.nearClipPlane = 0.05f; cam.farClipPlane = 60f;
                camGo.AddComponent<AudioListener>(); camGo.AddComponent<DesktopCameraController>();
            }

            // network
            var net = new GameObject("Network").transform;
            var attacker = Inst<NetworkNode>("Network/Node_Attacker", net, new Vector3(-2f, 1.9f, 4.2f));
            var router = Inst<NetworkNode>("Network/Node_Router", net, new Vector3(0f, 2.0f, 5.4f));
            var server = Inst<NetworkNode>("Network/Node_Server", net, new Vector3(2f, 1.9f, 4.2f));
            foreach (var n in new[] { attacker, router, server })
                Prim(PrimitiveType.Cylinder, n.name + "_Beam", net, new Vector3(n.transform.position.x, 1.55f, n.transform.position.z), new Vector3(0.05f, 0.35f, 0.05f), "Beam");
            var c1 = Conn("Conn_AttackerRouter", net, attacker.anchor, router.anchor);
            var c2 = Conn("Conn_RouterServer", net, router.anchor, server.anchor);

            // attack
            var atkGo = new GameObject("AttackSystem");
            var attack = atkGo.AddComponent<AttackController>();
            attack.attackerAnchor = attacker.anchor; attack.routerAnchor = router.anchor; attack.serverAnchor = server.anchor;
            attack.packets = new Transform[4];
            for (int i = 0; i < 4; i++)
                attack.packets[i] = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Attacks/AttackPacket.prefab"), atkGo.transform)).transform;

            // defense
            var defGo = new GameObject("DefenseSystem");
            var defense = defGo.AddComponent<DefenseController>();
            var sh = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Defense/DefenseShield.prefab"), defGo.transform);
            sh.transform.position = router.transform.position;
            defense.shield = sh.transform;

            // threat beacon on back wall
            var beaconRoot = new GameObject("ThreatBeacon"); beaconRoot.transform.position = new Vector3(0f, 3.2f, 10.8f);
            var panel = Prim(PrimitiveType.Cube, "Panel", beaconRoot.transform, Vector3.zero, new Vector3(5.2f, 1.1f, 0.06f), "BeaconPanel");
            var txt = Text3D(beaconRoot.transform, "Text", "NETWORK SECURE", 5f, new Vector3(0f, 0f, -0.05f), new Vector2(5f, 1f));
            txt.color = Color.white; txt.fontStyle = FontStyles.Bold;
            var ti = beaconRoot.AddComponent<ThreatIndicator>(); ti.panel = panel.GetComponent<Renderer>(); ti.text = txt;
            Text3D(beaconRoot.transform, "Caption", "THREAT LEVEL", 2f, new Vector3(0f, 0.8f, -0.05f), new Vector2(4f, 0.5f)).color = new Color(0.62f, 0.7f, 0.8f);

            // simulation
            var gm = new GameObject("GameManager");
            var sim = gm.AddComponent<SimulationController>();
            sim.attack = attack; sim.defense = defense; sim.attacker = attacker; sim.router = router; sim.server = server;
            sim.attackerToRouter = c1; sim.routerToServer = c2; sim.threatIndicator = ti;

            // dashboard (tilted console)
            var dashParent = new GameObject("DashboardAnchor");
            dashParent.transform.position = new Vector3(0f, 1.08f, 1.3f);
            dashParent.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
            var dash = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "UI/Dashboard.prefab"), dashParent.transform);
            dash.transform.localPosition = Vector3.zero; dash.transform.localRotation = Quaternion.identity;
            dash.GetComponent<DashboardController>().simulation = sim;
            Prim(PrimitiveType.Cube, "Bezel", dashParent.transform, new Vector3(0f, 0f, 0.015f), new Vector3(1.26f, 0.78f, 0.02f), "Metal");

            // labels on environment
            var rl = Text3D(env, "HoloLabel", "NETWORK TOPOLOGY", 2.2f, new Vector3(0f, 1.3f, 2.3f), new Vector2(4f, 0.6f));
            rl.color = new Color(0.6f, 0.9f, 1f); rl.gameObject.AddComponent<Billboard>();

            AddEventSystem();

            if (!xr)
            {
                var st = new GameObject("SelfTest").AddComponent<SimulationSelfTest>();
                st.simulation = sim; st.runOnStart = false;
            }
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        static T Inst<T>(string prefabRel, Transform parent, Vector3 pos) where T : Component
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + prefabRel + ".prefab"), parent);
            go.transform.position = pos;
            return go.GetComponent<T>();
        }

        static NetworkConnection Conn(string n, Transform parent, Transform a, Transform b)
        {
            var g = new GameObject(n); g.transform.SetParent(parent, false);
            var lr = g.AddComponent<LineRenderer>();
            lr.sharedMaterial = M("LineUnlit"); lr.widthMultiplier = 0.03f; lr.positionCount = 2;
            lr.shadowCastingMode = ShadowCastingMode.Off; lr.receiveShadows = false;
            var nc = g.AddComponent<NetworkConnection>(); nc.from = a; nc.to = b; nc.Refresh();
            return nc;
        }
    }

    [InitializeOnLoad]
    static class AutoGenerateOnFirstOpen
    {
        static readonly double start = EditorApplication.timeSinceStartup;
        static bool done;

        static AutoGenerateOnFirstOpen()
        {
            if (Application.isBatchMode) return;
            EditorApplication.update += Poll;
        }

        static void Finish() { done = true; EditorApplication.update -= Poll; }

        static void Poll()
        {
            if (done) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (CyberDefenseProjectBuilder.AllScenesExist()) { Finish(); return; }
            if (Shader.Find("Universal Render Pipeline/Lit") == null)
            {
                if (EditorApplication.timeSinceStartup - start > 180)
                {
                    Finish();
                    Debug.LogWarning("[CyberDefenseVR] URP shaders never became available. Check the Package Manager for errors, then run CyberDefense > Generate Project Assets.");
                }
                return;
            }
            Finish();
            if (EditorUtility.DisplayDialog("Cyber Defense VR",
                "Some game scenes have not been generated yet.\n\nGenerate the missing scenes, prefabs and materials now? Existing assets are kept. (about a minute)",
                "Generate", "Later (use CyberDefense menu)"))
                CyberDefenseProjectBuilder.GenerateMenu();
        }
    }
}
