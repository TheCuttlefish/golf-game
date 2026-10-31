// CuttlefishSceneActionMenu.cs — Unity 6.3 Editor
// Put in Assets/Editor. Space in Scene View; Tools > Cuttlefish Action Menu (Space).
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CuttlefishSceneActionMenu
{
    private static readonly Color Background = new Color(.105f, .108f, .12f);
    private static readonly Color Hover = new Color(.34f, .35f, .38f);
    private static readonly Color Active = new Color(.27f, .29f, .32f);
    private static readonly Color Green = new Color(.57f, .81f, .62f);
    private static readonly Color Purple = new Color(.80f, .64f, .90f);
    private static readonly Color Cyan = new Color(.56f, .81f, .83f);
    private static readonly Color Amber = new Color(.87f, .72f, .49f);
    private static ActionPopup openPopup;

    static CuttlefishSceneActionMenu() { SceneView.duringSceneGui += SceneGUI; }

    private static void SceneGUI(SceneView view)
    {
        Event e = Event.current;
        if (e == null || e.type != EventType.KeyDown || e.keyCode != KeyCode.Space ||
            e.alt || e.shift || e.control || e.command || EditorGUIUtility.editingTextField ||
            GUIUtility.hotControl != 0) return;
        // Exactly the same GUI-local anchor approach as CuttlefishAddMenu's Shift+A.
        Vector2 at = e.mousePosition;
        e.Use();
        if (openPopup != null) { ClosePopup(); return; }
        Rect anchor = new Rect(at.x, at.y, 1, 1);
        EditorApplication.delayCall += () =>
        {
            if (view != null) PopupWindow.Show(anchor, new ActionPopup(view));
        };
    }

    [MenuItem("Tools/Cuttlefish Action Menu _SPACE")]
    private static void OpenFromTools()
    {
        if (openPopup != null) { ClosePopup(); return; }
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null) return;
        // Menu command has no mouse event; use Scene View center as fallback only.
        Rect r = view.position;
        Rect anchor = new Rect(r.center.x, r.center.y, 1, 1);
        EditorApplication.delayCall += () =>
        {
            if (view != null) PopupWindow.Show(anchor, new ActionPopup(view));
        };
    }

    private static void ClosePopup()
    {
        ActionPopup popup = openPopup;
        openPopup = null;
        if (popup == null || popup.editorWindow == null) return;
        EditorWindow window = popup.editorWindow;
        EditorApplication.delayCall += () => { if (window != null) window.Close(); };
    }

    private sealed class ActionPopup : PopupWindowContent
    {
        private readonly SceneView view;
        private GUIStyle textStyle;
        private Texture2D bg, highlight, selected;
        private bool pending;
        private const float Width = 192f;
        private const float Row = 23f;
        private const float Gap = 3f;
        private const float Pad = 6f;
        // 2 rows + separator + 2 rows + separator + 1 row, with exact padding.
        private const float Height = 6f + 23f + 3f + 23f + 7f + 23f + 3f + 23f + 7f + 23f + 6f;
        public ActionPopup(SceneView sceneView) { view = sceneView; openPopup = this; }
        public override Vector2 GetWindowSize() { return new Vector2(Width, Height); }
        public override void OnClose()
        {
            if (openPopup == this) openPopup = null;
            Texture2D a = bg, b = highlight, c = selected;
            bg = highlight = selected = null;
            EditorApplication.delayCall += () =>
            {
                if (a != null) Object.DestroyImmediate(a);
                if (b != null) Object.DestroyImmediate(b);
                if (c != null) Object.DestroyImmediate(c);
            };
        }
        private void Init()
        {
            if (textStyle != null) return;
            textStyle = new GUIStyle(EditorStyles.label) { fontSize = 11, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            textStyle.normal.textColor = new Color(.91f, .91f, .91f);
            bg = MakeRounded(Background, 6);
            highlight = MakeRounded(Hover, 4);
            selected = MakeRounded(Active, 4);
        }
        public override void OnGUI(Rect rect)
        {
            Init();
            Event e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Escape || (e.keyCode == KeyCode.Space && !e.shift && !e.alt && !e.control && !e.command)))
            {
                e.Use();
                ClosePopup();
                return;
            }
            DrawNineSlice(new Rect(0, 0, Width, Height), bg, 6);
            float y = Pad;
            DrawPair(0, 1, y); y += Row + Gap;
            DrawPair(2, 3, y); y += Row + 4;
            Divider(y); y += 3;
            DrawFull(4, y); y += Row + Gap;
            DrawFull(5, y); y += Row + 4;
            Divider(y); y += 3;
            DrawFull(6, y);
        }
        private void Divider(float y)
        {
            EditorGUI.DrawRect(new Rect(Pad + 2, y, Width - 2 * Pad - 4, 1), new Color(.30f, .30f, .32f, .85f));
        }
        private void DrawPair(int a, int b, float y)
        {
            float w = (Width - 2 * Pad - Gap) / 2;
            DrawItem(a, new Rect(Pad, y, w, Row));
            DrawItem(b, new Rect(Pad + w + Gap, y, w, Row));
        }
        private void DrawFull(int i, float y) { DrawItem(i, new Rect(Pad, y, Width - 2 * Pad, Row)); }
        private void DrawItem(int i, Rect r)
        {
            string[] names = { "Center", "Pivot", "Global", "Local", "Move to View", "Align with View", "Center on Children" };
            string[] icons = { "ToolHandleCenter", "ToolHandlePivot", "ToolHandleGlobal", "ToolHandleLocal", "MoveTool", "RotateTool", "TransformTool" };
            Color tint = i < 2 ? Green : i < 4 ? Purple : i < 6 ? Cyan : Amber;
            bool isActive = (i == 0 && Tools.pivotMode == PivotMode.Center) ||
                (i == 1 && Tools.pivotMode == PivotMode.Pivot) ||
                (i == 2 && Tools.pivotRotation == PivotRotation.Global) ||
                (i == 3 && Tools.pivotRotation == PivotRotation.Local);
            Event e = Event.current;
            bool isHover = r.Contains(e.mousePosition);
            if (isHover) DrawNineSlice(r, highlight, 4);
            else if (isActive) DrawNineSlice(r, selected, 4);
            Texture icon = EditorGUIUtility.FindTexture(icons[i]);
            Rect ir = new Rect(r.x + 6, r.y + 4, 15, 15);
            if (icon != null)
            {
                Color old = GUI.color;
                GUI.color = isHover ? Color.white : tint;
                GUI.DrawTexture(ir, icon, ScaleMode.ScaleToFit, true);
                GUI.color = old;
            }
            else EditorGUI.DrawRect(new Rect(ir.x + 5, ir.y + 5, 5, 5), tint);
            textStyle.normal.textColor = isHover ? Color.white : new Color(.91f, .91f, .91f);
            GUI.Label(new Rect(r.x + 26, r.y + 1, r.width - 29, Row - 2), names[i], textStyle);
            if (isHover && e.type == EventType.MouseDown && e.button == 0 && !pending)
            {
                pending = true;
                int action = i;
                // Defer scene changes and popup closure outside IMGUI to avoid ExitGUIException.
                EditorApplication.delayCall += () =>
                {
                    Perform(action, view);
                    pending = false;
                    if (editorWindow != null) editorWindow.Repaint();
                };
                e.Use();
            }
        }
        private static Texture2D MakeRounded(Color color, int radius)
        {
            const int size = 24;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + .5f, radius, size - radius);
                float cy = Mathf.Clamp(y + .5f, radius, size - radius);
                float alpha = Mathf.Clamp01(radius + .5f - Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy)));
                tex.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * alpha));
            }
            tex.Apply();
            return tex;
        }
        private static void DrawNineSlice(Rect r, Texture2D tex, float corner)
        {
            float c = Mathf.Min(corner, r.width / 2f, r.height / 2f);
            float[] xs = { r.x, r.x + c, r.xMax - c, r.xMax };
            float[] ys = { r.y, r.y + c, r.yMax - c, r.yMax };
            float uv = corner / tex.width;
            float[] u = { 0, uv, 1 - uv, 1 };
            for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
                GUI.DrawTextureWithTexCoords(new Rect(xs[x], ys[y], xs[x + 1] - xs[x], ys[y + 1] - ys[y]),
                    tex, new Rect(u[x], 1 - u[y + 1], u[x + 1] - u[x], u[y + 1] - u[y]));
        }
    }
    private static void Perform(int i, SceneView view)
    {
        if (view == null) return;
        Transform t = Selection.activeTransform;
        switch (i)
        {
            case 0: Tools.pivotMode = PivotMode.Center; break;
            case 1: Tools.pivotMode = PivotMode.Pivot; break;
            case 2: Tools.pivotRotation = PivotRotation.Global; break;
            case 3: Tools.pivotRotation = PivotRotation.Local; break;
            case 4:
                if (t != null && view.camera != null)
                {
                    Undo.RecordObject(t, "Move to View");
                    t.position = view.camera.transform.position;
                }
                break;
            case 5:
                if (t != null)
                {
                    Undo.RecordObject(t, "Align with View");
                    t.rotation = view.rotation;
                }
                break;
            case 6:
                if (t != null && t.childCount > 0)
                {
                    int count = t.childCount;
                    Vector3 average = Vector3.zero;
                    Transform[] children = new Transform[count];
                    Vector3[] positions = new Vector3[count];
                    Quaternion[] rotations = new Quaternion[count];
                    for (int j = 0; j < count; j++)
                    {
                        children[j] = t.GetChild(j);
                        average += children[j].position;
                        positions[j] = children[j].position;
                        rotations[j] = children[j].rotation;
                    }
                    average /= count;
                    Undo.RecordObject(t, "Center on Children");
                    for (int j = 0; j < count; j++) Undo.RecordObject(children[j], "Preserve Child Position");
                    t.position = average;
                    for (int j = 0; j < count; j++) children[j].SetPositionAndRotation(positions[j], rotations[j]);
                }
                break;
        }
        SceneView.RepaintAll();
    }
}
#endif
