using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace CyberDefenseVR.EditorTools
{
    /// <summary>
    /// Static validation of the generated scenes: existence, missing scripts, null serialized references on project components,
    /// camera / EventSystem counts, error shaders and build settings. Writes ValidationReport.txt in the project root.
    /// Batch: -executeMethod CyberDefenseVR.EditorTools.ProjectValidator.RunBatch
    /// </summary>
    public static class ProjectValidator
    {
        static readonly string[] Scenes = { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/CyberDefenseTraining.unity", "Assets/Scenes/DesktopTest.unity" };

        [MenuItem("CyberDefense/Validate Project")]
        public static void RunMenu() { Run(); }

        public static void RunBatch() { int fails = Run(); EditorApplication.Exit(fails == 0 ? 0 : 1); }

        public static int Run()
        {
            var sb = new StringBuilder(); int fails = 0;
            Action<bool, string> check = (ok, msg) => { if (!ok) fails++; sb.AppendLine((ok ? "PASS  " : "FAIL  ") + msg); };

            var inBuild = new HashSet<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) inBuild.Add(s.path);

            foreach (var path in Scenes)
            {
                bool exists = File.Exists(path);
                check(exists, "Scene exists: " + path);
                check(inBuild.Contains(path), "Scene in Build Settings: " + path);
                if (!exists) continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                ValidateScene(scene, check);
            }
            check(PlayerSettingsOk(), "Android: IL2CPP + ARM64 + minSdk >= 29");
            check(UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null, "URP asset assigned as default render pipeline");

            File.WriteAllText("ValidationReport.txt", sb.ToString());
            Debug.Log("[CyberDefenseVR] Validation " + (fails == 0 ? "PASSED" : "FAILED (" + fails + ")") + "\n" + sb);
            return fails;
        }

        static bool PlayerSettingsOk()
        {
            return PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android) == ScriptingImplementation.IL2CPP
                && (PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) != 0
                && (int)PlayerSettings.Android.minSdkVersion >= 29;
        }

        static void ValidateScene(Scene scene, Action<bool, string> check)
        {
            string n = scene.name;
            int missing = 0, nullRefs = 0, badMats = 0;
            var nullList = new StringBuilder();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    var r = t.GetComponent<Renderer>();
                    if (r != null)
                        foreach (var m in r.sharedMaterials)
                            if (m == null || m.shader == null || m.shader.name.Contains("InternalErrorShader")) badMats++;
                }
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    var type = mb.GetType();
                    if (type.Namespace != "CyberDefenseVR") continue;
                    foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (!typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType) && !f.FieldType.IsArray) continue;
                        var v = f.GetValue(mb);
                        bool isNull = v == null || (v is UnityEngine.Object && (UnityEngine.Object)v == null);
                        if (!isNull && v is Array)
                            foreach (var e in (Array)v) if (e == null || (e is UnityEngine.Object && (UnityEngine.Object)e == null)) isNull = true;
                        // VRUIPointer.line/reticle and optional fields are always assigned by the generator, so any null is a defect.
                        if (isNull) { nullRefs++; nullList.Append(" " + type.Name + "." + f.Name); }
                    }
                }
            }
            check(missing == 0, n + ": no missing scripts (" + missing + ")");
            check(nullRefs == 0, n + ": no null serialized references (" + nullRefs + ")" + nullList);
            check(badMats == 0, n + ": no missing/error materials (" + badMats + ")");
            check(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1, n + ": exactly one camera");
            check(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1, n + ": exactly one EventSystem");
            var modules = UnityEngine.Object.FindObjectsByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool uiOk = modules.Length == 1 && modules[0].actionsAsset != null && modules[0].point != null && modules[0].leftClick != null
                && modules[0].move != null && modules[0].submit != null && modules[0].cancel != null
                && modules[0].trackedDevicePosition != null && modules[0].trackedDeviceOrientation != null;
            check(uiOk, n + ": InputSystemUIInputModule has an InputActionAsset and all UI action references");
            check(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1, n + ": exactly one AudioListener");
        }
    }
}
