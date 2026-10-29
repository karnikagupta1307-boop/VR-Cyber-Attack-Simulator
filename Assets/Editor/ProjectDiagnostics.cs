using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CyberDefenseVR.EditorTools
{
    /// <summary>CyberDefense > Diagnose Project: explains why a project may look empty.</summary>
    public static class ProjectDiagnostics
    {
        [MenuItem("CyberDefense/Diagnose Project")]
        public static void Run()
        {
            var sb = new StringBuilder();
            string root = Path.GetDirectoryName(Application.dataPath);
            sb.AppendLine("Project root opened in Unity: " + root);

            // Wrong-folder check: a child folder that itself looks like a Unity project.
            foreach (var dir in Directory.GetDirectories(root))
                if (Directory.Exists(Path.Combine(dir, "Assets")) && Directory.Exists(Path.Combine(dir, "Packages")))
                    sb.AppendLine("WARNING: '" + dir + "' looks like the REAL project. You opened the outer folder. Close Unity and open that inner folder in Unity Hub.");

            string[] scenes = { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/CyberDefenseTraining.unity", "Assets/Scenes/DesktopTest.unity" };
            foreach (var s in scenes) sb.AppendLine((File.Exists(s) ? "OK       " : "MISSING  ") + s + (File.Exists(s) ? "" : "   -> run CyberDefense > Generate Project Assets"));
            sb.AppendLine("Scripts found: " + AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Scripts" }).Length + " (expected 18)");
            sb.AppendLine("URP shader available: " + (Shader.Find("Universal Render Pipeline/Lit") != null));
            sb.AppendLine("TMP Essentials imported: " + File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"));
            sb.AppendLine("Scenes in Build Settings: " + EditorBuildSettings.scenes.Length);
            sb.AppendLine("Compiling now: " + EditorApplication.isCompiling + "  (if the Console shows red compile errors, the generator cannot run until they are fixed)");
            string lower = root.ToLowerInvariant();
            foreach (var cloud in new[] { "onedrive", "dropbox", "icloud", "google drive", "googledrive" })
                if (lower.Contains(cloud)) sb.AppendLine("WARNING: project path is inside a cloud-synced folder (" + cloud + "). Sync clients lock files and are a known cause of AssetImportWorker crashes. Move the project to a plain local folder such as C:\\Dev\\CyberDefenseVR.");
            foreach (var ch in root) if (ch > 127) { sb.AppendLine("WARNING: project path contains non-ASCII characters; prefer a plain ASCII path."); break; }
            sb.AppendLine("Input actions asset: " + (File.Exists("Assets/Input/CyberDefenseUI.inputactions") ? "present" : "missing (created by the generator)"));
            foreach (var n in new[] { "com.unity.xr.management", "com.unity.xr.openxr", "com.unity.xr.meta-openxr", "com.unity.inputsystem" })
            {
                bool found = false;
                foreach (var pkg in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()) if (pkg.name == n) { found = true; sb.AppendLine("Package " + n + " " + pkg.version); }
                if (!found) sb.AppendLine("Package " + n + ": NOT installed");
            }
            sb.AppendLine("Logs to inspect for import-worker crashes: the Editor.log (Console window menu > Open Editor Log) and Library/Logs/AssetImportWorker*.log in the project folder.");
            Debug.Log("[CyberDefenseVR] Diagnosis\n" + sb);
        }
    }
}
