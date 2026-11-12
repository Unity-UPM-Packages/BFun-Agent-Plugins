// BFun Agent Bridge, shipped with the ads-integration skill.
//
// The agent copies this file and BFun.AgentTools.Editor.asmdef into
// <project>/Assets/Editor/BFunAgent/ and removes that folder when the integration is done.
//
// Protocol (file based, so any coding agent can drive it without a Unity CLI or MCP server):
//   1. The agent writes   Library/BFunAgent/command.json   e.g. {"id":"3","name":"setup-ads-settings"}
//   2. This script runs the command on the Editor main thread, deletes command.json, and
//   3. writes             Library/BFunAgent/status.json    (result of the last command + Editor facts).
//   status.json is also rewritten every ~2 s as a heartbeat ("updatedAt").
//
// Everything that touches Ads Manager goes through reflection, so this file compiles before
// Ads Manager is installed. It does not modify any package; it only does what a person would do
// by clicking the TripSoft menus, the AdsSettings inspector and its SAVE button.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEditor.Events;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace BFun.AgentTools.Editor
{
    [InitializeOnLoad]
    internal static class BFunAdsAgentBridge
    {
        private const string Dir = "Library/BFunAgent";
        private const string CommandFile = Dir + "/command.json";
        private const string StatusFile = Dir + "/status.json";

        private const string AdsSettingsPath = "Assets/TripSoft/AdsManager/Resources/AdsSettingsAsset.asset";
        private const string AppsFlyerSettingsPath = "Assets/TripSoft/AppsFlyer/Resources/AppsFlyerSettingsAsset.asset";
        private const string PackageSplashScene = "Packages/com.thelegends.ads.manager/Runtime/AdsManager/Scenes/AdsSplash.unity";
        private const string DefaultSplashScene = "Assets/Scenes/AdsSplash.unity";
        private const int AdmobFlag = 4; // TheLegends.Base.Ads.AdsMediation.Admob

        [Serializable]
        private class Hook
        {
            public string eventName;      // OnInitFirebaseDone | OnInitAdsDone | OnCompleteSplash
            public string gameObjectPath; // hierarchy path inside the AdsSplash scene, e.g. "Systems/RemoteConfig"
            public string component;      // component type name (short or full)
            public string method;         // public void method with no parameters
        }

        [Serializable]
        private class Command
        {
            public string id;
            public string name;
            public string scenePath;      // install-splash-scene / configure-splash (default Assets/Scenes/AdsSplash.unity)
            public string nextSceneName;  // configure-splash
            public Hook[] hooks;          // configure-splash
        }

        [Serializable]
        private class Status
        {
            public string state;          // ready | compiling | updating | compile-failed
            public string updatedAt;
            public string unityVersion;
            public bool isCompiling;
            public bool isUpdating;
            public string[] compileErrors;
            public string[] packages;
            public string[] tripSoftMenus;
            public bool adsSettingsExists;
            public bool appsFlyerSettingsExists;
            public string androidDefines;
            public string iosDefines;
            public string[] buildScenes;
            public string lastCommandId;
            public string lastCommandName;
            public bool lastOk;
            public string lastMessage;
            public string[] lastDetails;
        }

        private static readonly List<string> CompileErrors = new List<string>();
        private static Status _status;
        private static double _nextPoll;
        private static double _lastWrite;
        private static double _badCommandSince = -1;

        static BFunAdsAgentBridge()
        {
            Directory.CreateDirectory(Dir);
            _status = ReadStatus() ?? new Status(); // keep the last command result across domain reloads

            CompilationPipeline.compilationStarted += _ =>
            {
                CompileErrors.Clear();
                Write("compiling");
            };
            CompilationPipeline.assemblyCompilationFinished += (_, messages) =>
            {
                foreach (var m in messages)
                    if (m.type == CompilerMessageType.Error) CompileErrors.Add(m.message);
            };
            CompilationPipeline.compilationFinished += _ =>
            {
                if (CompileErrors.Count > 0) Write("compile-failed");
            };

            EditorApplication.update += Poll;
            EditorApplication.delayCall += () => Write(null);
        }

        // ─── Polling ────────────────────────────────────────────────────────────

        private static void Poll()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextPoll) return;
            _nextPoll = now + 0.5;

            if (File.Exists(CommandFile) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                RunCommandFile(now);
            }

            if (now - _lastWrite > 2) Write(null);
        }

        private static void RunCommandFile(double now)
        {
            Command cmd = null;
            try { cmd = JsonUtility.FromJson<Command>(File.ReadAllText(CommandFile)); }
            catch (Exception) { /* may still be being written */ }

            if (cmd == null || string.IsNullOrEmpty(cmd.name))
            {
                if (_badCommandSince < 0) _badCommandSince = now;
                if (now - _badCommandSince < 5) return;
                _badCommandSince = -1;
                TryDelete(CommandFile);
                Finish(new Command { id = "?", name = "?" }, false, "command.json is not valid JSON or has no \"name\"", null);
                return;
            }

            _badCommandSince = -1;
            TryDelete(CommandFile);

            var details = new List<string>();
            bool ok;
            string message;
            try
            {
                ok = Execute(cmd, details, out message);
            }
            catch (Exception e)
            {
                var inner = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                ok = false;
                message = inner.GetType().Name + ": " + inner.Message;
                details.Add(inner.StackTrace ?? "");
            }

            Finish(cmd, ok, message, details);
        }

        private static bool Execute(Command cmd, List<string> d, out string message)
        {
            switch (cmd.name)
            {
                case "status":
                    message = "status refreshed";
                    return true;

                case "refresh":
                    AssetDatabase.Refresh();
                    Client.Resolve();
                    message = "AssetDatabase refreshed and Package Manager resolve requested";
                    return true;

                case "setup-ads-settings":
                    return SetupAdsSettings(d, out message);

                case "create-appsflyer-settings":
                    return CreateAppsFlyerSettings(d, out message);

                case "install-splash-scene":
                    return InstallSplashScene(cmd, d, out message);

                case "configure-splash":
                    return ConfigureSplash(cmd, d, out message);

                default:
                    message = "unknown command: " + cmd.name;
                    return false;
            }
        }

        // ─── Step 4 + 5: AdsSettings, same as the TripSoft menu + inspector + SAVE ───

        private static bool SetupAdsSettings(List<string> d, out string message)
        {
            var settingsType = FindType("TheLegends.Base.Ads.AdsSettings");
            var integrationType = FindType("TheLegends.Base.Ads.PackagesManagerIntergration");
            if (settingsType == null || integrationType == null)
            {
                message = "Ads Manager is not compiled yet (AdsSettings or PackagesManagerIntergration type not found)";
                return false;
            }

            if (!File.Exists(AdsSettingsPath))
            {
                if (!EditorApplication.ExecuteMenuItem("TripSoft/Ads Settings"))
                {
                    message = "menu TripSoft/Ads Settings not found";
                    return false;
                }
                d.Add("created " + AdsSettingsPath + " via TripSoft/Ads Settings");
            }

            var asset = AssetDatabase.LoadAssetAtPath(AdsSettingsPath, settingsType);
            if (asset == null)
            {
                message = "AdsSettings asset not found at " + AdsSettingsPath;
                return false;
            }

            // Same as toggling "Google Admob" in the inspector: goes through the FlagMediations setter,
            // which also rebuilds the mediation list and showADMOB.
            var flagProp = settingsType.GetProperty("FlagMediations");
            int flags = Math.Max(0, Convert.ToInt32(flagProp.GetValue(asset)));
            flagProp.SetValue(asset, Enum.ToObject(flagProp.PropertyType, flags | AdmobFlag));

            var primaryField = settingsType.GetField("primaryMediation");
            if (Convert.ToInt32(primaryField.GetValue(asset)) <= 0)
                primaryField.SetValue(asset, Enum.ToObject(primaryField.FieldType, AdmobFlag));

            EditorUtility.SetDirty(asset);

            var so = new SerializedObject(asset);
            so.Update();
            SetBool(so, "showADMOB", true, d);
            SetBool(so, "useFirebase", true, d);
            SetBool(so, "useAppsFlyer", true, d);
            SetBool(so, "preloadSettings.nativeAds.preloadNativeBanner", true, d);
            SetBool(so, "preloadSettings.nativeAds.preloadNativeReward", true, d);
            SetBool(so, "preloadSettings.nativeAds.preloadNativeAppOpen", true, d);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            // Same as the SAVE button: syncs define symbols and adds/updates the optional packages
            // in Packages/manifest.json using the versions from Ads Manager's optionalDependencies.
            integrationType.GetMethod("UpdateManifest", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);

            var getRequired = integrationType.GetMethod("GetOptionalDependencyVersion", BindingFlags.Public | BindingFlags.Static);
            string manifest = File.ReadAllText("Packages/manifest.json");
            foreach (var pkg in new[] { "com.google.ads.mobile", "com.thelegends.firebase.manager", "com.thelegends.appsflyer.manager" })
            {
                var required = getRequired?.Invoke(null, new object[] { pkg }) as string;
                var m = Regex.Match(manifest, "\"" + Regex.Escape(pkg) + "\"\\s*:\\s*\"([^\"]+)\"");
                d.Add(pkg + ": optionalDependencies=" + (required ?? "-") + ", manifest=" + (m.Success ? m.Groups[1].Value : "MISSING"));
            }

            message = "AdsSettings configured and saved; manifest updated (Unity will now resolve and recompile)";
            return true;
        }

        private static void SetBool(SerializedObject so, string path, bool value, List<string> d)
        {
            var p = so.FindProperty(path);
            if (p == null) throw new InvalidOperationException("AdsSettings has no serialized field " + path);
            p.boolValue = value;
            d.Add(path + " = " + value);
        }

        // ─── Step 6: AppsFlyer Setting ──────────────────────────────────────────

        private static bool CreateAppsFlyerSettings(List<string> d, out string message)
        {
            if (File.Exists(AppsFlyerSettingsPath))
            {
                message = "AppsFlyer settings already exist at " + AppsFlyerSettingsPath;
                return true;
            }

            if (!EditorApplication.ExecuteMenuItem("TripSoft/AppsFlyer Setting"))
            {
                message = "menu TripSoft/AppsFlyer Setting not found (AppsFlyer Manager not installed or not compiled yet)";
                return false;
            }

            AssetDatabase.SaveAssets();
            bool ok = File.Exists(AppsFlyerSettingsPath);
            message = ok ? "created " + AppsFlyerSettingsPath : "menu ran but " + AppsFlyerSettingsPath + " was not found";
            return ok;
        }

        // ─── Step 7: copy AdsSplash and make it build index 0 ───────────────────

        private static bool InstallSplashScene(Command cmd, List<string> d, out string message)
        {
            string target = string.IsNullOrEmpty(cmd.scenePath) ? DefaultSplashScene : cmd.scenePath.Replace('\\', '/');
            if (!target.StartsWith("Assets/") || !target.EndsWith(".unity"))
            {
                message = "scenePath must be under Assets/ and end with .unity";
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PackageSplashScene) == null)
            {
                message = "package scene not found: " + PackageSplashScene;
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(target) == null)
            {
                EnsureFolder(Path.GetDirectoryName(target)?.Replace('\\', '/'));
                if (!AssetDatabase.CopyAsset(PackageSplashScene, target))
                {
                    message = "AssetDatabase.CopyAsset failed for " + target;
                    return false;
                }
                d.Add("copied " + PackageSplashScene + " -> " + target);
            }
            else
            {
                d.Add(target + " already exists, not overwritten");
            }

            var scenes = EditorBuildSettings.scenes.Where(s => s.path != target).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(target, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();

            for (int i = 0; i < scenes.Count; i++) d.Add("build[" + i + "] " + scenes[i].path + (scenes[i].enabled ? "" : " (disabled)"));
            message = target + " is build index 0";
            return true;
        }

        // ─── Step 8: next scene + optional UnityEvent hooks ─────────────────────

        private static bool ConfigureSplash(Command cmd, List<string> d, out string message)
        {
            string path = string.IsNullOrEmpty(cmd.scenePath) ? DefaultSplashScene : cmd.scenePath.Replace('\\', '/');
            var controllerType = FindType("TheLegends.Base.Ads.AdsSplashController");
            if (controllerType == null)
            {
                message = "AdsSplashController type not found";
                return false;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                message = "scene not found: " + path + " (run install-splash-scene first)";
                return false;
            }

            var scene = SceneManager.GetSceneByPath(path);
            bool openedHere = false;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                openedHere = true;
            }

            try
            {
                var controller = scene.GetRootGameObjects()
                    .SelectMany(g => g.GetComponentsInChildren(controllerType, true))
                    .FirstOrDefault();
                if (controller == null)
                {
                    message = "no AdsSplashController in " + path;
                    return false;
                }

                if (!string.IsNullOrEmpty(cmd.nextSceneName))
                {
                    if (!EnsureSceneInBuild(cmd.nextSceneName, d, out message)) return false;

                    var so = new SerializedObject(controller);
                    so.Update();
                    so.FindProperty("sceneName").stringValue = cmd.nextSceneName;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    d.Add("sceneName = " + cmd.nextSceneName);
                }

                foreach (var hook in cmd.hooks ?? Array.Empty<Hook>())
                {
                    AddHook(scene, controller, controllerType, hook, d);
                }

                EditorUtility.SetDirty(controller);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    message = "failed to save " + path;
                    return false;
                }

                message = "AdsSplash configured and saved";
                return true;
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static bool EnsureSceneInBuild(string sceneName, List<string> d, out string message)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            int index = scenes.FindIndex(s => Path.GetFileNameWithoutExtension(s.path) == sceneName);
            if (index >= 0)
            {
                if (!scenes[index].enabled)
                {
                    scenes[index].enabled = true;
                    EditorBuildSettings.scenes = scenes.ToArray();
                    d.Add("enabled " + scenes[index].path + " in Build Settings");
                }
                message = null;
                return true;
            }

            var candidates = AssetDatabase.FindAssets(sceneName + " t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.StartsWith("Assets/") && Path.GetFileNameWithoutExtension(p) == sceneName)
                .ToList();

            if (candidates.Count != 1)
            {
                message = candidates.Count == 0
                    ? "no scene named " + sceneName + " under Assets/"
                    : "several scenes named " + sceneName + ": " + string.Join(", ", candidates);
                return false;
            }

            scenes.Add(new EditorBuildSettingsScene(candidates[0], true));
            EditorBuildSettings.scenes = scenes.ToArray();
            d.Add("added " + candidates[0] + " to Build Settings");
            message = null;
            return true;
        }

        private static void AddHook(Scene scene, Component controller, Type controllerType, Hook hook, List<string> d)
        {
            var field = controllerType.GetField(hook.eventName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || !typeof(UnityEvent).IsAssignableFrom(field.FieldType))
                throw new InvalidOperationException("AdsSplashController has no UnityEvent named " + hook.eventName);

            var unityEvent = (UnityEvent)field.GetValue(controller);
            if (unityEvent == null)
            {
                unityEvent = new UnityEvent();
                field.SetValue(controller, unityEvent);
            }

            var go = FindByPath(scene, hook.gameObjectPath);
            if (go == null)
                throw new InvalidOperationException("GameObject not found in " + scene.path + ": " + hook.gameObjectPath);

            var target = go.GetComponents<Component>()
                .FirstOrDefault(c => c != null && (c.GetType().Name == hook.component || c.GetType().FullName == hook.component));
            if (target == null)
                throw new InvalidOperationException("component " + hook.component + " not found on " + hook.gameObjectPath);

            var method = target.GetType().GetMethod(hook.method, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (method == null || method.ReturnType != typeof(void))
                throw new InvalidOperationException(hook.component + "." + hook.method + " must be a public void method with no parameters");

            for (int i = 0; i < unityEvent.GetPersistentEventCount(); i++)
            {
                if (unityEvent.GetPersistentTarget(i) == target && unityEvent.GetPersistentMethodName(i) == hook.method)
                {
                    d.Add(hook.eventName + " already calls " + hook.component + "." + hook.method);
                    return;
                }
            }

            var action = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), target, method);
            UnityEventTools.AddVoidPersistentListener(unityEvent, action);
            d.Add(hook.eventName + " -> " + hook.gameObjectPath + " " + hook.component + "." + hook.method);
        }

        private static GameObject FindByPath(Scene scene, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var parts = path.Trim('/').Split(new[] { '/' }, 2);
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == parts[0]);
            if (root == null || parts.Length == 1) return root;
            var child = root.transform.Find(parts[1]);
            return child != null ? child.gameObject : null;
        }

        // ─── Status ─────────────────────────────────────────────────────────────

        private static void Finish(Command cmd, bool ok, string message, List<string> details)
        {
            _status.lastCommandId = cmd.id;
            _status.lastCommandName = cmd.name;
            _status.lastOk = ok;
            _status.lastMessage = message;
            _status.lastDetails = details?.ToArray() ?? Array.Empty<string>();

            string log = "[BFunAgent] " + cmd.name + " (" + cmd.id + "): " + (ok ? "OK" : "FAILED") + " - " + message;
            if (ok) Debug.Log(log); else Debug.LogError(log);

            Write(null);
        }

        private static void Write(string forcedState)
        {
            _lastWrite = EditorApplication.timeSinceStartup;
            try
            {
                var s = _status;
                s.isCompiling = EditorApplication.isCompiling;
                s.isUpdating = EditorApplication.isUpdating;
                s.state = forcedState
                          ?? (s.isCompiling ? "compiling"
                              : s.isUpdating ? "updating"
                              : CompileErrors.Count > 0 ? "compile-failed"
                              : "ready");
                s.updatedAt = DateTime.UtcNow.ToString("o");
                s.unityVersion = Application.unityVersion;
                s.compileErrors = CompileErrors.Distinct().Take(50).ToArray();
                s.packages = PackageInfo.GetAllRegisteredPackages()
                    .Where(p => p.name.StartsWith("com.thelegends.") || p.name.StartsWith("com.google.ads.mobile")
                                || p.name.StartsWith("com.google.firebase.") || p.name.StartsWith("appsflyer")
                                || p.name.StartsWith("com.pimdewitte.") || p.name.StartsWith("com.v0lt."))
                    .Select(p => p.name + "@" + p.version)
                    .OrderBy(x => x)
                    .ToArray();
                s.tripSoftMenus = TypeCache.GetMethodsWithAttribute<MenuItem>()
                    .SelectMany(m => m.GetCustomAttributes(typeof(MenuItem), false).Cast<MenuItem>())
                    .Select(a => a.menuItem)
                    .Where(p => p != null && p.StartsWith("TripSoft/"))
                    .Distinct()
                    .OrderBy(x => x)
                    .ToArray();
                s.adsSettingsExists = File.Exists(AdsSettingsPath);
                s.appsFlyerSettingsExists = File.Exists(AppsFlyerSettingsPath);
                s.androidDefines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
                s.iosDefines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS);
                s.buildScenes = EditorBuildSettings.scenes.Select(x => x.path + (x.enabled ? "" : " (disabled)")).ToArray();

                string tmp = StatusFile + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(s, true));
                if (File.Exists(StatusFile)) File.Delete(StatusFile);
                File.Move(tmp, StatusFile);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BFunAgent] could not write status: " + e.Message);
            }
        }

        private static Status ReadStatus()
        {
            try { return File.Exists(StatusFile) ? JsonUtility.FromJson<Status>(File.ReadAllText(StatusFile)) : null; }
            catch (Exception) { return null; }
        }

        // ─── Helpers ────────────────────────────────────────────────────────────

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(fullName, false))
                .FirstOrDefault(t => t != null);
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); }
            catch (Exception) { /* ignore */ }
        }
    }
}
