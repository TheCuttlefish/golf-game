// Blender-style modal transforms for Unity 6.3 LTS.
// Place this file inside Assets/Editor/.
using UnityEngine;
using UnityEditor;
using System.Globalization;

[InitializeOnLoad]
public static class BlenderTransformTool
{
    private enum Mode { None, Move, Rotate, Scale, Trackball }
    private static Mode mode = Mode.None;
    private static bool enabledMode;
    private static Transform target;
    private static Transform[] targets = System.Array.Empty<Transform>();
    private static Vector3[] startPositions, startScales;
    private static Quaternion[] startRotations;
    private static Vector3 groupCenter;
    private static PivotMode pivotModeAtStart;
    private static PivotRotation pivotRotationAtStart;
    private static bool duplicating;
    private static GameObject[] originalSelections;
    private static GameObject[] duplicatedObjects;
    private static Vector3 startPosition, startScale;
    private static Quaternion startRotation;
    private static Vector2 startMouse, lastMouse;
    // Virtual cursor accumulates fine motion while Shift is held, without jumps.
    private static Vector2 precisionMouse, previousPhysicalMouse;
    private const float PrecisionFactor = 0.15f;
    private static Vector2 FineMouse(Vector2 physical) => precisionMouse;

    private static Vector2 pivotScreen;
    private static char axis;
    private static bool moveLocalAxis;
    private static bool rotateLocalAxis;
    private static float startRadius;
    private static Vector3 startPlanePoint;
    private static Vector3 movePlaneNormal;
    private static Vector3 startPlaneHit;
    private static float rotationAngle;
    private static float accumulatedMouseAngle;
    private static float freeArcZeroAngle;
    private static Vector2 previousRotationVector;
    private static bool rotationVectorReady;
    private static Quaternion trackballDelta;
    private static Quaternion trackballCameraRotation;
    // Read Unity's Edit > Grid and Snap Settings values live.
    private static Vector3 MoveSnap => EditorSnapSettings.move;
    private static float RotateSnap => Mathf.Max(0.0001f, EditorSnapSettings.rotate);
    private static float ScaleSnap => Mathf.Max(0.0001f, EditorSnapSettings.scale);
    private static string numericInput = "";
    private static GUIStyle panelStyle;
    private static bool ctrlHeld;
    private static bool moveSnapAnchorSet;
    private static Vector3 moveSnapAnchor;

    // Temporary Scene View display override for G -> B vertex snapping.
    private static SceneView vertexWireScene;
    private static SceneView.CameraMode vertexPreviousCameraMode;
    private static bool vertexWireActive;

    private static void EnableVertexWireframe(SceneView scene)
    {
        if (vertexWireActive || scene == null) return;
        vertexWireScene = scene;
        vertexPreviousCameraMode = scene.cameraMode;
        vertexWireActive = true;
        SceneView.CameraMode wireMode = SceneView.GetBuiltinCameraMode(DrawCameraMode.TexturedWire);
        scene.cameraMode = wireMode;
        scene.Repaint();
    }

    private static void RestoreVertexWireframe()
    {
        if (!vertexWireActive) return;
        if (vertexWireScene != null)
        {
            vertexWireScene.cameraMode = vertexPreviousCameraMode;
            vertexWireScene.Repaint();
        }
        vertexWireActive = false;
        vertexWireScene = null;
    }

    private static bool vertexSnapMode;
    private static Vector3 vertexSnapSource;
    private static bool vertexSourceChosen;
    private static bool vertexDestinationFound;
    private static Vector3 vertexDestination;
    private static bool rightMouseHeld;
    private static bool previousToolsHidden;
    private const string PrefKey = "BlenderTransformTool.Enabled";
    private const string PanelXKey = "BlenderTransformTool.PanelOffsetX";
    private const string PanelYKey = "BlenderTransformTool.PanelOffsetY";
    private static Vector2 panelOffset;
    private static bool draggingPanel;
    private static Vector2 dragMouseStart, dragOffsetStart;

    static BlenderTransformTool()
    {
        enabledMode = EditorPrefs.GetBool(PrefKey, false);
        panelOffset = new Vector2(EditorPrefs.GetFloat(PanelXKey, 0f),
                                  EditorPrefs.GetFloat(PanelYKey, 0f));
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private static void OnSceneGUI(SceneView scene)
    {
        Event e = Event.current;
        if (e == null) return;

        if (e.type == EventType.MouseDown && e.button == 1 && mode == Mode.None) rightMouseHeld = true;
        if (e.type == EventType.MouseUp && e.button == 1) rightMouseHeld = false;

        // F1 works even when Blender Mode is disabled, provided Scene view has focus.
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F1 && !e.alt && !e.control && !e.command)
        {
            if (mode != Mode.None) Cancel();
            enabledMode = !enabledMode;
            EditorPrefs.SetBool(PrefKey, enabledMode);
            e.Use();
            scene.Repaint();
            return;
        }
        if (!enabledMode) return;
        if (HandlePanelDrag(e, scene))
        {
            DrawOverlay(scene);
            return;
        }

        // Preserve Unity's right-mouse fly navigation and Alt camera controls.
        if (mode == Mode.None && (rightMouseHeld || (e.alt && !(e.type == EventType.KeyDown && (e.keyCode == KeyCode.G || e.keyCode == KeyCode.R || e.keyCode == KeyCode.S)))))
        {
            DrawOverlay(scene);
            return;
        }

        if (mode == Mode.None)
        {
            // Blender object selection shortcuts (Scene view only).
            if (e.type == EventType.KeyDown && !e.shift && !e.alt &&
                ((e.keyCode == KeyCode.A && !e.control && !e.command) ||
                 (e.keyCode == KeyCode.I && (e.control || e.command))))
            {
                var all = new System.Collections.Generic.List<GameObject>();
                var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (activeScene.IsValid() && activeScene.isLoaded)
                {
                    foreach (GameObject root in activeScene.GetRootGameObjects())
                        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                            if (t && (t.gameObject.hideFlags & (HideFlags.HideInHierarchy | HideFlags.NotEditable)) == 0)
                                all.Add(t.gameObject);
                }
                if (e.keyCode == KeyCode.A)
                    Selection.objects = Selection.gameObjects.Length > 0
                        ? System.Array.Empty<Object>()
                        : all.ConvertAll(go => (Object)go).ToArray();
                else
                {
                    var selected = new System.Collections.Generic.HashSet<GameObject>(Selection.gameObjects);
                    Selection.objects = all.FindAll(go => !selected.Contains(go))
                        .ConvertAll(go => (Object)go).ToArray();
                }
                e.Use();
                scene.Repaint();
                return;
            }

            // Handle Alt resets only while Blender Mode is enabled. Unity/OS may
            // still reserve some Alt combinations before SceneGUI receives them.
            if (e.type == EventType.KeyDown && e.alt && !e.control && !e.command &&
                (e.keyCode == KeyCode.G || e.keyCode == KeyCode.R || e.keyCode == KeyCode.S) &&
                Selection.activeTransform != null)
            {
                Transform t = Selection.activeTransform;
                Undo.RecordObject(t, "Blender Reset " + e.keyCode);
                if (e.keyCode == KeyCode.G) t.localPosition = Vector3.zero;
                else if (e.keyCode == KeyCode.R) t.localRotation = Quaternion.identity;
                else t.localScale = Vector3.one;
                EditorUtility.SetDirty(t);
                e.Use();
                scene.Repaint();
            }
            else if (e.type == EventType.KeyDown && e.shift && !e.alt && !e.control && !e.command &&
                e.keyCode == KeyCode.D && Selection.activeTransform != null)
            {
                DuplicateAndMove(e, scene);
                e.Use();
            }
            else if (e.type == EventType.KeyDown && !e.alt && !e.control && !e.command &&
                (e.keyCode == KeyCode.G || e.keyCode == KeyCode.R || e.keyCode == KeyCode.S) &&
                Selection.activeTransform != null)
            {
                Begin(e, scene);
                e.Use();
            }
        }
        else
        {
            if (ctrlHeld != (e.control || e.command))
            {
                ctrlHeld = e.control || e.command;
                if (ctrlHeld) CaptureMoveSnapAnchor();
                else ResetMoveSnapMarkers();
                UpdateTransform(FineMouse(e.mousePosition), scene);
            }
            // Capture input so Unity's built-in selection/handles don't interfere.
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if (e.type == EventType.KeyDown || e.type == EventType.KeyUp)
            {
                if (e.keyCode == KeyCode.LeftControl || e.keyCode == KeyCode.RightControl ||
                    e.keyCode == KeyCode.LeftCommand || e.keyCode == KeyCode.RightCommand)
                {
                    ctrlHeld = e.type == EventType.KeyDown;
                    if (ctrlHeld) CaptureMoveSnapAnchor();
                    else ResetMoveSnapMarkers();
                    UpdateTransform(FineMouse(e.mousePosition), scene);
                    e.Use();
                }
            }
            if (e.type == EventType.KeyDown && e.alt)
            {
                // Alt belongs to Scene navigation; never invoke editor transform shortcuts.
                // Consume Alt+G/R/S while transforming to prevent legacy hotkey conflicts.
                if (e.keyCode == KeyCode.G || e.keyCode == KeyCode.R || e.keyCode == KeyCode.S) e.Use();
            }
            else if (e.type == EventType.KeyDown && e.keyCode != KeyCode.LeftControl &&
                e.keyCode != KeyCode.RightControl && e.keyCode != KeyCode.LeftCommand &&
                e.keyCode != KeyCode.RightCommand)
            {
                if (e.keyCode == KeyCode.Escape) { Cancel(); e.Use(); }
                else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                { Confirm(); e.Use(); }
                else if (e.keyCode == KeyCode.Backspace && numericInput.Length > 0)
                { numericInput = numericInput.Substring(0, numericInput.Length - 1); UpdateTransform(FineMouse(e.mousePosition), scene); e.Use(); }
                else if (TryAppendNumber(e))
                { UpdateTransform(FineMouse(e.mousePosition), scene); e.Use(); }
                else if (numericInput.Length == 0 && (e.keyCode == KeyCode.X || e.keyCode == KeyCode.Y || e.keyCode == KeyCode.Z))
                {
                    char requested = e.keyCode == KeyCode.X ? 'X' : e.keyCode == KeyCode.Y ? 'Y' : 'Z';
                    bool repeatedMoveAxis = mode == Mode.Move && axis == requested && !e.shift;
                    if (mode == Mode.Move)
                        moveLocalAxis = repeatedMoveAxis ? !moveLocalAxis : false;
                    bool repeatedRotateAxis = mode == Mode.Rotate && axis == requested && !e.shift;
                    bool nextRotateLocal = repeatedRotateAxis ? !rotateLocalAxis : false;
                    char newAxis = mode == Mode.Move || mode == Mode.Rotate ? requested :
                        (axis == requested && planarScale == e.shift ? '\0' : requested);
                    planarScale = mode == Mode.Scale && e.shift && newAxis != '\0';
                    if (mode == Mode.Rotate || mode == Mode.Trackball)
                    {
                        // Preserve the current orientation and reset the dial's baseline
                        // before changing the rotation plane.
                        RebaseDuplicateOperation(e.mousePosition, scene, Mode.Rotate);
                        rotateLocalAxis = nextRotateLocal;
                    }
                    axis = newAxis;
                    lastMouse = FineMouse(e.mousePosition);
                    UpdateTransform(FineMouse(e.mousePosition), scene);
                    e.Use();
                }
                else if (e.keyCode == KeyCode.G || e.keyCode == KeyCode.R || e.keyCode == KeyCode.S)
                {
                    Mode next = e.keyCode == KeyCode.G ? Mode.Move :
                                e.keyCode == KeyCode.R ? Mode.Rotate : Mode.Scale;
                    if (e.keyCode == KeyCode.R && (mode == Mode.Rotate || mode == Mode.Trackball))
                    {
                        // Rebase before toggling so the current preview is preserved.
                        Mode nextRotation = mode == Mode.Rotate ? Mode.Trackball : Mode.Rotate;
                        RebaseDuplicateOperation(e.mousePosition, scene, nextRotation);
                    }
                    else if (mode != next)
                        RebaseDuplicateOperation(e.mousePosition, scene, next);
                    e.Use();
                }
                else if (e.keyCode == KeyCode.B && mode == Mode.Move && numericInput.Length == 0)
                {
                    vertexSnapMode = !vertexSnapMode;
                    if (vertexSnapMode) EnableVertexWireframe(scene);
                    else RestoreVertexWireframe();
                    vertexSourceChosen = false;
                    vertexDestinationFound = false;
                    axis = '\0';
        moveLocalAxis = false;
        rotateLocalAxis = false;
        planarScale = false;
                    if (vertexSnapMode)
                    {
                        // Start the pick from the original position, not the
                        // temporary offset already produced by G.
                        for (int i = 0; i < targets.Length; i++)
                            if (targets[i]) targets[i].position = startPositions[i];
                    }
                    else UpdateTransform(FineMouse(e.mousePosition), scene);
                    e.Use();
                }
                else { e.Use(); } // Suppress other Scene View shortcuts during a modal transform.
            }
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                Vector2 physicalDelta = e.mousePosition - previousPhysicalMouse;
                previousPhysicalMouse = e.mousePosition;
                precisionMouse += physicalDelta * (e.shift && mode != Mode.Rotate && mode != Mode.Trackball ? PrecisionFactor : 1f);
            }
            if ((e.type == EventType.MouseMove || e.type == EventType.MouseDrag) && numericInput.Length == 0)
            {
                if (mode == Mode.Trackball)
                {
                    // Pivot-centered directional R -> R. Interpret the mouse position
                    // relative to the projected center of the selected model, rather
                    // than using a screen-centered / fixed-pixel rotation scale.
                    // Subtract the initial pivot-relative angles so entering trackball
                    // never causes a sudden rotation jump.
                    Vector2 pivot = HandleUtility.WorldToGUIPoint(RotationPivot());
                    Vector2 fromCenter = FineMouse(e.mousePosition) - pivot;
                    Vector2 initialFromCenter = startMouse - pivot;
                    // Radius adapts to the model's on-screen position and the cursor's
                    // initial offset. Clamp it to keep movement predictable nearby.
                    float radius = Mathf.Max(90f, initialFromCenter.magnitude);
                    float horizontal = (Mathf.Atan2(fromCenter.x, radius) -
                                        Mathf.Atan2(initialFromCenter.x, radius)) * Mathf.Rad2Deg;
                    float vertical = (Mathf.Atan2(fromCenter.y, radius) -
                                      Mathf.Atan2(initialFromCenter.y, radius)) * Mathf.Rad2Deg;
                    Quaternion yaw = Quaternion.AngleAxis(-horizontal,
                                                           trackballCameraRotation * Vector3.up);
                    Quaternion pitch = Quaternion.AngleAxis(-vertical,
                                                             trackballCameraRotation * Vector3.right);
                    trackballDelta = yaw * pitch;
                    lastMouse = FineMouse(e.mousePosition);
                }
                if (!vertexSnapMode || vertexSourceChosen) UpdateTransform(FineMouse(e.mousePosition), scene);
                e.Use();
            }
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (mode == Mode.Move && vertexSnapMode && !vertexSourceChosen)
                {
                    if (TryPickSourceVertex(e.mousePosition, out Vector3 source))
                    {
                        vertexSnapSource = source;
                        vertexSourceChosen = true;
                        vertexDestinationFound = false;
                        UpdateTransform(FineMouse(e.mousePosition), scene);
                    }
                    e.Use();
                }
                else { Confirm(); e.Use(); }
            }
            else if (e.type == EventType.MouseDown && e.button == 1) { Cancel(); e.Use(); }
        }

        DrawOverlay(scene);
        if (mode != Mode.None) scene.Repaint();
    }

    private static void DuplicateAndMove(Event e, SceneView scene)
    {
        Transform[] selected = Selection.transforms;
        if (selected.Length == 0) return;

        // Only duplicate selection roots, so a selected parent does not
        // cause its selected children to be duplicated a second time.
        var roots = new System.Collections.Generic.List<Transform>();
        foreach (Transform t in selected)
        {
            bool ancestorSelected = false;
            for (Transform p = t.parent; p != null; p = p.parent)
            {
                if (System.Array.IndexOf(selected, p) >= 0)
                {
                    ancestorSelected = true;
                    break;
                }
            }
            if (!ancestorSelected) roots.Add(t);
        }

        originalSelections = Selection.gameObjects;
        var copies = new System.Collections.Generic.List<GameObject>();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Blender Duplicate");

        foreach (Transform source in roots)
        {
            GameObject copy;
            // Keep prefab instances linked to their source where possible.
            if (PrefabUtility.IsPartOfPrefabInstance(source.gameObject) &&
                PrefabUtility.IsAnyPrefabInstanceRoot(source.gameObject))
            {
                GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(source.gameObject);
                copy = prefab ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, source.gameObject.scene) : null;
                if (copy)
                {
                    copy.transform.SetParent(source.parent, false);
                    copy.transform.localPosition = source.localPosition;
                    copy.transform.localRotation = source.localRotation;
                    copy.transform.localScale = source.localScale;
                }
            }
            else copy = null;

            if (!copy) copy = Object.Instantiate(source.gameObject, source.parent);
            copy.name = source.gameObject.name;
            copy.transform.SetSiblingIndex(source.GetSiblingIndex() + 1);
            Undo.RegisterCreatedObjectUndo(copy, "Blender Duplicate");
            copies.Add(copy);
        }

        if (copies.Count == 0) return;
        duplicatedObjects = copies.ToArray();
        Selection.objects = System.Array.ConvertAll(duplicatedObjects, go => (Object)go);
        duplicating = true;
        Begin(e, scene);
        mode = Mode.Move;
        UpdateTransform(FineMouse(e.mousePosition), scene);
        Undo.CollapseUndoOperations(undoGroup);
    }

    private static void Begin(Event e, SceneView scene)
    {
        RestoreVertexWireframe();
        target = Selection.activeTransform;
        if (!target) return;
        mode = e.keyCode == KeyCode.G ? Mode.Move : e.keyCode == KeyCode.R ? Mode.Rotate : Mode.Scale;
        axis = '\0';
        moveLocalAxis = false;
        rotateLocalAxis = false;
        planarScale = false;
        vertexSnapMode = false;
        vertexSourceChosen = false;
        vertexDestinationFound = false;
        numericInput = "";
        ctrlHeld = e.control || e.command;
        ResetMoveSnapMarkers();
        // Capture all selected roots, avoiding transforming selected children twice.
        var selected = Selection.transforms;
        var roots = new System.Collections.Generic.List<Transform>();
        foreach (Transform t in selected)
        {
            bool ancestorSelected = false;
            for (Transform parent = t.parent; parent != null; parent = parent.parent)
                if (System.Array.IndexOf(selected, parent) >= 0) { ancestorSelected = true; break; }
            if (!ancestorSelected) roots.Add(t);
        }
        targets = roots.ToArray();
        startPositions = new Vector3[targets.Length];
        startRotations = new Quaternion[targets.Length];
        startScales = new Vector3[targets.Length];
        groupCenter = Vector3.zero;
        for (int i = 0; i < targets.Length; i++)
        {
            startPositions[i] = targets[i].position;
            startRotations[i] = targets[i].rotation;
            startScales[i] = targets[i].localScale;
            groupCenter += startPositions[i];
        }
        if (targets.Length > 0) groupCenter /= targets.Length;
        pivotModeAtStart = Tools.pivotMode;
        pivotRotationAtStart = Tools.pivotRotation;
        startPosition = target.position;
        vertexSnapSource = startPosition;
        startRotation = target.rotation;
        startScale = target.localScale;
        startMouse = lastMouse = e.mousePosition;
        precisionMouse = previousPhysicalMouse = e.mousePosition;
        pivotScreen = HandleUtility.WorldToGUIPoint(pivotModeAtStart == PivotMode.Center ? groupCenter : startPosition);
        startRadius = Mathf.Max(12f, Vector2.Distance(startMouse, pivotScreen));
        rotationAngle = 0;
        accumulatedMouseAngle = 0f;
        freeArcZeroAngle = Mathf.Atan2(startMouse.y - pivotScreen.y, startMouse.x - pivotScreen.x);
        previousRotationVector = startMouse - pivotScreen;
        rotationVectorReady = previousRotationVector.sqrMagnitude > 16f;
        trackballDelta = Quaternion.identity;
        trackballCameraRotation = scene.camera.transform.rotation;
        movePlaneNormal = scene.camera.transform.forward;
        startPlanePoint = startPosition;
        startPlaneHit = MousePlaneHit(startMouse, startPlanePoint, movePlaneNormal, startPosition);
        previousToolsHidden = Tools.hidden;
        Tools.hidden = true;
    }

    private static bool TryPickSourceVertex(Vector2 mouse, out Vector3 vertex)
    {
        vertex = Vector3.zero;
        float bestSq = 18f * 18f;
        bool found = false;
        // Pick only from selected objects, unlike Unity's global destination
        // vertex lookup. This prevents accidentally choosing a background mesh.
        foreach (Transform root in targets)
        {
            if (!root) continue;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (!filter || !filter.sharedMesh) continue;
                Mesh mesh = filter.sharedMesh;
                Vector3[] vertices;
                try { vertices = mesh.vertices; }
                catch (System.Exception) { continue; } // non-readable meshes
                foreach (Vector3 local in vertices)
                {
                    Vector3 world = filter.transform.TransformPoint(local);
                    Vector2 screen = HandleUtility.WorldToGUIPoint(world);
                    float sq = (screen - mouse).sqrMagnitude;
                    if (sq < bestSq)
                    {
                        bestSq = sq;
                        vertex = world;
                        found = true;
                    }
                }
            }
        }
        return found;
    }

    private static Vector3 MousePlaneHit(Vector2 mouse, Vector3 point, Vector3 normal, Vector3 fallback)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
        Plane plane = new Plane(normal, point);
        return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : fallback;
    }

    private static Vector3 MoveAxisVector()
    {
        Vector3 direction = AxisVector();
        return moveLocalAxis ? (startRotation * direction).normalized : direction;
    }

    private static Vector3 AxisVector()
    {
        return axis == 'X' ? Vector3.right : axis == 'Y' ? Vector3.up : Vector3.forward;
    }

    private static Vector3 RotationPivot()
    {
        return pivotModeAtStart == PivotMode.Center ? groupCenter : startPosition;
    }

    private static Vector3 RotationPlaneNormal(SceneView scene)
    {
        Vector3 n = axis == '\0' ? scene.camera.transform.forward : AxisVector();
        if (axis != '\0' && rotateLocalAxis)
            n = startRotation * n;
        return n.normalized;
    }

    private static bool TryRotationPlaneVector(Vector2 mouse, Vector3 pivot, Vector3 normal, out Vector3 direction)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
        Plane plane = new Plane(normal, pivot);
        direction = Vector3.zero;
        if (Mathf.Abs(Vector3.Dot(ray.direction, normal)) < 0.07f ||
            !plane.Raycast(ray, out float distance)) return false;
        direction = Vector3.ProjectOnPlane(ray.GetPoint(distance) - pivot, normal);
        return direction.sqrMagnitude > 0.000001f;
    }

    private static Vector3 RotationDialZero(Vector3 normal, SceneView scene)
    {
        // Stable zero-angle axis shared by dial ticks, cursor tether and snapping.
        Vector3 zero = Vector3.ProjectOnPlane(scene.camera.transform.right, normal);
        if (zero.sqrMagnitude < 0.0001f)
            zero = Vector3.ProjectOnPlane(scene.camera.transform.up, normal);
        return zero.normalized;
    }

    // Shift + X/Y/Z constrains scale to the plane perpendicular to that axis.
    private static bool planarScale;

    private static Color RotationGuideColor(bool snapping)
    {
        if (rotateLocalAxis && mode == Mode.Rotate && axis != '\0')
        {
            Color baseColor = axis == 'X' ? new Color(0.96f, 0.31f, 0.31f, 1f) :
                              axis == 'Y' ? new Color(0.30f, 0.85f, 0.39f, 1f) :
                                            new Color(0.32f, 0.57f, 1f, 1f);
            return new Color(baseColor.r * 0.72f, baseColor.g * 0.72f, baseColor.b * 0.72f,
                snapping ? 1f : 0.9f);
        }
        if (axis == 'X') return snapping ? new Color(1f, 0.43f, 0.43f, 1f) : new Color(0.96f, 0.31f, 0.31f, 0.9f);
        if (axis == 'Y') return snapping ? new Color(0.42f, 1f, 0.53f, 1f) : new Color(0.30f, 0.85f, 0.39f, 0.9f);
        if (axis == 'Z') return snapping ? new Color(0.42f, 0.72f, 1f, 1f) : new Color(0.32f, 0.57f, 1f, 0.9f);
        return snapping && mode == Mode.Rotate ? Color.white :
               snapping ? new Color(0.35f, 0.94f, 0.48f, 0.95f) : new Color(1f, 0.72f, 0.32f, 0.85f);
    }

    private static void UpdateTransform(Vector2 mouse, SceneView scene)
    {
        if (!target) { Cancel(); return; }
        bool hasNumber = float.TryParse(numericInput, NumberStyles.Float, CultureInfo.InvariantCulture, out float number);
        if (mode == Mode.Scale)
        {
            float radius = Vector2.Distance(mouse, pivotScreen);
            float factor = Mathf.Max(0.001f, radius / startRadius);
            if (hasNumber) factor = number;
            else if (ctrlHeld) factor = Mathf.Max(0.001f, 1f + Mathf.Round((factor - 1f) / ScaleSnap) * ScaleSnap);
            Vector3 scale = startScale;
            if (axis == '\0') scale *= factor;
            else if (planarScale)
            {
                if (axis != 'X') scale.x *= factor;
                if (axis != 'Y') scale.y *= factor;
                if (axis != 'Z') scale.z *= factor;
            }
            else if (axis == 'X') scale.x *= factor;
            else if (axis == 'Y') scale.y *= factor;
            else scale.z *= factor;
            for (int i = 0; i < targets.Length; i++)
            {
                if (!targets[i]) continue;
                Vector3 f = new Vector3(
                    Mathf.Abs(startScale.x) > 0.000001f ? scale.x / startScale.x : factor,
                    Mathf.Abs(startScale.y) > 0.000001f ? scale.y / startScale.y : factor,
                    Mathf.Abs(startScale.z) > 0.000001f ? scale.z / startScale.z : factor);
                targets[i].localScale = Vector3.Scale(startScales[i], f);
                if (pivotModeAtStart == PivotMode.Center)
                    targets[i].position = groupCenter + Vector3.Scale(startPositions[i] - groupCenter, f);
            }
        }
        else if (mode == Mode.Move)
        {
            Vector3 movement = MousePlaneHit(mouse, startPlanePoint, movePlaneNormal, startPlaneHit) - startPlaneHit;
            if (axis != '\0')
            {
                // Project the mouse displacement onto the axis as viewed on screen.
                Vector3 a = MoveAxisVector();
                Vector2 screenAxis = HandleUtility.WorldToGUIPoint(startPosition + a) -
                                     HandleUtility.WorldToGUIPoint(startPosition);
                if (screenAxis.sqrMagnitude > 0.0001f)
                {
                    float worldPerScreenUnit = 1f / screenAxis.magnitude;
                    movement = a * Vector2.Dot(mouse - startMouse, screenAxis.normalized) * worldPerScreenUnit;
                }
                else movement = Vector3.zero;
            }
            if (vertexSnapMode && !hasNumber)
            {
                // First click chooses a vertex on the moving selection.
                // Afterwards its world-space position, not the object pivot,
                // is matched to the destination vertex.
                if (!vertexSourceChosen) movement = Vector3.zero;
                else
                {
                    vertexDestinationFound = HandleUtility.FindNearestVertex(mouse, out vertexDestination);
                    if (vertexDestinationFound)
                        movement = vertexDestination - vertexSnapSource;
                    else movement = Vector3.zero;
                }
            }
            else if (hasNumber)
            {
                Vector3 direction = axis == '\0' ? (movement.sqrMagnitude > 0.000001f ? movement.normalized : scene.camera.transform.right) : MoveAxisVector();
                movement = direction * number;
            }
            else if (ctrlHeld)
            {
                // Quantize the actual pivot against the SAME stationary origin
                // used by the visual track. Never snap a different offset grid.
                CaptureMoveSnapAnchor();
                Vector3 basePivot = pivotModeAtStart == PivotMode.Center ? groupCenter : startPosition;
                Vector3 desiredPivot = basePivot + movement;
                Vector3 fromAnchor = desiredPivot - moveSnapAnchor;
                Vector3 snappedOffset = Vector3.zero;
                if (axis == '\0')
                {
                    snappedOffset = new Vector3(
                        Mathf.Round(fromAnchor.x / Mathf.Max(0.0001f, Mathf.Abs(MoveSnap.x))) * Mathf.Max(0.0001f, Mathf.Abs(MoveSnap.x)),
                        Mathf.Round(fromAnchor.y / Mathf.Max(0.0001f, Mathf.Abs(MoveSnap.y))) * Mathf.Max(0.0001f, Mathf.Abs(MoveSnap.y)),
                        Mathf.Round(fromAnchor.z / Mathf.Max(0.0001f, Mathf.Abs(MoveSnap.z))) * Mathf.Max(0.0001f, Mathf.Abs(MoveSnap.z)));
                }
                else
                {
                    Vector3 direction = MoveAxisVector();
                    float step = Mathf.Max(0.0001f, Mathf.Abs(axis == 'X' ? MoveSnap.x : axis == 'Y' ? MoveSnap.y : MoveSnap.z));
                    snappedOffset = direction * (Mathf.Round(Vector3.Dot(fromAnchor, direction) / step) * step);
                }
                movement = moveSnapAnchor + snappedOffset - basePivot;
            }
            for (int i = 0; i < targets.Length; i++) if (targets[i]) targets[i].position = startPositions[i] + movement;
        }
        else if (mode == Mode.Rotate)
        {
            // Measure mouse motion on the SAME rotation plane as the gizmo.
            // Ray/plane intersections keep the cursor guide, dial and object
            // rotation coherent even when viewing an axis obliquely.
            Vector3 rotationAxis = RotationPlaneNormal(scene);
            Vector3 pivotWorld = RotationPivot();
            Vector3 currentVector3 = Vector3.zero;
            Vector3 previousVector3 = Vector3.zero;
            bool planar = axis != '\0' &&
                TryRotationPlaneVector(mouse, pivotWorld, rotationAxis, out currentVector3) &&
                TryRotationPlaneVector(lastMouse, pivotWorld, rotationAxis, out previousVector3);
            if (planar)
            {
                float stepAngle = Vector3.SignedAngle(previousVector3, currentVector3, rotationAxis);
                // Suppress unstable jumps when the plane is nearly edge-on.
                if (Mathf.Abs(stepAngle) < 90f)
                    accumulatedMouseAngle += stepAngle;
            }
            else
            {
                Vector2 currentVector = mouse - pivotScreen;
                Vector2 previousVector = lastMouse - pivotScreen;
                if (currentVector.sqrMagnitude > 16f && previousVector.sqrMagnitude > 16f)
                    accumulatedMouseAngle -= Vector2.SignedAngle(previousVector, currentVector);
            }
            lastMouse = mouse;
            rotationAngle = accumulatedMouseAngle;
            if (hasNumber) rotationAngle = number;
            else if (ctrlHeld)
            {
                if (axis != '\0' &&
                    TryRotationPlaneVector(startMouse, pivotWorld, rotationAxis, out Vector3 initialRay) &&
                    TryRotationPlaneVector(mouse, pivotWorld, rotationAxis, out Vector3 currentRay))
                {
                    Vector3 zero = RotationDialZero(rotationAxis, scene);
                    float initialAbsolute = Vector3.SignedAngle(zero, initialRay, rotationAxis);
                    float currentAbsolute = Vector3.SignedAngle(zero, currentRay, rotationAxis);
                    float snappedAbsolute = Mathf.Round(currentAbsolute / RotateSnap) * RotateSnap;
                    // Snap the cursor's world-space angle, not an angle offset
                    // from the initial cursor. Avoid wrapping at +/-180 degrees.
                    float snappedDelta = Mathf.DeltaAngle(initialAbsolute, snappedAbsolute);
                    rotationAngle = snappedDelta + 360f * Mathf.Round((accumulatedMouseAngle - snappedDelta) / 360f);
                }
                else rotationAngle = Mathf.Round(rotationAngle / RotateSnap) * RotateSnap;
            }
            Quaternion delta = Quaternion.AngleAxis(rotationAngle, rotationAxis);
            ApplyRotation(delta);
        }
        else if (mode == Mode.Trackball)
        {
            if (hasNumber) trackballDelta = Quaternion.AngleAxis(number, scene.camera.transform.up);
            if (ctrlHeld && !hasNumber)
            {
                Vector3 euler = trackballDelta.eulerAngles;
                euler = new Vector3(Mathf.Round(Mathf.DeltaAngle(0, euler.x) / RotateSnap) * RotateSnap,
                                    Mathf.Round(Mathf.DeltaAngle(0, euler.y) / RotateSnap) * RotateSnap,
                                    Mathf.Round(Mathf.DeltaAngle(0, euler.z) / RotateSnap) * RotateSnap);
                ApplyRotation(Quaternion.Euler(euler));
            }
            else ApplyRotation(trackballDelta);
        }
        SceneView.RepaintAll();
    }

    private static void ApplyRotation(Quaternion worldDelta)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            if (!targets[i]) continue;
            Quaternion delta = worldDelta;
            if (rotateLocalAxis && axis != '\0')
            {
                Vector3 localAxis = AxisVector();
                delta = Quaternion.AngleAxis(rotationAngle, startRotations[i] * localAxis);
            }
            targets[i].rotation = delta * startRotations[i];
            if (pivotModeAtStart == PivotMode.Center)
                targets[i].position = groupCenter + delta * (startPositions[i] - groupCenter);
        }
    }

    private static void RebaseDuplicateOperation(Vector2 mouse, SceneView scene, Mode next)
    {
        // Save the current preview as the starting point for the next operation.
        // Unlike restarting Begin(), this does not create more duplicates or
        // discard the duplicate session / original selection.
        groupCenter = Vector3.zero;
        int valid = 0;
        for (int i = 0; i < targets.Length; i++)
        {
            if (!targets[i]) continue;
            startPositions[i] = targets[i].position;
            startRotations[i] = targets[i].rotation;
            startScales[i] = targets[i].localScale;
            groupCenter += startPositions[i];
            valid++;
        }
        if (valid > 0) groupCenter /= valid;
        if (!target) return;
        startPosition = target.position;
        startRotation = target.rotation;
        startScale = target.localScale;
        startMouse = lastMouse = mouse;
        precisionMouse = previousPhysicalMouse = mouse;
        pivotScreen = HandleUtility.WorldToGUIPoint(
            pivotModeAtStart == PivotMode.Center ? groupCenter : startPosition);
        startRadius = Mathf.Max(12f, Vector2.Distance(mouse, pivotScreen));
        rotationAngle = 0f;
        accumulatedMouseAngle = 0f;
        freeArcZeroAngle = Mathf.Atan2(mouse.y - pivotScreen.y, mouse.x - pivotScreen.x);
        previousRotationVector = mouse - pivotScreen;
        rotationVectorReady = previousRotationVector.sqrMagnitude > 16f;
        trackballDelta = Quaternion.identity;
        trackballCameraRotation = scene.camera.transform.rotation;
        movePlaneNormal = scene.camera.transform.forward;
        startPlanePoint = startPosition;
        startPlaneHit = MousePlaneHit(mouse, startPlanePoint, movePlaneNormal, startPosition);
        axis = '\0';
        moveLocalAxis = false;
        rotateLocalAxis = false;
        planarScale = false;
        numericInput = "";
        RestoreVertexWireframe();
        vertexSnapMode = false;
        vertexSourceChosen = false;
        vertexDestinationFound = false;
        mode = next;
        ResetMoveSnapMarkers();
        SceneView.RepaintAll();
    }

    private static void Confirm()
    {
        if (duplicating)
        {
            // The objects were registered as created in Undo at duplication.
            // Persist their final transform values as part of that action.
            foreach (Transform t in targets)
                if (t) EditorUtility.SetDirty(t);
        }
        else if (target)
        {
            Vector3[] endPositions = new Vector3[targets.Length];
            Quaternion[] endRotations = new Quaternion[targets.Length];
            Vector3[] endScales = new Vector3[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                if (!targets[i]) continue;
                endPositions[i] = targets[i].position;
                endRotations[i] = targets[i].rotation;
                endScales[i] = targets[i].localScale;
                targets[i].position = startPositions[i];
                targets[i].rotation = startRotations[i];
                targets[i].localScale = startScales[i];
            }
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Blender " + mode);
            Undo.RecordObjects(System.Array.ConvertAll(targets, t => (Object)t), "Blender " + mode);
            for (int i = 0; i < targets.Length; i++)
            {
                if (!targets[i]) continue;
                targets[i].position = endPositions[i];
                targets[i].rotation = endRotations[i];
                targets[i].localScale = endScales[i];
                EditorUtility.SetDirty(targets[i]);
            }
            Undo.CollapseUndoOperations(undoGroup);
        }
        Finish();
    }

    private static void Cancel()
    {
        if (duplicating)
        {
            if (duplicatedObjects != null)
                foreach (GameObject copy in duplicatedObjects)
                    if (copy) Object.DestroyImmediate(copy);
            Selection.objects = System.Array.ConvertAll(
                originalSelections ?? System.Array.Empty<GameObject>(), go => (Object)go);
            Finish();
            return;
        }
        for (int i = 0; i < targets.Length; i++)
        {
            if (!targets[i]) continue;
            targets[i].position = startPositions[i];
            targets[i].rotation = startRotations[i];
            targets[i].localScale = startScales[i];
        }
        Finish();
    }

    private static void Finish()
    {
        RestoreVertexWireframe();
        mode = Mode.None;
        duplicating = false;
        originalSelections = null;
        duplicatedObjects = null;
        target = null;
        targets = System.Array.Empty<Transform>();
        axis = '\0';
        moveLocalAxis = false;
        rotateLocalAxis = false;
        planarScale = false;
        ctrlHeld = false;
        moveSnapAnchorSet = false;
        numericInput = "";
        Tools.hidden = previousToolsHidden;
        SceneView.RepaintAll();
    }

    private static bool TryAppendNumber(Event e)
    {
        // KeyCode first: Scene View often supplies no printable Event.character,
        // and Unity binds Alpha2 to its own 2D shortcut.
        if (e.keyCode >= KeyCode.Alpha0 && e.keyCode <= KeyCode.Alpha9)
        { numericInput += (char)('0' + (int)(e.keyCode - KeyCode.Alpha0)); return true; }
        if (e.keyCode >= KeyCode.Keypad0 && e.keyCode <= KeyCode.Keypad9)
        { numericInput += (char)('0' + (int)(e.keyCode - KeyCode.Keypad0)); return true; }
        if ((e.keyCode == KeyCode.Period || e.keyCode == KeyCode.KeypadPeriod || e.character == '.') && !numericInput.Contains("."))
        { numericInput += "."; return true; }
        if ((e.keyCode == KeyCode.Minus || e.keyCode == KeyCode.KeypadMinus || e.character == '-') && !numericInput.StartsWith("-"))
        { numericInput = "-" + numericInput; return true; }
        return false;
    }

    // Unity editor-inspired background, adjusted for light/dark editor skins.
    private static GUIStyle PanelStyle()
    {
        if (panelStyle != null) return panelStyle;
        const int size = 64, radius = 6;
        Color background = EditorGUIUtility.isProSkin
            ? new Color(9f / 255f, 9f / 255f, 9f / 255f, 0.90f)
            : new Color(0.77f, 0.77f, 0.77f, 0.98f);
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = Mathf.Max(radius - x - 0.5f, 0f, x - (size - radius) + 0.5f);
            float dy = Mathf.Max(radius - y - 0.5f, 0f, y - (size - radius) + 0.5f);
            float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy));
            texture.SetPixel(x, y, new Color(background.r, background.g, background.b, background.a * alpha));
        }
        texture.Apply();
        panelStyle = new GUIStyle(GUIStyle.none);
        panelStyle.normal.background = texture;
        panelStyle.border = new RectOffset(radius, radius, radius, radius);
        return panelStyle;
    }

    private static Rect PanelRect(SceneView scene)
    {
        // Keep the overlay fixed-width; only the counter's own label grows.
        float width = 320f;
        float height = mode == Mode.None ? 32f : 67f;
        float x = scene.position.width - width - 16f + panelOffset.x;
        float y = scene.position.height - 32f - 36f + panelOffset.y;
        return new Rect(Mathf.Clamp(x, 0f, Mathf.Max(0f, scene.position.width - width)),
                        Mathf.Clamp(y, 0f, Mathf.Max(0f, scene.position.height - height - 20f)),
                        width, height);
    }

    private static bool HandlePanelDrag(Event e, SceneView scene)
    {
        Rect rect = PanelRect(scene);
        // The narrow left grip is the only draggable region.
        Rect grip = new Rect(rect.x + 3f, rect.y + 3f, 19f, 26f);
        if (e.type == EventType.MouseDown && e.button == 0 && grip.Contains(e.mousePosition))
        {
            draggingPanel = true;
            dragMouseStart = e.mousePosition;
            dragOffsetStart = panelOffset;
            e.Use();
            return true;
        }
        if (!draggingPanel) return false;
        if (e.type == EventType.MouseDrag)
        {
            panelOffset = dragOffsetStart + e.mousePosition - dragMouseStart;
            // Clamp against the actual 320px panel bounds, not an old
            // hard-coded width. Keep the stored offset consistent with the
            // visible GUI position so scrolling/repainting cannot shift it.
            Rect clamped = PanelRect(scene);
            const float panelWidth = 320f;
            const float rightMargin = 16f;
            const float bottomAnchor = 32f + 36f;
            panelOffset = new Vector2(
                clamped.x - (scene.position.width - panelWidth - rightMargin),
                clamped.y - (scene.position.height - bottomAnchor));
            scene.Repaint();
            e.Use();
            return true;
        }
        if (e.type == EventType.MouseUp && e.button == 0)
        {
            draggingPanel = false;
            EditorPrefs.SetFloat(PanelXKey, panelOffset.x);
            EditorPrefs.SetFloat(PanelYKey, panelOffset.y);
            e.Use();
            return true;
        }
        return e.type == EventType.MouseMove;
    }

    private static float SafeRatio(float current, float original)
    {
        return Mathf.Abs(original) < 0.00001f ? current - original : current / original;
    }

    private static void CaptureMoveSnapAnchor()
    {
        if (mode != Mode.Move || vertexSnapMode || moveSnapAnchorSet || !target) return;
        moveSnapAnchor = pivotModeAtStart == PivotMode.Center
            ? CurrentSelectionCenter() : target.position;
        moveSnapAnchorSet = true;
    }

    private static void ResetMoveSnapMarkers()
    {
        moveSnapAnchorSet = false;
    }

    private static Vector3 CurrentSelectionCenter()
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (Transform t in targets)
            if (t) { sum += t.position; count++; }
        return count > 0 ? sum / count : (target ? target.position : Vector3.zero);
    }

    // Counts mesh triangles in the selected hierarchies. Nested selections are
    // counted once, and shared mesh instances count once per rendered object.
    private static long SelectedTriangleCount()
    {
        long total = 0;
        var visited = new System.Collections.Generic.HashSet<int>();
        foreach (GameObject selected in Selection.gameObjects)
        {
            if (!selected) continue;
            foreach (MeshFilter filter in selected.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter || !visited.Add(filter.GetInstanceID())) continue;
                Mesh mesh = filter.sharedMesh;
                if (mesh) total += MeshTriangleCount(mesh);
            }
            foreach (SkinnedMeshRenderer renderer in selected.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!renderer || !visited.Add(renderer.GetInstanceID())) continue;
                Mesh mesh = renderer.sharedMesh;
                if (mesh) total += MeshTriangleCount(mesh);
            }
        }
        return total;
    }

    private static long MeshTriangleCount(Mesh mesh)
    {
        long count = 0;
        for (int i = 0; i < mesh.subMeshCount; i++)
            if (mesh.GetTopology(i) == MeshTopology.Triangles)
                count += (long)mesh.GetIndexCount(i) / 3;
        return count;
    }

    private static void DrawOverlay(SceneView scene)
    {
        // Draw a Blender-style colored guide through the transform pivot.
        if (mode == Mode.Rotate && axis != '\0' && target && Event.current.type == EventType.Repaint)
        {
            Vector3 direction = AxisVector();
            if (rotateLocalAxis && mode == Mode.Rotate)
                direction = startRotation * direction;
            Vector3 anchor = pivotModeAtStart == PivotMode.Center ? groupCenter : startPosition;
            // Fixed world-space length, matching the grid's unit-based behavior.
            // Camera zoom and object scale do not change the guide length.
            const float extent = 30f;
            Color oldColor = Handles.color;
            Handles.color = axis == 'X' ? new Color(1f, 0.27f, 0.27f, 0.8f) :
                            axis == 'Y' ? new Color(0.32f, 0.92f, 0.40f, 0.8f) :
                                          new Color(0.32f, 0.60f, 1f, 0.8f);
            if (rotateLocalAxis) Handles.color = RotationGuideColor(false);
            Handles.DrawAAPolyLine(mode == Mode.Move ? 3f : 2f, anchor - direction * extent, anchor + direction * extent);
            Handles.color = oldColor;
        }
        Handles.BeginGUI();
        // G movement cue: white, heavier four-way icon at the live pivot.
        // On constrained moves, show the displacement from the original pivot.
        if (mode == Mode.Move && !vertexSnapMode && target && Event.current.type == EventType.Repaint)
        {
            Vector3 worldPivot = pivotModeAtStart == PivotMode.Center
                ? CurrentSelectionCenter() : target.position;
            Vector2 center = HandleUtility.WorldToGUIPoint(worldPivot);
            Color previous = Handles.color;
            // One opaque, uniform color for the entire G icon (no translucent overlaps).
            // Unconstrained = white; X/Y/Z = red/green/blue.
            Color moveIconColor = axis == 'X' ? new Color(0.96f, 0.31f, 0.31f, 1f) :
                                  axis == 'Y' ? new Color(0.30f, 0.85f, 0.39f, 1f) :
                                  axis == 'Z' ? new Color(0.32f, 0.57f, 1f, 1f) :
                                                Color.white;
            if (moveLocalAxis && axis != '\0')
                moveIconColor = new Color(moveIconColor.r * 0.72f, moveIconColor.g * 0.72f, moveIconColor.b * 0.72f, 1f);
            if (axis == '\0')
            {
                // Free movement keeps the four-way icon.
                Handles.color = moveIconColor;
                const float inner = 4f;
                const float outer = 14f;
                const float stroke = 3.6f;
                Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
                foreach (Vector2 dir in dirs)
                {
                    Vector2 tip = center + dir * outer;
                    Vector2 start = center + dir * inner;
                    Vector2 side = new Vector2(-dir.y, dir.x);
                    Vector2 basePoint = tip - dir * 6f;
                    Handles.DrawAAPolyLine(stroke, new Vector3(start.x, start.y, 0),
                        new Vector3(tip.x, tip.y, 0));
                    Handles.DrawAAPolyLine(stroke,
                        new Vector3((basePoint + side * 5.5f).x, (basePoint + side * 5.5f).y, 0),
                        new Vector3(tip.x, tip.y, 0),
                        new Vector3((basePoint - side * 5.5f).x, (basePoint - side * 5.5f).y, 0));
                }
            }
            else
            {
                // Axis movement: original-position dot, displacement line, and
                // a single soft arrowhead at the object's current position.
                Vector3 originalPivot = pivotModeAtStart == PivotMode.Center ? groupCenter : startPosition;
                Vector2 from = HandleUtility.WorldToGUIPoint(originalPivot);
                Vector2 delta = center - from;
                Handles.color = moveIconColor;
                Handles.DrawSolidDisc(new Vector3(from.x, from.y, 0), Vector3.forward, 4f);
                if (delta.sqrMagnitude > 1f)
                {
                    Handles.DrawAAPolyLine(2.8f, new Vector3(from.x, from.y, 0),
                        new Vector3(center.x, center.y, 0));
                    // Arrowhead points in the actual on-screen displacement direction.
                    // Hide it when movement is too small to display cleanly.
                    if (delta.magnitude > 12f)
                    {
                        Vector2 dir = delta.normalized;
                        Vector2 side = new Vector2(-dir.y, dir.x);
                        Vector2 back = center - dir * 8f;
                        Vector2 wingA = back + side * 8f;
                        Vector2 wingB = back - side * 8f;
                        Handles.DrawAAPolyLine(3f,
                            new Vector3(wingA.x, wingA.y, 0),
                            new Vector3(center.x, center.y, 0),
                            new Vector3(wingB.x, wingB.y, 0));
                    }
                }
            }
            Handles.color = previous;
        }
        // Stationary snap preview: anchor once when Ctrl snapping starts.
        // Points remain in world space while the selected object moves.
        if (mode == Mode.Move && ctrlHeld && !vertexSnapMode && target &&
            Event.current.type == EventType.Repaint)
        {
            CaptureMoveSnapAnchor();
            Vector3 currentPivot = moveSnapAnchor;
            Color previousColor = Handles.color;
            for (int axisIndex = 0; axisIndex < 3; axisIndex++)
            {
                char axisLetter = axisIndex == 0 ? 'X' : axisIndex == 1 ? 'Y' : 'Z';
                if (axis != '\0' && axis != axisLetter) continue;
                Vector3 direction = axisIndex == 0 ? Vector3.right :
                                    axisIndex == 1 ? Vector3.up : Vector3.forward;
                float increment = Mathf.Abs(axisIndex == 0 ? MoveSnap.x :
                                      axisIndex == 1 ? MoveSnap.y : MoveSnap.z);
                if (increment < 0.0001f) continue;
                Color tint = axisIndex == 0 ? new Color(1f, 0.38f, 0.38f, 0.9f) :
                             axisIndex == 1 ? new Color(0.42f, 1f, 0.48f, 0.9f) :
                                              new Color(0.42f, 0.68f, 1f, 0.9f);
                // Draw a fixed axis-colored track behind the stationary dots.
                Vector2 lineStart = HandleUtility.WorldToGUIPoint(
                    currentPivot - direction * (increment * 3f));
                Vector2 lineEnd = HandleUtility.WorldToGUIPoint(
                    currentPivot + direction * (increment * 3f));
                Handles.color = new Color(tint.r, tint.g, tint.b, 0.78f);
                Handles.DrawAAPolyLine(1.5f,
                    new Vector3(lineStart.x, lineStart.y, 0f),
                    new Vector3(lineEnd.x, lineEnd.y, 0f));
                // Thick displacement segment: fixed start marker to the
                // object's current position projected onto this axis.
                Vector3 movedPivot = pivotModeAtStart == PivotMode.Center
                    ? CurrentSelectionCenter() : target.position;
                float traveled = Vector3.Dot(movedPivot - currentPivot, direction);
                if (Mathf.Abs(traveled) > 0.00001f)
                {
                    Vector2 originScreen = HandleUtility.WorldToGUIPoint(currentPivot);
                    Vector2 movedScreen = HandleUtility.WorldToGUIPoint(
                        currentPivot + direction * traveled);
                    Handles.color = new Color(tint.r, tint.g, tint.b, 1f);
                    Handles.DrawAAPolyLine(5f,
                        new Vector3(originScreen.x, originScreen.y, 0f),
                        new Vector3(movedScreen.x, movedScreen.y, 0f));
                }
                // Right-angle, world-axis-aligned connector from the snap
                // ruler to the object. No diagonal shortcut: the orthogonal
                // bends create a tidy square/corner shape in the Scene view.
                Vector3 pointOnTrack = currentPivot + direction * traveled;
                Vector3 offset = movedPivot - pointOnTrack;
                if (offset.sqrMagnitude > 0.000001f)
                {
                    Vector3 cornerX = pointOnTrack + Vector3.right * offset.x;
                    Vector3 cornerXY = cornerX + Vector3.up * offset.y;
                    Vector2 p0 = HandleUtility.WorldToGUIPoint(pointOnTrack);
                    Vector2 p1 = HandleUtility.WorldToGUIPoint(cornerX);
                    Vector2 p2 = HandleUtility.WorldToGUIPoint(cornerXY);
                    Vector2 p3 = HandleUtility.WorldToGUIPoint(movedPivot);
                    Handles.color = new Color(tint.r * 0.72f, tint.g * 0.72f,
                                              tint.b * 0.72f, 0.82f);
                    Handles.DrawAAPolyLine(2f,
                        new Vector3(p0.x, p0.y, 0f),
                        new Vector3(p1.x, p1.y, 0f),
                        new Vector3(p2.x, p2.y, 0f),
                        new Vector3(p3.x, p3.y, 0f));
                }
                // Fixed small reference dots; do not create persistent trail markers.
                for (int distance = 1; distance <= 3; distance++)
                {
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        Vector3 worldPoint = currentPivot + direction * (sign * increment * distance);
                        Vector2 screenPoint = HandleUtility.WorldToGUIPoint(worldPoint);
                        Handles.color = tint;
                        Handles.DrawSolidDisc(new Vector3(screenPoint.x, screenPoint.y, 0f),
                                              Vector3.forward, 2.5f);
                    }
                }
                // One active indicator exactly at the snapped position.
                if (Mathf.Abs(traveled) > 0.00001f)
                {
                    Vector2 active = HandleUtility.WorldToGUIPoint(
                        currentPivot + direction * traveled);
                    Handles.color = new Color(tint.r, tint.g, tint.b, 1f);
                    Handles.DrawSolidDisc(new Vector3(active.x, active.y, 0f),
                                          Vector3.forward, 6f);
                }
            }
            Handles.color = previousColor;
        }
        if (mode == Mode.Move && vertexSnapMode && Event.current.type == EventType.Repaint)
        {
            Vector3 hoveredSource = Vector3.zero;
            bool sourceHovered = !vertexSourceChosen &&
                TryPickSourceVertex(Event.current.mousePosition, out hoveredSource);
            bool destinationActive = vertexSourceChosen && vertexDestinationFound;
            Color markerColor = !vertexSourceChosen ? Color.white :
                destinationActive ? new Color(0.32f, 0.95f, 0.45f, 1f) :
                new Color(1f, 0.82f, 0.18f, 1f);

            // White = hover/source selection; yellow = grabbed;
            // green = a destination vertex has been found.
            if (sourceHovered || vertexSourceChosen)
            {
                Vector3 world = vertexSourceChosen ?
                    (destinationActive ? vertexDestination : vertexSnapSource) : hoveredSource;
                Vector2 center = HandleUtility.WorldToGUIPoint(world);
                Color previous = Handles.color;
                Handles.color = markerColor;
                const float r = 7f;
                Vector3 a = new Vector3(center.x - r, center.y - r, 0f);
                Vector3 b = new Vector3(center.x + r, center.y - r, 0f);
                Vector3 c = new Vector3(center.x + r, center.y + r, 0f);
                Vector3 d = new Vector3(center.x - r, center.y + r, 0f);
                Handles.DrawAAPolyLine(2.5f, a, b, c, d, a);
                // Keep the small crosshair inside the vertex marker.
                Handles.DrawAAPolyLine(2.5f,
                    new Vector3(center.x - 4, center.y, 0f),
                    new Vector3(center.x + 4, center.y, 0f));
                Handles.DrawAAPolyLine(2.5f,
                    new Vector3(center.x, center.y - 4, 0f),
                    new Vector3(center.x, center.y + 4, 0f));
                Handles.color = previous;
            }
            if (vertexSourceChosen)
            {
                // Fixed start point to current snapped vertex (or cursor).
                Vector2 origin = HandleUtility.WorldToGUIPoint(vertexSnapSource);
                Vector2 end = destinationActive ?
                    HandleUtility.WorldToGUIPoint(vertexDestination) : Event.current.mousePosition;
                Color previous = Handles.color;
                Handles.color = new Color(1f, 0.56f, 0.16f, 0.92f);
                Handles.DrawAAPolyLine(4f,
                    new Vector3(origin.x, origin.y, 0f),
                    new Vector3(end.x, end.y, 0f));
                // Endpoint dots match the orange vertex snapping tether.
                Handles.DrawSolidDisc(new Vector3(origin.x, origin.y, 0f), Vector3.forward, 4f);
                Handles.DrawSolidDisc(new Vector3(end.x, end.y, 0f), Vector3.forward, 4f);
                Handles.color = previous;
            }
        }
        // Scale guide: short outer T caps and inner dots, separated by a variable dotted gap.
        // GUI-space drawing keeps line weight and endpoint dots constant while zooming.
        if (mode == Mode.Scale && target && Event.current.type == EventType.Repaint)
        {
            Vector3 scalePivot = pivotModeAtStart == PivotMode.Center
                ? CurrentSelectionCenter() : target.position;
            Vector2 objectPoint = HandleUtility.WorldToGUIPoint(scalePivot);
            Vector2 cursorPoint = Event.current.mousePosition;
            Vector2 delta = cursorPoint - objectPoint;
            float distance = delta.magnitude;
            if (distance > 12f)
            {
                Vector2 along = delta / distance;
                Vector2 perpendicular = new Vector2(-along.y, along.x);
                // Keep the solid end segments short and constant at large distances.
                // At close range, preserve the existing proportional layout.
                float endSegmentLength = Mathf.Min(distance * 0.39f, 28f);
                float scaleGuideGap = distance - 2f * endSegmentLength;
                Vector2 firstTip = objectPoint + along * endSegmentLength;
                Vector2 secondTip = cursorPoint - along * endSegmentLength;
                Color previous = Handles.color;
                Color orange = planarScale ? new Color(0.96f, 0.96f, 0.96f, 0.94f) :
                    axis == '\0' ? new Color(1f, 0.72f, 0.32f, 0.92f) : RotationGuideColor(false);
                Handles.color = orange;
                const float stroke = 3f; // Match the rotation guide stroke width.
                const float capHalfWidth = 6.75f; // Three times wider T crossbars; unchanged stroke weight.
                Handles.DrawAAPolyLine(stroke, objectPoint, firstTip);
                Handles.DrawAAPolyLine(stroke, cursorPoint, secondTip);
                // Short perpendicular T caps at the ends of the solid segments.
                Handles.DrawAAPolyLine(stroke,
                    objectPoint + perpendicular * capHalfWidth,
                    objectPoint - perpendicular * capHalfWidth);
                Handles.DrawAAPolyLine(stroke,
                    cursorPoint + perpendicular * capHalfWidth,
                    cursorPoint - perpendicular * capHalfWidth);
                // Faint dotted connection across the open space.
                Handles.color = new Color(orange.r, orange.g, orange.b, 0.30f);
                const float dotSpacing = 9f;
                for (float offset = 5f; offset < scaleGuideGap - 3f; offset += dotSpacing)
                {
                    Vector2 dot = firstTip + along * offset;
                    Handles.DrawSolidDisc(new Vector3(dot.x, dot.y, 0f), Vector3.forward, 1.1f);
                }
                Handles.color = orange;
                Handles.DrawSolidDisc(new Vector3(firstTip.x, firstTip.y, 0f), Vector3.forward, 3.5f);
                Handles.DrawSolidDisc(new Vector3(secondTip.x, secondTip.y, 0f), Vector3.forward, 3.5f);
                Handles.color = previous;
            }
        }
        // R -> R trackball: single-stroke white ring, with tether starting at its edge.
        if (mode == Mode.Trackball && target && Event.current.type == EventType.Repaint)
        {
            Vector2 pivot = HandleUtility.WorldToGUIPoint(RotationPivot());
            Vector2 cursor = Event.current.mousePosition;
            Color oldColor = Handles.color;
            Color trackballWhite = Color.white;
            const float stroke = 3f;
            const float ringRadius = 13f;
            Handles.color = trackballWhite;

            // Draw one circular outline at the same weight as the tether.
            const int segments = 64;
            Vector3[] ring = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                ring[i] = new Vector3(pivot.x + Mathf.Cos(angle) * ringRadius,
                                      pivot.y + Mathf.Sin(angle) * ringRadius, 0f);
            }
            Handles.DrawAAPolyLine(stroke, ring);

            Vector2 offset = cursor - pivot;
            if (offset.sqrMagnitude > ringRadius * ringRadius)
            {
                Vector2 lineStart = pivot + offset.normalized * ringRadius;
                Handles.DrawAAPolyLine(stroke,
                    new Vector3(lineStart.x, lineStart.y, 0f),
                    new Vector3(cursor.x, cursor.y, 0f));
            }
            Handles.DrawSolidDisc(new Vector3(cursor.x, cursor.y, 0f), Vector3.forward, 3.5f);
            Handles.color = oldColor;
        }
        // Cursor tether for the regular Blender-style rotation only.
        // Use GUI-space coordinates so the line stays attached to the cursor.
        if (mode == Mode.Rotate && target && Event.current.type == EventType.Repaint)
        {
            // The camera can move/zoom while a transform is active. Never use
            // the cached pivotScreen for drawing: it was captured before zoom.
            Vector3 worldPivot = RotationPivot();
            Vector2 pivot = HandleUtility.WorldToGUIPoint(worldPivot);
            Vector2 cursor = Event.current.mousePosition;
            Vector3 normal = RotationPlaneNormal(scene);
            bool constrained = axis != '\0';
            Vector3 reference = Vector3.ProjectOnPlane(scene.camera.transform.right, normal);
            if (reference.sqrMagnitude < 0.0001f)
                reference = Vector3.ProjectOnPlane(scene.camera.transform.up, normal);
            reference.Normalize();
            // Zero tick must be the actual initial cursor direction, otherwise
            // snapped angles can sit between the visual clock ticks.
            if (constrained)
            {
                Vector3 pickedDirection;
                if (TryRotationPlaneVector(startMouse, worldPivot, normal, out pickedDirection))
                    reference = pickedDirection.normalized;
            }
            else
            {
                Vector2 screenZero = startMouse - pivot;
                if (screenZero.sqrMagnitude > 0.001f)
                {
                    reference = (scene.camera.transform.right * screenZero.x
                        - scene.camera.transform.up * screenZero.y).normalized;
                }
            }
            if (constrained && ctrlHeld)
                reference = RotationDialZero(normal, scene);
            Vector3 secondary = Vector3.Cross(normal, reference).normalized;
            float worldPerPixel = scene.camera.orthographic
                ? (scene.camera.orthographicSize * 2f / Mathf.Max(1f, scene.position.height))
                : (2f * Mathf.Max(0.01f, Vector3.Dot(worldPivot - scene.camera.transform.position,
                    scene.camera.transform.forward)) * Mathf.Tan(scene.camera.fieldOfView * 0.5f * Mathf.Deg2Rad)
                    / Mathf.Max(1f, scene.position.height));

            // The cursor tether uses a projected world-space radial vector
            // rotated by the EXACT angle applied to the selected objects.
            if (constrained)
            {
                Vector3 rotatedDirection;
                if (ctrlHeld && TryRotationPlaneVector(startMouse, worldPivot, normal, out Vector3 initialRay))
                    rotatedDirection = Quaternion.AngleAxis(rotationAngle, normal) * initialRay.normalized;
                else
                    rotatedDirection = Quaternion.AngleAxis(rotationAngle, normal) * reference;
                float lineRadius = Mathf.Max(65f, (Event.current.mousePosition - pivot).magnitude);
                cursor = HandleUtility.WorldToGUIPoint(
                    worldPivot + rotatedDirection * (lineRadius * worldPerPixel));
            }
            else if (ctrlHeld)
            {
                float radius = Mathf.Max(16f, (cursor - pivot).magnitude);
                float initialAngle = Mathf.Atan2(startMouse.y - pivot.y, startMouse.x - pivot.x);
                float snappedGuiAngle = initialAngle - rotationAngle * Mathf.Deg2Rad;
                cursor = pivot + new Vector2(Mathf.Cos(snappedGuiAngle),
                                              Mathf.Sin(snappedGuiAngle)) * radius;
            }
            // Shift only controls visibility. Never reset or re-anchor the arc.
            // The angle continues accumulating while the arc is hidden.
            Color oldGuiColor = Handles.color;
            Color guideColor = RotationGuideColor(ctrlHeld);
            Handles.color = guideColor;
            if (ctrlHeld)
            {
                const float dialInnerRadius = 46f;
                const float dialOuterRadius = 55f;
                float step = Mathf.Max(0.0001f, RotateSnap);
                int tickCount = Mathf.Min(180, Mathf.CeilToInt(360f / step));
                // Axis snapping: only the dial ticks become white. The tether,
                // pivot dots and other axis guides retain their RGB colors.
                Color dialColor = constrained ? Color.white : guideColor;
                Handles.color = new Color(dialColor.r, dialColor.g, dialColor.b, 0.40f);
                for (int i = 0; i < tickCount; i++)
                {
                    float degrees = i * step;
                    if (degrees >= 360f - 0.0001f) break;
                    float a = degrees * Mathf.Deg2Rad;
                    Vector3 direction = reference * Mathf.Cos(a) + secondary * Mathf.Sin(a);
                    Vector2 inner = HandleUtility.WorldToGUIPoint(
                        worldPivot + direction * (dialInnerRadius * worldPerPixel));
                    Vector2 outer = HandleUtility.WorldToGUIPoint(
                        worldPivot + direction * (dialOuterRadius * worldPerPixel));
                    Handles.DrawAAPolyLine(4f,
                        new Vector3(inner.x, inner.y, 0f),
                        new Vector3(outer.x, outer.y, 0f));
                }
                Handles.color = guideColor;
            }
            Handles.DrawAAPolyLine(3f, new Vector3(pivot.x, pivot.y, 0f),
                                      new Vector3(cursor.x, cursor.y, 0f));
            // Rotation arc lies in the actual X/Y/Z rotation plane when constrained.
            // Project each point individually so oblique camera angles are respected.
            {
                const float arcRadius = 29f;
                // Alternate drawing and erasing each full revolution.
                // 0..360 draws the arc; 360..720 erases it from its starting end.
                // The accumulated rotationAngle is unchanged.
                float visualAngle = rotationAngle;
                float turns = Mathf.Abs(visualAngle) / 360f;
                int fullTurns = Mathf.FloorToInt(turns);
                float progress = (turns - fullTurns) * 360f;
                bool erasing = (fullTurns % 2) == 1;
                float startDegrees = erasing ? progress : 0f;
                float endDegrees = erasing ? 360f : progress;
                float visibleDegrees = endDegrees - startDegrees;
                float directionSign = Mathf.Sign(visualAngle);
                int segments = Mathf.Clamp(Mathf.CeilToInt(visibleDegrees / 5f), 2, 72);
                Vector3[] arc = new Vector3[segments + 1];
                if (constrained)
                {
                    for (int i = 0; i <= segments; i++)
                    {
                        float a = (startDegrees + visibleDegrees * i / segments) * Mathf.Deg2Rad * directionSign;
                        Vector3 arcReference = reference;
                        Vector3 arcSecondary = secondary;
                        Vector3 direction = arcReference * Mathf.Cos(a) + arcSecondary * Mathf.Sin(a);
                        Vector2 screen = HandleUtility.WorldToGUIPoint(
                            worldPivot + direction * (arcRadius * worldPerPixel));
                        arc[i] = new Vector3(screen.x, screen.y, 0f);
                    }
                }
                else
                {
                    // Use the initial dial direction for the entire operation.
                    // Shift only hides the arc; it does not change this anchor.
                    float startAngle = freeArcZeroAngle;
                    for (int i = 0; i <= segments; i++)
                    {
                        float a = startAngle - (startDegrees + visibleDegrees * i / segments) * Mathf.Deg2Rad * directionSign;
                        arc[i] = new Vector3(pivot.x + Mathf.Cos(a) * arcRadius,
                                             pivot.y + Mathf.Sin(a) * arcRadius, 0f);
                    }
                }
                Handles.color = ctrlHeld ? guideColor :
                    constrained ? guideColor : new Color(1f, 0.72f, 0.32f, 0.92f);
                // In X/Y/Z snapping, the white dial ticks replace the arc.
                if (visibleDegrees > 0.01f && !ctrlHeld)
                    Handles.DrawAAPolyLine(3f, arc);
                Handles.color = guideColor;
            }
            // Small filled endpoint dots in the same orange / snap-green color.
            // Draw in GUI coordinates so their size stays constant while zooming.
            Handles.DrawSolidDisc(new Vector3(pivot.x, pivot.y, 0f), Vector3.forward, 3.5f);
            Handles.DrawSolidDisc(new Vector3(cursor.x, cursor.y, 0f), Vector3.forward, 3.5f);
            Handles.color = oldGuiColor;
        }
        Rect rect = PanelRect(scene);
        GUI.Box(rect, GUIContent.none, PanelStyle());
        // Three-line drag grip, like Unity's built-in Scene view toolbar.
        Color gripColor = EditorGUIUtility.isProSkin
            ? new Color(0.63f, 0.63f, 0.63f, 0.85f)
            : new Color(0.30f, 0.30f, 0.30f, 0.85f);
        for (int i = 0; i < 2; i++)
            EditorGUI.DrawRect(new Rect(rect.x + 11 + i * 3, rect.y + 9, 1.5f, 14), gripColor);
        GUIStyle title = new GUIStyle(EditorStyles.label)
        {
            fontSize = 12, normal = { textColor = new Color(1f, 0.58f, 0.19f) }
        };
        GUIStyle body = new GUIStyle(EditorStyles.label)
        {
            fontSize = 12, normal = { textColor = new Color(0.92f, 0.94f, 0.97f) }
        };
        GUI.Label(new Rect(rect.x + 27, rect.y + 6, 186, 19), "Blender Mode", title);
        // Primary selection count, with a quieter triangle count to its right.
        int selectedCount = Selection.gameObjects.Length;
        string countText = selectedCount.ToString(CultureInfo.InvariantCulture);
        string trisText = "Tris: " + SelectedTriangleCount().ToString("N0", CultureInfo.InvariantCulture);
        GUIContent objectIcon = EditorGUIUtility.IconContent("GameObject Icon");
        GUIStyle countStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleRight
        };
        countStyle.normal.textColor = new Color(0.84f, 0.88f, 0.94f);
        GUIStyle trisStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleRight
        };
        trisStyle.normal.textColor = EditorGUIUtility.isProSkin
            ? new Color(0.60f, 0.63f, 0.67f)
            : new Color(0.43f, 0.45f, 0.48f);
        const float rightPadding = 10f;
        const float gap = 10f;
        float trisWidth = Mathf.Max(43f, trisStyle.CalcSize(new GUIContent(trisText)).x + 2f);
        float trisX = rect.xMax - rightPadding - trisWidth;
        GUI.Label(new Rect(trisX, rect.y + 6, trisWidth, 19), trisText, trisStyle);
        float countWidth = Mathf.Max(17f, countStyle.CalcSize(new GUIContent(countText)).x + 3f);
        float countX = trisX - gap - countWidth;
        GUI.Label(new Rect(countX, rect.y + 6, countWidth, 19), countText, countStyle);
        if (objectIcon != null && objectIcon.image != null)
            GUI.DrawTexture(new Rect(countX - 17f, rect.y + 9, 15, 15), objectIcon.image, ScaleMode.ScaleToFit);
        if (mode != Mode.None)
        {
            string action = vertexSnapMode && mode == Mode.Move ? "VERTEX SNAP" : mode == Mode.Move ? "MOVE" : mode == Mode.Rotate ? "ROTATE" :
                            mode == Mode.Trackball ? "TRACKBALL" : "SCALE";
            string iconName = mode == Mode.Move ? "MoveTool" :
                              (mode == Mode.Rotate || mode == Mode.Trackball) ? "RotateTool" : "ScaleTool";
            GUIContent transformIcon = EditorGUIUtility.IconContent(iconName);
            if (transformIcon != null && transformIcon.image != null)
            {
                Color previousColor = GUI.color;
                if (ctrlHeld) GUI.color = mode == Mode.Rotate ? Color.white : new Color(0.38f, 1f, 0.48f, 1f);
                GUI.DrawTexture(new Rect(rect.x + 27, rect.y + 36, 16, 16),
                                transformIcon.image, ScaleMode.ScaleToFit);
                GUI.color = previousColor;
            }
            string operation = (duplicating ? "COPY / " : "") + action;
            if (ctrlHeld)
            {
                float increment = mode == Mode.Move
                    ? (axis == 'Y' ? MoveSnap.y : axis == 'Z' ? MoveSnap.z : MoveSnap.x)
                    : mode == Mode.Scale ? ScaleSnap : RotateSnap;
                operation = "Snap: " + increment.ToString("0.###", CultureInfo.InvariantCulture);
            }
            GUIStyle operationStyle = new GUIStyle(body)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11
            };
            // A small gap after the 16px tool icon; never center the label
            // across the whole operation area.
            GUI.Label(new Rect(rect.x + 48, rect.y + 34, 80, 20), operation, operationStyle);
            Vector3 values = Vector3.zero;
            if (target)
            {
                if (mode == Mode.Move) values = target.position - startPosition;
                else if (mode == Mode.Scale)
                    values = new Vector3(SafeRatio(target.localScale.x, startScale.x),
                                         SafeRatio(target.localScale.y, startScale.y),
                                         SafeRatio(target.localScale.z, startScale.z));
                else
                {
                    Quaternion delta = target.rotation * Quaternion.Inverse(startRotation);
                    Vector3 angles = delta.eulerAngles;
                    values = new Vector3(Mathf.DeltaAngle(0, angles.x),
                                         Mathf.DeltaAngle(0, angles.y),
                                         Mathf.DeltaAngle(0, angles.z));
                }
            }
            // Inspector-style inline axis fields: colored letter, then a
            // dark rounded value box with white text, all on one row.
            float x = rect.x + 131f;
            for (int i = 0; i < 3; i++)
            {
                char letter = "XYZ"[i];
                Color color = i == 0 ? new Color(0.96f, 0.43f, 0.43f) :
                              i == 1 ? new Color(0.48f, 0.86f, 0.53f) :
                                       new Color(0.48f, 0.66f, 1f);
                GUIStyle axisStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleCenter
                };
                axisStyle.normal.textColor = color;
                GUI.Label(new Rect(x, rect.y + 35f, 13f, 18f), letter.ToString(), axisStyle);

                Rect valueBox = new Rect(x + 14f, rect.y + 35f, 43f, 19f);
                GUIStyle fieldBackground = new GUIStyle(EditorStyles.textField);
                GUI.Box(valueBox, GUIContent.none, fieldBackground);

                float value = i == 0 ? values.x : i == 1 ? values.y : values.z;
                GUIStyle numberStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 10,
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip
                };
                numberStyle.normal.textColor = Color.white;
                GUI.Label(valueBox, value.ToString("0.##", CultureInfo.InvariantCulture), numberStyle);
                x += 61f;
            }
            // Numeric input is already reflected in the live XYZ values.
        }
        Handles.EndGUI();
    }
}
