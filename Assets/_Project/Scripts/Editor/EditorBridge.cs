#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoadwaysGame.Editor
{
    /// <summary>
    /// Lets a coding agent drive the open editor without stealing focus: write one command to Temp/bridge/cmd,
    /// read "seq result" from Temp/bridge/out. Commands: status | refresh | play | stop | open PATH | menu PATH | shot FILE | gameview W H.
    /// Editor-only; Temp/ is gitignored and never ships.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorBridge
    {
        private static readonly string Dir = Path.GetFullPath("Temp/bridge");
        private static double nextPoll;
        private static int seq;

        static EditorBridge()
        {
            Directory.CreateDirectory(Dir);
            EditorApplication.update += Poll;
            Write("ready " + DateTime.Now.ToString("HH:mm:ss"));
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.25;
            string cmdPath = Path.Combine(Dir, "cmd");
            if (!File.Exists(cmdPath)) return;
            string cmd = File.ReadAllText(cmdPath).Trim();
            File.Delete(cmdPath);
            try { Write(Run(cmd)); }
            catch (Exception e) { Write("error " + e.Message); }
        }

        private static string Run(string cmd)
        {
            int space = cmd.IndexOf(' ');
            string verb = space < 0 ? cmd : cmd.Substring(0, space);
            string arg = space < 0 ? "" : cmd.Substring(space + 1).Trim();
            switch (verb)
            {
                case "status":
                    return $"playing={EditorApplication.isPlaying} frame={(EditorApplication.isPlaying ? Time.frameCount : 0)} screen={Screen.width}x{Screen.height} compiling={EditorApplication.isCompiling} scene={EditorSceneManager.GetActiveScene().path} dirty={EditorSceneManager.GetActiveScene().isDirty}";
                case "refresh":
                    AssetDatabase.Refresh();
                    return "refreshed";
                case "play":
                    EditorApplication.isPlaying = true;
                    return "entering play";
                case "stop":
                    EditorApplication.isPlaying = false;
                    return "stopping";
                case "open":
                    if (EditorApplication.isPlaying) return "refused: stop play mode first";
                    // Never silently throw away someone's unsaved scene edits.
                    if (EditorSceneManager.GetActiveScene().isDirty) return "refused: active scene has unsaved changes";
                    EditorSceneManager.OpenScene(arg, OpenSceneMode.Single);
                    return "opened " + arg;
                case "menu":
                    return EditorApplication.ExecuteMenuItem(arg) ? "ran " + arg : "no such menu item: " + arg;
                case "shot":
                    if (!EditorApplication.isPlaying) return "refused: screenshots need play mode";
                    string[] sp = arg.Split(' ');
                    return Shot(sp[0], sp.Length > 2 ? int.Parse(sp[1]) : 720, sp.Length > 2 ? int.Parse(sp[2]) : 1600);
                case "gvinfo":
                {
                    Type gv = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                    var sb = new System.Text.StringBuilder("paused=" + EditorApplication.isPaused);
                    foreach (UnityEngine.Object v in Resources.FindObjectsOfTypeAll(gv))
                    {
                        var w = (EditorWindow)v;
                        object idx = gv.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(v);
                        object target = gv.GetProperty("targetSize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(v);
                        sb.Append($" | view pos={w.position} hasFocus={w.hasFocus} docked={w.docked} sizeIndex={idx} target={target}");
                    }
                    return sb.ToString();
                }
                case "gvprops":
                {
                    Type gv = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                    var sb = new System.Text.StringBuilder();
                    foreach (PropertyInfo p in gv.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        if (p.Name.IndexOf("play", StringComparison.OrdinalIgnoreCase) >= 0 || p.Name.IndexOf("focus", StringComparison.OrdinalIgnoreCase) >= 0)
                            sb.Append(p.Name).Append(':').Append(p.PropertyType.Name).Append(' ');
                    return sb.ToString();
                }
                case "playunfocused":
                case "playfocused":
                {
                    // Unfocused: Play no longer activates the Unity app (no focus stealing while an agent drives the editor).
                    // Focused: Unity's default; restore it when handing the editor back to a person.
                    Type gv = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                    PropertyInfo behavior = gv.GetProperty("enterPlayModeBehavior", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    string names = string.Join(",", Enum.GetNames(behavior.PropertyType));
                    string want = verb == "playfocused" ? "PlayFocused" : "Unfocused";
                    string pick = Array.Find(Enum.GetNames(behavior.PropertyType), n => n.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (pick == null) return "no " + want + " option; options: " + names;
                    foreach (UnityEngine.Object v in Resources.FindObjectsOfTypeAll(gv)) behavior.SetValue(v, Enum.Parse(behavior.PropertyType, pick));
                    return "play mode behaviour = " + pick + " (options: " + names + ")";
                }
                case "meshcheck":
                {
                    // For each generated vehicle part: how many triangles face the way their normals say (Unity front = clockwise).
                    var sb = new System.Text.StringBuilder();
                    void Check(string name, Mesh m)
                    {
                        Vector3[] v = m.vertices, n = m.normals; int[] t = m.triangles; int agree = 0, disagree = 0;
                        for (int i = 0; i < t.Length; i += 3)
                        {
                            Vector3 w = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                            if (w.sqrMagnitude < 1e-12f) continue;
                            if (Vector3.Dot(w, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]) > 0f) agree++; else disagree++;
                        }
                        sb.Append($"{name}: agree={agree} inverted={disagree} | ");
                    }
                    foreach (VehicleArchetype a in Enum.GetValues(typeof(VehicleArchetype)))
                    {
                        Check(a + ".hull", BusMeshGenerator.CreateChamferedBodyMesh(a));
                        Check(a + ".glass", BusMeshGenerator.CreateWindowMesh(a));
                        Check(a + ".wheels", BusMeshGenerator.CreateWheelMeshes(a));
                    }
                    Check("arrow", BusMeshGenerator.CreateRoofArrowMesh(0.038f));
                    Check("bean", ChilbiMeshGenerator.CreateCandyBeanMesh(0.34f, 0.5f, 0.28f));
                    return sb.ToString();
                }
                case "zoom":
                {
                    // zoom FILE X Z DIST [PITCH]: close-up capture looking at ground point (X, Z); camera restored afterwards.
                    string[] z = arg.Split(' ');
                    Camera c = Camera.main;
                    if (c == null) return "error: no main camera";
                    Vector3 pos = c.transform.position; Quaternion rot = c.transform.rotation;
                    float pitch = z.Length > 4 ? float.Parse(z[4]) : 50f;
                    Quaternion look = Quaternion.Euler(pitch, 0f, 0f);
                    Vector3 target = new Vector3(float.Parse(z[1]), 0f, float.Parse(z[2]));
                    try
                    {
                        string r = Shot(z[0], 720, 720, () => { c.transform.SetPositionAndRotation(target - look * Vector3.forward * float.Parse(z[3]), look); });
                        return "zoom " + r;
                    }
                    finally { c.transform.SetPositionAndRotation(pos, rot); }
                }
                case "autoplay":
                {
                    var game = UnityEngine.Object.FindAnyObjectByType<RoadwaysGame.Slice.SliceGame>();
                    if (game == null) return "error: no SliceGame running";
                    string[] ap = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); // autoplay [PAUSE] [SPEED]
                    game.StartAutoplay(ap.Length > 0 ? float.Parse(ap[0]) : 0.15f, ap.Length > 1 ? float.Parse(ap[1]) : 1f);
                    return "autoplay started";
                }
                case "pause": // pause on|off: opens / closes the pause popup
                {
                    var game = UnityEngine.Object.FindAnyObjectByType<RoadwaysGame.Slice.SliceGame>();
                    if (game == null) return "error: no SliceGame running";
                    game.SetPausedForTest(arg != "off");
                    return "paused " + (arg != "off");
                }
                case "home": // the pause popup's Home button
                {
                    var game = UnityEngine.Object.FindAnyObjectByType<RoadwaysGame.Slice.SliceGame>();
                    if (game == null) return "error: no SliceGame running";
                    game.GoHomeForTest();
                    return "home";
                }
                case "level":
                {
                    var game = UnityEngine.Object.FindAnyObjectByType<RoadwaysGame.Slice.SliceGame>();
                    if (game == null) return "error: no SliceGame running";
                    game.LoadLevelForTest(int.Parse(arg));
                    return "level " + arg;
                }
                case "showgame":
                {
                    // Bring the Game view tab to the front of its dock without activating the Unity app.
                    Type gv = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                    EditorWindow.GetWindow(gv, false, null, false);
                    return "game view shown";
                }
                case "gameview":
                    string[] wh = arg.Split(' ');
                    return SetGameViewSize(int.Parse(wh[0]), int.Parse(wh[1]));
                default:
                    return "unknown command: " + cmd;
            }
        }

        /// <summary>Selects (adding if needed) a fixed Game view resolution, so the editor frames the game like the phone does.
        /// Uses internal editor types via reflection; failures come back as an error string rather than breaking the editor.</summary>
        private static string SetGameViewSize(int w, int h)
        {
            const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            Assembly asm = typeof(UnityEditor.Editor).Assembly;
            Type sizesType = asm.GetType("UnityEditor.GameViewSizes"), sizeType = asm.GetType("UnityEditor.GameViewSize");
            object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            PropertyInfo current = sizesType.GetProperty("currentGroupType", any);
            object groupType = current.GetValue(current.GetGetMethod(true).IsStatic ? null : sizes);
            object group = sizesType.GetMethod("GetGroup", any).Invoke(sizes, new[] { groupType });
            MethodInfo count = group.GetType().GetMethod("GetTotalCount", any), get = group.GetType().GetMethod("GetGameViewSize", any);
            int index = -1, total = (int)count.Invoke(group, null);
            for (int i = 0; i < total && index < 0; i++)
            {
                object s = get.Invoke(group, new object[] { i });
                if ((int)sizeType.GetProperty("width", any).GetValue(s) == w && (int)sizeType.GetProperty("height", any).GetValue(s) == h) index = i;
            }
            if (index < 0)
            {
                object fixedRes = Enum.Parse(asm.GetType("UnityEditor.GameViewSizeType"), "FixedResolution");
                group.GetType().GetMethod("AddCustomSize", any).Invoke(group, new[] { Activator.CreateInstance(sizeType, fixedRes, w, h, "Device " + w + "x" + h) });
                index = (int)count.Invoke(group, null) - 1;
            }
            Type gameViewType = asm.GetType("UnityEditor.GameView");
            UnityEngine.Object[] views = Resources.FindObjectsOfTypeAll(gameViewType);
            if (views.Length == 0) return "no Game view is open";
            foreach (UnityEngine.Object view in views)
            {
                // The editor's own menu callback also refreshes the view; setting the index property alone does not.
                MethodInfo select = gameViewType.GetMethod("SizeSelectionCallback", any);
                if (select != null) select.Invoke(view, new object[] { index, null });
                else gameViewType.GetProperty("selectedSizeIndex", any).SetValue(view, index);
                ((EditorWindow)view).Repaint();
            }
            return "game view " + w + "x" + h + " (size index " + index + ")";
        }

        /// <summary>Renders the main camera straight into a texture. Unlike ScreenCapture this works while the editor is
        /// unfocused (the Game view doesn't repaint then). HUDs must use a ScreenSpaceCamera canvas to be included.</summary>
        private static string Shot(string path, int w, int h, Action pose = null)
        {
            Camera cam = Camera.main;
            if (cam == null) return "error: no main camera";
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = cam.targetTexture, previousActive = RenderTexture.active;
            var game = UnityEngine.Object.FindAnyObjectByType<RoadwaysGame.Slice.SliceGame>();
            try
            {
                cam.targetTexture = rt;
                if (pose != null) { cam.aspect = w / (float)h; pose(); }
                else if (game != null) game.FrameFor(w, h); // frame for the capture's aspect, not the editor window's
                foreach (var scaler in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.CanvasScaler>(FindObjectsSortMode.None))
                {
                    scaler.enabled = false; scaler.enabled = true; // re-run scaling against the new render target
                }
                Canvas.ForceUpdateCanvases();
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            finally
            {
                cam.targetTexture = previousTarget;
                cam.ResetAspect();
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(rt);
            }
            return "shot " + w + "x" + h + " " + path;
        }

        private static void Write(string result) => File.WriteAllText(Path.Combine(Dir, "out"), (++seq) + " " + result);
    }
}
#endif
