// CuttlefishSceneActionMenu.cs — Unity 6.3 Editor
// Put in Assets/Editor. Space in Scene View; Tools > Cuttlefish Action Menu (Space).
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Cuttlefish.Editor
{
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

    // Match the actual row layout exactly. A 160-point native window around
    // 147 points of content exposed Unity's grey background below the menu.
    private const float PopupWidth = 192f;
    private const float PopupHeight = 147f;

    // The Unity _SPACE menu accelerator can run before SceneGUI receives KeyDown.
    // Cache the cursor while SceneGUI coordinates are valid so BOTH shortcut
    // paths open at the cursor instead of one jumping to Scene View center.
    private static SceneView cursorScene;
    private static Vector2 cursorPopupPosition;
    private static bool hasSceneCursor;

    static CuttlefishSceneActionMenu() { SceneView.duringSceneGui += SceneGUI; }

    private static void SceneGUI(SceneView view)
    {
        Event e = Event.current;
        if (e == null) return;

        // Keep the latest cursor anchor. In particular, Unity may dispatch
        // the _SPACE MenuItem before it dispatches this window's KeyDown.
        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag ||
            e.type == EventType.MouseDown || e.type == EventType.MouseUp ||
            (e.type == EventType.Repaint && EditorWindow.mouseOverWindow == view) ||
            (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space))
        {
            Rect sceneArea = new Rect(0f, 0f, view.position.width, view.position.height);
            if (sceneArea.Contains(e.mousePosition))
            {
                cursorScene = view;
                cursorPopupPosition = FindSpaceOpeningPoint(view,
                    GUIUtility.GUIToScreenPoint(e.mousePosition));
                hasSceneCursor = true;
            }
        }

        if (e.type != EventType.KeyDown || e.keyCode != KeyCode.Space ||
            e.alt || e.shift || e.control || e.command || EditorGUIUtility.editingTextField ||
            GUIUtility.hotControl != 0) return;
        Vector2 point = FindSpaceOpeningPoint(view, GUIUtility.GUIToScreenPoint(e.mousePosition));
        cursorScene = view;
        cursorPopupPosition = point;
        hasSceneCursor = true;
        e.Use();
        // Repeated Space presses are ignored while the Action Menu is open.
        if (openPopup != null) return;
        ShowPopupAt(point, view);
    }

    private static void ShowPopupAt(Vector2 screenPoint, SceneView view)
    {
        if (view == null || openPopup != null) return;
        ActionPopup popup = ScriptableObject.CreateInstance<ActionPopup>();
        popup.Initialize(view);
        // EditorWindow.position uses screen coordinates. Anchor the popup's
        // top-left corner directly at the mouse position, like Shift+A.
        popup.position = new Rect(screenPoint.x, screenPoint.y, PopupWidth, PopupHeight);
        openPopup = popup;
        popup.ShowPopup();
        popup.Focus();
    }

    // Same Scene View screen-space bounds logic as the working Shift+A menu.
    // The Space popup has only one column, so no secondary-column reservation.
    private static Vector2 FindSpaceOpeningPoint(SceneView view, Vector2 cursorScreen)
    {
        const float popupWidth = PopupWidth;
        const float popupHeight = PopupHeight;
        const float edgeMargin = 8f;
        Vector2 sceneOrigin = GUIUtility.GUIToScreenPoint(Vector2.zero);
        Rect bounds = new Rect(sceneOrigin.x, sceneOrigin.y,
                               view.position.width, view.position.height);
        // Identical anchor rule to Add: cursor touches popup's upper-left.
        // Only shift the position when needed to keep the window on-screen.
        Vector2 point = cursorScreen;
        point.x = Mathf.Clamp(point.x, bounds.xMin + edgeMargin,
            Mathf.Max(bounds.xMin + edgeMargin, bounds.xMax - popupWidth - edgeMargin));
        point.y = Mathf.Clamp(point.y, bounds.yMin + edgeMargin,
            Mathf.Max(bounds.yMin + edgeMargin, bounds.yMax - popupHeight - edgeMargin));
        return point;
    }

    // Toolbar supplies a Scene GUI-space anchor.
    public static void ShowAtToolbarGUI(Rect guiAnchor, SceneView view)
    {
        ShowPopupAt(GUIUtility.GUIToScreenPoint(guiAnchor.position), view);
    }

    public static void ShowAtScreen(Vector2 screenPoint, SceneView view)
    {
        ShowPopupAt(screenPoint, view);
    }

    // Restore the Unity menu accelerator as a fallback for Space keyboard input.
    [MenuItem("Tools/Cuttlefish Action Menu _SPACE")]
    private static void OpenFromTools()
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null || openPopup != null) return;
        // This Unity accelerator may fire instead of the SceneGUI shortcut.
        // Reuse the last SceneGUI cursor position rather than the old center
        // fallback (which was also in dock-relative, not screen, coordinates).
        Vector2 point = hasSceneCursor && cursorScene == view
            ? cursorPopupPosition
            : view.position.center;
        ShowPopupAt(point, view);
    }

    // Incrementing this also cancels any already-queued focus restoration.
    private static int focusRequestVersion;

    // A different popup is taking over: close Space but NEVER refocus the Scene View.
    public static void CloseForOtherMenu() { ClosePopup(false); }

    private static void ClosePopup(bool restoreSceneFocus = true)
    {
        int request = ++focusRequestVersion;
        ActionPopup popup = openPopup;
        if (popup == null) return;
        openPopup = null;
        SceneView view = popup.Scene;
        popup.Close();
        if (!restoreSceneFocus) return;

        // Normal Space/Escape closes still return focus to the Scene View.
        // However, if Add has opened in the meantime, do not steal its focus.
        EditorApplication.delayCall += () =>
        {
            if (request == focusRequestVersion && openPopup == null &&
                !CuttlefishAddMenu.IsOpen && view != null)
                view.Focus();
        };
    }

    private sealed class ActionPopup : EditorWindow
    {
        private SceneView view;
        internal SceneView Scene { get { return view; } }
        private GUIStyle textStyle;
        private Texture2D bg, highlight, selected;
        private bool pending;
        private const float Width = 192f;
        private const float Row = 23f;
        private const float Gap = 3f;
        private const float Pad = 6f;
        internal void Initialize(SceneView sceneView) { view = sceneView; }
        private void OnDisable()
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
        private void OnLostFocus()
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null && openPopup == this && EditorWindow.focusedWindow != this)
                    ClosePopup();
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
        private void OnGUI()
        {
            Init();
            Event e = Event.current;
            // Space is reserved for this menu while it is open; do not toggle it.
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space &&
                !e.shift && !e.alt && !e.control && !e.command)
            {
                e.Use();
                return;
            }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                e.Use();
                ClosePopup();
                return;
            }
            // Same three-layer treatment as Shift+A: opaque background,
            // rounded surface, then a 1-point neutral border on all edges.
            Rect windowRect = new Rect(0f, 0f, position.width, position.height);
            EditorGUI.DrawRect(windowRect, Background);
            DrawNineSlice(windowRect, bg, 6f);
            DrawWindowBorder(windowRect);
            float y = Pad;
            DrawPair(0, 1, y); y += Row + Gap;
            DrawPair(2, 3, y); y += Row + 4;
            Divider(y); y += 3;
            DrawFull(4, y); y += Row + Gap;
            DrawFull(5, y); y += Row + 4;
            Divider(y); y += 3;
            DrawFull(6, y);
        }
        // Same pixel and color values as CuttlefishAddMenu.DrawWindowBorder.
        private static void DrawWindowBorder(Rect r)
        {
            Color edge = new Color(.39f, .40f, .42f, 1f);
            EditorGUI.DrawRect(new Rect(0f, 0f, r.width, 1f), edge);
            EditorGUI.DrawRect(new Rect(0f, r.height - 1f, r.width, 1f), edge);
            EditorGUI.DrawRect(new Rect(0f, 0f, 1f, r.height), edge);
            EditorGUI.DrawRect(new Rect(r.width - 1f, 0f, 1f, r.height), edge);
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
                    if (this != null) Repaint();
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
}

#endif
