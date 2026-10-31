// CuttlefishAddMenu.cs — Unity 6.x editor-only Shift+A popup.
// Place in Assets/Editor. Remove older BlenderAddMenu/CuttlefishAddMenu scripts to avoid duplicates.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CuttlefishAddMenu
{
    // Shared preference key used by Cuttlefish Mode (retain for compatibility).
    private const string EnabledKey = "BlenderTransformTool.Enabled";
    // Existing prefab directory retained so saved prefabs remain discoverable.
    private const string PrefabFolder = "Assets/Prefabs";
    private static bool rightHeld;

    static CuttlefishAddMenu() { SceneView.duringSceneGui += OnSceneGUI; }

    // Diagnostic fallback: use this menu command to verify the new script loaded.
    [MenuItem("Tools/Cuttlefish Add Menu #a")]
    private static void OpenFromTools()
    {
        SceneView scene = SceneView.lastActiveSceneView;
        if (scene == null) return;
        Vector3 position = scene.pivot;
        Quaternion rotation = scene.rotation;
        EditorApplication.delayCall += () =>
            PopupWindow.Show(new Rect(120, 100, 1, 1), new AddPopup(position, rotation));
    }

    private static void OnSceneGUI(SceneView view)
    {
        Event e = Event.current;
        if (e == null) return;
        if (e.type == EventType.MouseDown && e.button == 1) rightHeld = true;
        if (e.type == EventType.MouseUp && e.button == 1) rightHeld = false;
        if (!EditorPrefs.GetBool(EnabledKey, false) || rightHeld || GUIUtility.hotControl != 0) return;
        if (e.type != EventType.KeyDown || e.keyCode != KeyCode.A || !e.shift || e.alt || e.control || e.command) return;
        Vector2 at = e.mousePosition;
        Vector3 position = view.pivot;
        Quaternion rotation = view.rotation;
        e.Use();
        Rect anchor = FindOpeningRect(view, at);
        EditorApplication.delayCall += () =>
            PopupWindow.Show(anchor, new AddPopup(position, rotation));
    }

    // Position the *compact* popup with room for its eventual second column.
    // The menu itself still expands/collapses exactly as before.
    private static Rect FindOpeningRect(SceneView view, Vector2 mouseGUI)
    {
        const float expandedWidth = 408f; // 2 * AddPopup.ColumnWidth
        const float expandedHeight = 440f; // room for the longest normal submenu
        const float margin = 8f;

        Vector2 screen = GUIUtility.GUIToScreenPoint(mouseGUI);
        Rect bounds = view.position; // editor window bounds in screen coordinates
        float left = bounds.xMin + margin;
        float right = Mathf.Max(left, bounds.xMax - expandedWidth - margin);
        float top = bounds.yMin + margin;
        float bottom = Mathf.Max(top, bounds.yMax - expandedHeight - margin);
        screen.x = Mathf.Clamp(screen.x, left, right);
        screen.y = Mathf.Clamp(screen.y, top, bottom);
        Vector2 gui = GUIUtility.ScreenToGUIPoint(screen);
        return new Rect(gui.x, gui.y, 1f, 1f);
    }

    private sealed class AddPopup : PopupWindowContent
    {
        private readonly Vector3 position;
        private readonly Quaternion rotation;
        private readonly List<Entry> entries = new List<Entry>();
        private string page = "";
        private readonly Stack<string> pageHistory = new Stack<string>();
        private GUIStyle rowStyle;
        private GUIStyle headingStyle;
        private GUIStyle searchHintStyle;
        private Texture2D background;
        private string searchText = "";

        private sealed class Entry
        {
            public string label, icon;
            public Action action;
            public string child;
            public bool separator;
            public Entry(string label, string icon, Action action = null, string child = null)
            { this.label = label; this.icon = icon; this.action = action; this.child = child; }
            public static Entry CreateDivider() { return new Entry("", "") { separator = true }; }
        }

        public AddPopup(Vector3 position, Quaternion rotation)
        { this.position = position; this.rotation = rotation; }

        private int selectedIndex;
        private int rootIndex = -1;
        private string openPage = "";
        private const float ColumnWidth = 204f;
        private const float RowHeight = 23f;
        private const float Pad = 6f;
        private const int MaxRows = 18; // Maximum visible rows; longer lists scroll.
        private Vector2 rightScroll;
        private const float SearchHeight = 28f;
        private const float SeparatorHeight = 10f;
        private Texture2D roundedHighlight;
        private Texture2D roundedMenu;

        private int RootRowCount() { return 8; }
        private bool ShowingResults { get { return !string.IsNullOrWhiteSpace(searchText); } }
        private bool ShowingRight { get { return ShowingResults || openPage.Length > 0; } }

        // Separators have their own height and never count as selectable rows.
        private static float ContentHeight(List<Entry> list, bool limit = true)
        {
            float height = Pad * 2f;
            int visible = 0;
            foreach (Entry entry in list)
            {
                if (limit && !entry.separator && visible >= MaxRows) break;
                height += entry.separator ? SeparatorHeight : RowHeight;
                if (!entry.separator) visible++;
            }
            return height;
        }

        public override Vector2 GetWindowSize()
        {
            List<Entry> right = ShowingResults ? SearchMatches() :
                openPage.Length > 0 ? GetEntries(openPage) : null;
            float leftHeight = SearchHeight + ContentHeight(RootEntries());
            float rightHeight = right == null ? 0f : Mathf.Min(ContentHeight(right, false), Pad * 2f + MaxRows * RowHeight);
            return new Vector2(ColumnWidth * (ShowingRight ? 2 : 1),
                Mathf.Max(leftHeight, rightHeight));
        }

        public override void OnOpen()
        {
            page = "";
            Build();
            selectedIndex = 0;
            rootIndex = -1;
        }

        private void ResizeToContents()
        {
            if (editorWindow == null) return;
            Vector2 size = GetWindowSize();
            Rect r = editorWindow.position;
            r.width = size.x;
            r.height = size.y;
            editorWindow.minSize = size;
            editorWindow.maxSize = size;
            editorWindow.position = r;
            editorWindow.Repaint();
        }

        private void Build()
        {
            entries.Clear();
            if (page == "search")
            {
                // Search results are populated by GetCurrentEntries.
            }
            else if (page == "")
            {
                // Explicit insertion order; no Unity GenericMenu sorting.
                entries.Add(new Entry("Search", "Search Icon", child: "search"));
                entries.Add(new Entry("My Prefabs", "Prefab Icon", child: "prefabs"));
                entries.Add(Entry.CreateDivider());
                entries.Add(new Entry("3D Objects", "GameObject Icon", child: "objects"));
                entries.Add(new Entry("Scene Objects", "Camera Icon", child: "scene"));
                entries.Add(new Entry("Lights", "Light Icon", child: "lights"));
                entries.Add(new Entry("Effects", "ParticleSystem Icon", child: "effects"));
                entries.Add(new Entry("UI", "Canvas Icon", child: "ui"));
            }
            else if (page == "objects")
            {
                entries.Add(new Entry("Empty", "Empty Dot", () =>
                {
                    GameObject go = new GameObject("GameObject");
                    go.transform.position = position;
                    Register(go, "Add Empty GameObject");
                }));
                entries.Add(Entry.CreateDivider());
                // ALC-inspired ordering: primary shapes, round shapes, then flat shapes.
                AddImportedPrimitive("Cone", "Cone");
                AddPrimitive("Cube", PrimitiveType.Cube);
                AddImportedPrimitive("Icosphere", "icosphere");
                entries.Add(Entry.CreateDivider());
                AddImportedPrimitive("Torus", "torus");
                AddPrimitive("Capsule", PrimitiveType.Capsule);
                AddImportedPrimitive("Cylinder", "cylinder");
                entries.Add(Entry.CreateDivider());
                // Other Shapes — imported Blender models.
                AddImportedPrimitive("Pyramid", "pyramid");
                AddImportedPrimitive("Star", "star");
                AddImportedPrimitive("Egg", "egg");
                AddImportedPrimitive("Hexagon", "hexagon");
                entries.Add(Entry.CreateDivider());
                // Flat primitives at the bottom of the list.
                AddPrimitive("Plane", PrimitiveType.Plane);
                AddPrimitive("Quad", PrimitiveType.Quad);
            }
            else if (page == "scene")
            {
                entries.Add(new Entry("Empty", "GameObject Icon", () =>
                {
                    GameObject go = new GameObject("GameObject");
                    go.transform.position = position;
                    Register(go, "Add Empty GameObject");
                }));
                entries.Add(new Entry("Camera", "Camera Icon", () =>
                {
                    GameObject go = new GameObject("Camera");
                    go.AddComponent<Camera>();
                    go.transform.SetPositionAndRotation(position, rotation);
                    Register(go, "Add Camera");
                }));
            }
            else if (page == "lights")
            {
                AddLight("Directional Light", LightType.Directional);
                AddLight("Point Light", LightType.Point);
                AddLight("Spot Light", LightType.Spot);
            }
            else if (page == "effects")
            {
                AddComponentObject<ParticleSystem>("Particle System");
                AddComponentObject<TrailRenderer>("Trail Renderer");
                AddComponentObject<LineRenderer>("Line Renderer");
            }
            else if (page == "ui")
            {
                AddUnityMenu("Canvas", "GameObject/UI/Canvas");
                AddUnityMenu("Event System", "GameObject/UI/Event System");
                entries.Add(Entry.CreateDivider());
                AddUnityMenu("Image", "GameObject/UI/Image");
                AddUnityMenu("Raw Image", "GameObject/UI/Raw Image");
                AddUnityMenu("Panel", "GameObject/UI/Panel");
                AddUnityMenu("Toggle", "GameObject/UI/Toggle");
                AddUnityMenu("Slider", "GameObject/UI/Slider");
                AddUnityMenu("Scrollbar", "GameObject/UI/Scrollbar");
                AddUnityMenu("Scroll View", "GameObject/UI/Scroll View");
                entries.Add(Entry.CreateDivider());
                AddUnityMenu("Text - TextMeshPro", "GameObject/UI/Text - TextMeshPro");
                AddUnityMenu("Button - TextMeshPro", "GameObject/UI/Button - TextMeshPro");
                AddUnityMenu("Dropdown - TextMeshPro", "GameObject/UI/Dropdown - TextMeshPro");
                AddUnityMenu("Input Field - TextMeshPro", "GameObject/UI/Input Field - TextMeshPro");
            }
            else if (page == "prefabs" || page.StartsWith("prefabs/", StringComparison.Ordinal))
            {
                if (!AssetDatabase.IsValidFolder(PrefabFolder))
                {
                    entries.Add(new Entry("No prefab folder found", "console.warnicon"));
                    return;
                }
                string[] ids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
                List<string> paths = new List<string>();
                foreach (string id in ids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(id);
                    if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) paths.Add(path);
                }
                paths.Sort(StringComparer.OrdinalIgnoreCase);
                string subfolder = page == "prefabs" ? "" : page.Substring("prefabs/".Length);
                string prefix = subfolder.Length == 0 ? "" : subfolder + "/";
                SortedSet<string> folders = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                List<string> visiblePrefabs = new List<string>();
                foreach (string path in paths)
                {
                    string relative = path.Substring(PrefabFolder.Length + 1);
                    if (!relative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                    string remainder = relative.Substring(prefix.Length);
                    int slash = remainder.IndexOf('/');
                    if (slash >= 0) folders.Add(remainder.Substring(0, slash));
                    else visiblePrefabs.Add(path);
                }
                foreach (string folder in folders)
                    entries.Add(new Entry(folder, "Folder Icon", child: "prefabs/" + prefix + folder));
                foreach (string path in visiblePrefabs)
                {
                    string saved = path;
                    string relative = path.Substring(PrefabFolder.Length + 1 + prefix.Length);
                    string label = relative.Substring(0, relative.Length - ".prefab".Length);
                    entries.Add(new Entry(label, "Prefab Icon", () => SpawnPrefab(saved)));
                }
                if (entries.Count == 0)
                    entries.Add(new Entry("No prefabs in this folder", "Prefab Icon"));
            }
        }

        private void AddPrimitive(string name, PrimitiveType type)
        {
            entries.Add(new Entry(name, "GameObject Icon", () =>
            {
                GameObject go = GameObject.CreatePrimitive(type);
                go.transform.position = position;
                Register(go, "Add " + name);
            }));
        }

        // Load meshes from Blender model assets without instantiating the FBX hierarchy.
        private void AddImportedPrimitive(string name, string modelFile)
        {
            entries.Add(new Entry(name, "GameObject Icon", () =>
            {
                string folder = "Assets/Editor/CuttlefishMeshes/";
                string path = folder + modelFile + ".fbx";
                if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                {
                    // Blender files can also be imported as .blend assets.
                    path = folder + modelFile + ".blend";
                }
                Mesh mesh = null;
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    Mesh candidate = asset as Mesh;
                    if (candidate != null && (mesh == null || candidate.vertexCount > mesh.vertexCount))
                        mesh = candidate;
                }
                if (mesh == null)
                {
                    Debug.LogWarning("Cuttlefish Add Menu: No mesh found in " + path + ". Check the model filename and import settings.");
                    return;
                }
                GameObject go = new GameObject(name);
                go.transform.position = position;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                GameObject materialSource = GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    renderer.sharedMaterial = materialSource.GetComponent<MeshRenderer>().sharedMaterial;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(materialSource);
                }
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                Register(go, "Add " + name);
            }));
        }

        private void AddLight(string name, LightType type)
        {
            entries.Add(new Entry(name, "Light Icon", () =>
            {
                GameObject go = new GameObject(name);
                go.AddComponent<Light>().type = type;
                go.transform.position = position;
                Register(go, "Add " + name);
            }));
        }

        private void AddComponentObject<T>(string name) where T : Component
        {
            entries.Add(new Entry(name, "GameObject Icon", () =>
            {
                GameObject go = new GameObject(name);
                go.transform.position = position;
                go.AddComponent<T>();
                Register(go, "Add " + name);
            }));
        }

        private void AddUnityMenu(string label, string menuPath)
        {
            entries.Add(new Entry(label, "Canvas Icon", () =>
            {
                if (!EditorApplication.ExecuteMenuItem(menuPath))
                    Debug.LogWarning("Unity menu item not available: " + menuPath +
                        ". Check that Unity UI / TextMeshPro packages are installed.");
            }));
        }

        private void SpawnPrefab(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) return;
            GameObject go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (!go) return;
            Undo.RegisterCreatedObjectUndo(go, "Add " + prefab.name);
            Undo.RecordObject(go.transform, "Position " + prefab.name);
            go.transform.position = position;
            Selection.activeGameObject = go;
        }

        private static void Register(GameObject go, string description)
        {
            Undo.RegisterCreatedObjectUndo(go, description);
            Selection.activeGameObject = go;
        }

        public override void OnGUI(Rect rect)
        {
            if (rowStyle == null)
            {
                rowStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, fontSize = 11 };
                rowStyle.normal.textColor = new Color(.91f, .91f, .91f);
                searchHintStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0)
                };
                searchHintStyle.normal.textColor = new Color(.63f, .63f, .65f);
                headingStyle = new GUIStyle(EditorStyles.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
                headingStyle.normal.textColor = Color.white;
                background = Solid(new Color(.105f, .108f, .12f));
                // Soft neutral selection and gently rounded popup surface.
                roundedHighlight = Rounded(new Color(.34f, .35f, .38f), 4);
                roundedMenu = Rounded(new Color(.105f, .108f, .12f), 6);
            }

            Event e = Event.current;
            bool searchFocused = GUI.GetNameOfFocusedControl() == "CuttlefishAddSearch";
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Escape)
                {
                    if (ShowingResults) { searchText = ""; ResizeToContents(); }
                    else if (openPage.Length > 0) CloseSubmenu();
                    else editorWindow.Close();
                    e.Use(); return;
                }
                if (!searchFocused && (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.Backspace))
                {
                    CloseSubmenu(); e.Use(); return;
                }
                if (e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.UpArrow)
                {
                    int count = ShowingResults ? SearchMatches().Count :
                        openPage.Length > 0 ? NavigableEntries(openPage).Count : RootRowCount();
                    if (count > 0)
                    {
                        selectedIndex = (selectedIndex + (e.keyCode == KeyCode.DownArrow ? 1 : count - 1)) % count;
                        if (!ShowingRight) rootIndex = selectedIndex;
                        else
                        {
                            float targetTop = Pad + selectedIndex * RowHeight;
                            float visibleHeight = Mathf.Max(1f, GetWindowSize().y - Pad * 2f);
                            if (targetTop < rightScroll.y) rightScroll.y = targetTop;
                            if (targetTop + RowHeight > rightScroll.y + visibleHeight)
                                rightScroll.y = targetTop + RowHeight - visibleHeight;
                        }
                    }
                    e.Use(); editorWindow.Repaint(); return;
                }
                if (e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    if (ShowingResults || openPage.Length > 0)
                    {
                        List<Entry> items = ShowingResults ? SearchMatches() : NavigableEntries(openPage);
                        if (items.Count > 0)
                        {
                            Entry item = items[Mathf.Clamp(selectedIndex, 0, items.Count - 1)];
                            if (item.child != null) OpenSubmenu(item.child);
                            else if (item.action != null) { item.action(); editorWindow.Close(); }
                        }
                    }
                    else
                    {
                        List<Entry> choices = RootEntries().FindAll(item => !item.separator);
                        Entry item = choices[Mathf.Clamp(selectedIndex, 0, choices.Count - 1)];
                        if (item.child != null) OpenSubmenu(item.child);
                        else if (item.action != null) { item.action(); editorWindow.Close(); }
                    }
                    e.Use(); return;
                }
            }

            DrawNineSlice(new Rect(0, 0, rect.width, rect.height), roundedMenu, 6f);
            GUI.SetNextControlName("CuttlefishAddSearch");
            Rect searchRect = new Rect(7, Pad, ColumnWidth - 14, 21);
            string typed = GUI.TextField(searchRect,
                searchText, EditorStyles.textField);
            if (string.IsNullOrEmpty(typed) && GUI.GetNameOfFocusedControl() != "CuttlefishAddSearch")
                GUI.Label(new Rect(searchRect.x + 7, searchRect.y + 1, searchRect.width - 12, 19),
                    "Search...", searchHintStyle);
            if (typed != searchText)
            {
                searchText = typed;
                selectedIndex = 0;
                if (ShowingResults) openPage = "";
                ResizeToContents();
            }

            List<Entry> roots = RootEntries();
            DrawColumn(roots, 0f, ShowingResults ? -1 : rootIndex, false, SearchHeight);

            if (ShowingRight)
            {
                EditorGUI.DrawRect(new Rect(ColumnWidth - 1, 0, 1, rect.height), new Color(.32f, .32f, .32f));
                List<Entry> items = ShowingResults ? SearchMatches() : GetEntries(openPage);
                float contentHeight = ContentHeight(items, false);
                Rect viewport = new Rect(ColumnWidth + 2f, 4f, ColumnWidth - 10f, rect.height - 8f);
                // Scroll only the right column, keeping the root menu and search fixed.
                // Keep the entire scrollbar inside the popup with a 4px top/bottom margin.
                // The row highlight also stops before the scrollbar instead of running underneath it.
                // Keep the track 9px wide, but make the draggable thumb slimmer.
                // Preserve the existing inset and remove arrow buttons.
                GUISkin originalSkin = GUI.skin;
                GUISkin narrowSkin = UnityEngine.Object.Instantiate(originalSkin);
                narrowSkin.hideFlags = HideFlags.HideAndDontSave;
                narrowSkin.verticalScrollbar = new GUIStyle(originalSkin.verticalScrollbar) { fixedWidth = 3f };
                narrowSkin.verticalScrollbarThumb = new GUIStyle(originalSkin.verticalScrollbarThumb)
                {
                    fixedWidth = 7f,
                    margin = new RectOffset(0, 2, 0, 0)
                };
                narrowSkin.verticalScrollbarUpButton = GUIStyle.none;
                narrowSkin.verticalScrollbarDownButton = GUIStyle.none;
                try
                {
                    GUI.skin = narrowSkin;
                    rightScroll = GUI.BeginScrollView(viewport, rightScroll,
                        new Rect(0f, 0f, viewport.width - 10f, contentHeight), false, contentHeight > viewport.height);
                    DrawColumn(items, 0f, selectedIndex, true, 0f);
                    GUI.EndScrollView();
                }
                finally
                {
                    GUI.skin = originalSkin;
                    UnityEngine.Object.DestroyImmediate(narrowSkin);
                }
            }
        }

        private List<Entry> RootEntries()
        {
            return new List<Entry>
            {
                new Entry("Prefabs", "Prefab Icon", child:"prefabs"),
                Entry.CreateDivider(),
                new Entry("3D Objects", "Plus Icon", child:"objects"),
                new Entry("Light", "Light Icon", child:"lights"),
                new Entry("UI Canvas", "Canvas Icon", child:"ui"),
                new Entry("Effects", "ParticleSystem Icon", child:"effects"),
                Entry.CreateDivider(),
                new Entry("Camera", "Camera Icon", () =>
                {
                    GameObject go = new GameObject("Camera");
                    go.AddComponent<Camera>();
                    go.transform.SetPositionAndRotation(position, rotation);
                    Register(go, "Add Camera");
                }),
                new Entry("Audio Source", "Audio Icon", () =>
                {
                    GameObject go = new GameObject("Audio Source");
                    go.transform.position = position;
                    go.AddComponent<AudioSource>();
                    Register(go, "Add Audio Source");
                }),
                new Entry("New Scene", "Scene Icon", () =>
                {
                    // Honor Unity's unsaved-scene confirmation before replacing a scene.
                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                })
            };
        }

        private List<Entry> GetEntries(string destination)
        {
            string oldPage = page;
            List<Entry> saved = new List<Entry>(entries);
            page = destination;
            Build();
            List<Entry> result = new List<Entry>();
            foreach (Entry entry in entries) result.Add(entry);
            entries.Clear();
            entries.AddRange(saved);
            page = oldPage;
            return result;
        }

        private List<Entry> NavigableEntries(string destination)
        {
            return GetEntries(destination).FindAll(item => !item.separator);
        }

        private List<Entry> SearchMatches()
        {
            List<Entry> matches = new List<Entry>();
            if (!ShowingResults) return matches;
            foreach (Entry entry in RootEntries())
                if (entry.action != null &&
                    entry.label.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    matches.Add(entry);
            string[] categories = { "objects", "lights", "effects", "ui" };
            foreach (string category in categories)
                foreach (Entry entry in GetEntries(category))
                    if (entry.action != null &&
                        entry.label.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                        matches.Add(entry);
            if (AssetDatabase.IsValidFolder(PrefabFolder))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string name = System.IO.Path.GetFileNameWithoutExtension(path);
                    if (name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string captured = path;
                        matches.Add(new Entry(name, "Prefab Icon", () => SpawnPrefab(captured)));
                    }
                }
            }
            return matches;
        }

        private void OpenSubmenu(string destination)
        {
            if (openPage == destination) return;
            if (openPage.Length > 0) pageHistory.Push(openPage);
            openPage = destination;
            selectedIndex = 0;
            rightScroll = Vector2.zero;
            ResizeToContents();
        }

        private void CloseSubmenu()
        {
            if (pageHistory.Count > 0) openPage = pageHistory.Pop();
            else { openPage = ""; selectedIndex = Math.Max(0, rootIndex); }
            rightScroll = Vector2.zero;
            ResizeToContents();
        }

        private void DrawColumn(List<Entry> items, float x, int selection, bool submenu, float offset)
        {
            float y = Pad + offset;
            int selectableIndex = 0;
            foreach (Entry entry in items)
            {
                if (entry.separator)
                {
                    // One deliberate, inset separator in a short dedicated gap.
                    EditorGUI.DrawRect(
                        new Rect(x + 11, y + SeparatorHeight * .5f, ColumnWidth - 22, 1f),
                        new Color(.30f, .30f, .32f, .85f));
                    y += SeparatorHeight;
                    continue;
                }
                if (!submenu && selectableIndex >= MaxRows) break;
                Rect r = new Rect(x + 4, y, ColumnWidth - (submenu ? 23f : 8f), RowHeight - 1f);
                bool over = r.Contains(Event.current.mousePosition);
                bool highlighted = selectableIndex == selection || over;
                if (highlighted)
                    DrawNineSlice(r, roundedHighlight, 4f);

                // The main groups have muted category tints. Utilities stay gray.
                Color tint = Color.white;
                if (!submenu)
                {
                    switch (selectableIndex)
                    {
                        case 1: tint = new Color(.57f, .81f, .62f); break; // 3D
                        case 2: tint = new Color(.84f, .62f, .84f); break; // Lights
                        case 3: tint = new Color(.65f, .73f, .92f); break; // UI
                        case 4: tint = new Color(.87f, .72f, .49f); break; // Effects
                        case 5:
                        case 6:
                        case 7: tint = new Color(.54f, .54f, .56f); break; // Utilities
                    }
                }
                // Submenu items receive a soft rainbow, evenly spread over the list.
                if (submenu && !entry.separator)
                {
                    int total = 0;
                    foreach (Entry candidate in items) if (!candidate.separator) total++;
                    float t = total <= 1 ? 0f : (float)selectableIndex / (total - 1);
                    tint = SoftRainbow(t, ShowingResults ? "search" : openPage);
                }
                // A neutral gray selection with crisp white lettering.
                if (highlighted) tint = Color.white;
                Color oldColor = GUI.color;
                GUI.color = tint;
                Rect iconRect = new Rect(r.x + 6, r.y + (r.height - 19f) * .5f, 19f, 19f);
                if (!DrawCustomPrimitiveIcon(entry.label, entry.icon, iconRect))
                {
                    if (!DrawPrimitiveIcon(entry.label, new Rect(r.x + 6, r.y, 19, r.height), tint))
                        GUI.Label(new Rect(r.x + 6, r.y, 19, r.height), IconGlyph(entry), headingStyle);
                }
                GUI.Label(new Rect(r.x + 29, r.y, r.width - 47, r.height), entry.label, rowStyle);
                if (entry.child != null)
                    GUI.Label(new Rect(r.xMax - 15, r.y, 12, r.height), "›", rowStyle);
                GUI.color = oldColor;

                if (over && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    if (!submenu) rootIndex = selectableIndex;
                    if (entry.child != null) OpenSubmenu(entry.child);
                    else if (entry.action != null)
                    {
                        entry.action();
                        editorWindow.Close();
                    }
                    Event.current.Use();
                    GUIUtility.ExitGUI();
                }
                if (over && Event.current.type == EventType.MouseMove)
                {
                    if (!submenu)
                    {
                        if (!ShowingResults &&
                            (rootIndex != selectableIndex || openPage != (entry.child ?? "")))
                        {
                            rootIndex = selectableIndex;
                            openPage = entry.child ?? "";
                            selectedIndex = 0;
                            pageHistory.Clear();
                            ResizeToContents();
                        }
                    }
                    else
                    {
                        selectedIndex = selectableIndex;
                    }
                    editorWindow.Repaint();
                }
                y += RowHeight;
                selectableIndex++;
            }
        }

        private static Color SoftRainbow(float t, string category)
        {
            // Begin each submenu's gradient at the parent category hue.
            // Hue wraps naturally through the pastel rainbow.
            float startHue;
            switch (category)
            {
                case "objects": startHue = .35f; break;  // green
                case "lights": startHue = .84f; break;   // magenta
                case "ui": startHue = .61f; break;       // blue
                case "effects": startHue = .105f; break;// amber
                default: startHue = .075f; break;       // prefabs/search: orange
            }
            float hue = Mathf.Repeat(startHue + Mathf.Clamp01(t) * .83f, 1f);
            return Color.HSVToRGB(hue, .36f, .94f);
        }

        private static Texture2D Rounded(Color color, int radius)
        {
            const int size = 24;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + .5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + .5f, radius, size - radius);
                    float dist = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy));
                    float alpha = Mathf.Clamp01(radius + .5f - dist);
                    tex.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * alpha));
                }
            tex.Apply();
            return tex;
        }

        // Nine-slice keeps corner radii circular instead of stretching them.
        private static void DrawNineSlice(Rect r, Texture2D tex, float corner)
        {
            if (tex == null) return;
            float c = Mathf.Min(corner, r.width * .5f, r.height * .5f);
            float[] xs = { r.x, r.x + c, r.xMax - c, r.xMax };
            float[] ys = { r.y, r.y + c, r.yMax - c, r.yMax };
            float uv = corner / tex.width;
            float[] us = { 0f, uv, 1f - uv, 1f };
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                    GUI.DrawTextureWithTexCoords(
                        new Rect(xs[x], ys[y], xs[x+1]-xs[x], ys[y+1]-ys[y]),
                        tex, new Rect(us[x], 1f - us[y+1], us[x+1]-us[x], us[y+1]-us[y]));
        }

        // Load the user's icons from anywhere under Assets/Editor, including subfolders.
        // Icon names match the PNG filenames, including sun, spot-light, and point-light (case-insensitive).
        private static readonly Dictionary<string, Texture2D> customIconCache = new Dictionary<string, Texture2D>();

        private static Texture2D FindCustomIcon(string iconName)
        {
            if (customIconCache.TryGetValue(iconName, out Texture2D cached) && cached != null)
                return cached;

            foreach (string guid in AssetDatabase.FindAssets(iconName + " t:Texture2D", new[] { "Assets/Editor" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(System.IO.Path.GetFileNameWithoutExtension(path), iconName,
                    StringComparison.OrdinalIgnoreCase)) continue;
                Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (icon != null)
                {
                    customIconCache[iconName] = icon;
                    return icon;
                }
            }
            return null;
        }

        private static bool DrawCustomPrimitiveIcon(string label, string iconType, Rect rect)
        {
            string iconName = label == "Empty" ? "empty" :
                              label == "Cone" ? "triangle" :
                              label == "Cube" ? "square" :
                              label == "Icosphere" ? "circle" :
                              label == "Torus" ? "torus" :
                              label == "Capsule" ? "capsule" :
                              label == "Cylinder" ? "cylinder" :
                              label == "Plane" ? "plane" :
                              label == "Quad" ? "quad" :
                              label == "Pyramid" ? "pyramid" :
                              label == "Star" ? "star" :
                              label == "Egg" ? "egg" :
                              label == "Hexagon" ? "hexagon" :
                              label == "Directional Light" || label == "Light" || label == "Lights" ? "sun" :
                              label == "Spot Light" ? "spot-light" :
                              label == "Point Light" ? "point-light" :
                              label == "Particle System" || label == "Effects" ? "particles" :
                              label == "Trail Renderer" ? "trail" :
                              label == "Line Renderer" ? "line" :
                              label == "Camera" ? "camera" :
                              label == "Prefabs" || label == "My Prefabs" || label == "No prefabs in this folder" ? "prefab" :
                              label == "New Scene" ? "scene" : null;
            if (iconName == null && iconType == "Prefab Icon") iconName = "prefab";
            if (iconName == null) return false;
            Texture2D icon = FindCustomIcon(iconName);
            if (icon == null) return false;
            GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit, true);
            return true;
        }

        // Consistent 1.7px vector strokes instead of font-dependent Unicode outlines.
        private static bool DrawPrimitiveIcon(string label, Rect rect, Color tint)
        {
            bool circle = label == "Icosphere" || label == "Torus" ||
                          label == "Capsule" || label == "Cylinder";
            bool square = label == "Cube";
            bool triangle = label == "Cone";
            bool slant = label == "Plane" || label == "Quad";
            if (!circle && !square && !triangle && !slant) return false;

            Vector2 center = rect.center;
            const float radius = 6.5f;
            Vector3[] points;
            if (circle)
            {
                const int segments = 32;
                points = new Vector3[segments + 1];
                for (int i = 0; i <= segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    points[i] = new Vector3(center.x + Mathf.Cos(angle) * radius,
                                            center.y + Mathf.Sin(angle) * radius, 0f);
                }
            }
            else if (triangle)
            {
                points = new Vector3[] {
                    new Vector3(center.x, center.y - radius, 0f),
                    new Vector3(center.x + radius, center.y + radius, 0f),
                    new Vector3(center.x - radius, center.y + radius, 0f),
                    new Vector3(center.x, center.y - radius, 0f)
                };
            }
            else if (slant)
            {
                points = new Vector3[] {
                    new Vector3(center.x - 5f, center.y - 4f, 0f),
                    new Vector3(center.x + 7f, center.y - 4f, 0f),
                    new Vector3(center.x + 5f, center.y + 4f, 0f),
                    new Vector3(center.x - 7f, center.y + 4f, 0f),
                    new Vector3(center.x - 5f, center.y - 4f, 0f)
                };
            }
            else
            {
                points = new Vector3[] {
                    new Vector3(center.x - radius, center.y - radius, 0f),
                    new Vector3(center.x + radius, center.y - radius, 0f),
                    new Vector3(center.x + radius, center.y + radius, 0f),
                    new Vector3(center.x - radius, center.y + radius, 0f),
                    new Vector3(center.x - radius, center.y - radius, 0f)
                };
            }
            Handles.BeginGUI();
            Color previous = Handles.color;
            Handles.color = tint;
            Handles.DrawAAPolyLine(1.7f, points);
            Handles.color = previous;
            Handles.EndGUI();
            return true;
        }

        private static string IconGlyph(Entry entry)
        {
            if (entry.icon == "Empty Dot") return "·";
            if (entry.icon == "Plus Icon") return "+";
            if (entry.icon == "Audio Icon") return "♫";
            if (entry.icon == "Scene Icon") return "▧";
            if (entry.icon == "Search Icon") return "⌕";
            if (entry.icon == "Folder Icon") return "▸";
            if (entry.icon == "Prefab Icon") return "◇";
            if (entry.icon == "Camera Icon") return "◉";
            if (entry.icon == "Light Icon") return "✳";
            if (entry.icon == "ParticleSystem Icon") return "✦";
            if (entry.icon == "Canvas Icon") return "▣";
            if (entry.label == "Cube") return "□";
            if (entry.label == "Icosphere" || entry.label == "Torus") return "○";
            if (entry.label == "Cone") return "△";
            if (entry.label == "Capsule") return "○";
            if (entry.label == "Cylinder") return "○";
            if (entry.label == "Plane" || entry.label == "Quad") return "▱";
            if (entry.icon == "GameObject Icon") return "◇";
            return entry.icon == "*" ? "✦" : entry.icon == "#" ? "▣" : "+";
        }

        private string PageTitle()
        {
            switch (page) { case "prefabs": return "My Prefabs"; case "objects": return "3D Objects"; case "scene": return "Scene Objects"; default: return page.StartsWith("prefabs/", StringComparison.Ordinal) ? page.Substring(page.LastIndexOf('/') + 1) : "Lights"; }
        }

        private static Texture2D Solid(Color color)
        {
            Texture2D t = new Texture2D(1, 1);
            t.hideFlags = HideFlags.HideAndDontSave;
            t.SetPixel(0, 0, color);
            t.Apply();
            return t;
        }
    }
}
