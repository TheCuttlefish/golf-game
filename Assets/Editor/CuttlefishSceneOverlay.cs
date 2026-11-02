
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;

namespace Cuttlefish.Editor
{
    [Overlay(typeof(SceneView), "Cuttlefish Tools | version 1.0", true,
        defaultDockZone = DockZone.TopToolbar,
        defaultDockPosition = DockPosition.Top)]
    public sealed class CuttlefishSceneOverlay : IMGUIOverlay
    {
        private static CuttlefishSceneOverlay FindInSceneView(SceneView scene)
        {
            if (scene == null || scene.overlayCanvas == null)
                return null;

            foreach (var item in scene.overlayCanvas.overlays)
                if (item is CuttlefishSceneOverlay cuttlefish)
                    return cuttlefish;

            return null;
        }

        private static SceneView FindSceneViewWithOverlay()
        {
            SceneView active = SceneView.lastActiveSceneView;

            if (FindInSceneView(active) != null)
                return active;

            foreach (SceneView scene in SceneView.sceneViews)
                if (FindInSceneView(scene) != null)
                    return scene;

            return active;
        }

        internal static bool IsExpandedInActiveSceneView()
        {
            CuttlefishSceneOverlay overlay =
                FindInSceneView(FindSceneViewWithOverlay());

            return overlay != null &&
                   overlay.displayed &&
                   overlay.floating &&
                   !overlay.collapsed;
        }

        internal static void SetExpandedInActiveSceneView(bool expand)
        {
            SceneView scene = FindSceneViewWithOverlay();
            CuttlefishSceneOverlay overlay = FindInSceneView(scene);

            if (overlay == null)
            {
                Debug.LogWarning(
                    "Cuttlefish: Scene View overlay not found. " +
                    "Enable Cuttlefish in the Scene View Overlays menu.");
                return;
            }

            if (expand)
            {
                overlay.displayed = true;

                if (!overlay.floating)
                    overlay.Undock();

                overlay.collapsed = false;
            }
            else
            {
                scene.overlayCanvas.ResetOverlay(overlay);
                overlay.displayed = true;
                overlay.collapsed = true;
            }

            scene.Repaint();
        }

        public override void OnCreated()
        {
            base.OnCreated();

            CuttlefishTransformTool.NativeOverlayAttached();

            collapsedIcon =
                CuttlefishTransformTool.NativeOverlayLogo;

            floatingChanged += OnFloatingChanged;

            if (floating)
                collapsed = false;
        }

        public override void OnWillBeDestroyed()
        {
            floatingChanged -= OnFloatingChanged;

            CuttlefishTransformTool.NativeOverlayDetached();

            base.OnWillBeDestroyed();
        }

        private void OnFloatingChanged(bool isFloating)
        {
            if (isFloating)
                collapsed = false;
        }

        public override void OnGUI()
        {
            CuttlefishTransformTool.DrawNativeOverlayToolbar(
                containerWindow as SceneView);
        }
    }
}
#endif
