# Cuttlefish — v1.0 Release Test Checklist

Tester: __________________   Unity editor version: __________________   OS: __________________

## Install and compile

- [ ] Project backed up / previous scripts removed from Assets.
- [ ] Exactly **one** copy of each of the three class definitions remains.
- [ ] Scripts are in `Assets/Cuttlefish/Editor/` with the exact downloaded filenames.
- [ ] Icon textures and FBX meshes moved into `Assets/Cuttlefish/` via Unity (preserving their `.meta` files).
- [ ] Console shows **no compile errors or obsolete/deprecation warnings**.
- [ ] Tools → Cuttlefish Mode enables/disables the overlay.
- [ ] Toolbar looks right after a domain reload / restart.

## Menu interaction — high priority

- [ ] Space opens the Action Menu next to the cursor (top-left anchor).
- [ ] Pressing Space repeatedly while Action is open **does not close it**.
- [ ] Clicking outside or pressing Escape closes the Action Menu.
- [ ] Shift + A opens the Add Menu at the cursor with the expected submenu layout.
- [ ] Pressing Shift + A repeatedly while Add is open **does not close it**.
- [ ] Space with the Add Menu search focused inserts spaces into search text.
- [ ] Space with Add open but search unfocused does **not** unexpectedly open Action.
- [ ] **Space → Shift + A**: Space closes, Add remains open.
- [ ] **Shift + A → Space**: behaviour remains the expected established behaviour (no unexpected double-close).
- [ ] Toolbar + Add and spatial-action buttons open the intended menus.
- [ ] Menus stay inside the Scene View at all four edges; test scaled display / second monitor.
- [ ] Dark and light Unity themes show correct menu borders with **no bottom grey strip**.
- [ ] Reopen/close menus 50 times without console warnings/errors or freezing.

## Object creation and bundled assets

- [ ] Built-in primitives create, are selected, and Undo works.
- [ ] Custom imported primitives (Cone, Icosphere, Torus, Cylinder, Pyramid, Star, Egg, Hexagon) create using **included** FBX files.
- [ ] Camera, light, audio source, and effect items work.
- [ ] Prefab search finds prefabs in another folder under Assets.
- [ ] Prefab spawn keeps the source prefab unmodified (new scene object is intentionally unpacked).
- [ ] UI object commands work with supported UGUI/TextMeshPro packages.
- [ ] Decal Projector behaves appropriately for the active render pipeline (or warns clearly when unavailable).
- [ ] All icons/logo are present after moving to `Assets/Cuttlefish/`.
- [ ] New object placement in front of Scene View camera is acceptable in perspective **and** orthographic mode.

## Transform and toolbar

- [ ] G/R/S work in normal and axis-constrained modes.
- [ ] Numeric entry, precision Shift and Ctrl snapping work.
- [ ] R → R trackball and local/world axes work.
- [ ] Shift + D duplicate/transform/undo works, including prefab instances.
- [ ] G → B vertex snapping works; temporary wireframe restores on confirm AND cancel.
- [ ] Multi-object transforms and parent+child selections behave correctly.
- [ ] Move/rotate/scale Undo and Redo return expected values; Escape returns original values.
- [ ] Pivot/Center and Local/Global spatial menu options work.
- [ ] Move to View, Align with View and Center on Children work and should have expected Undo behavior.
- [ ] Toolbar dragging, N collapse, selected object count and triangle count behave as expected.
- [ ] Opening Game View, entering/exiting Play Mode, changing Scene Views, closing/reopening scenes do not leave hidden handles or stuck input.

## Marketplace submission checks

- [ ] Imported clean into a **brand new Unity 6.3 project**.
- [ ] Confirm additional Unity versions only after testing them.
- [ ] Confirm URP/HDRP compatibility for versions/pipelines advertised.
- [ ] Package root is one `Assets/Cuttlefish` folder with relevant `.meta` files.
- [ ] Remove test/backup/duplicate/unused assets and check asset-path lengths.
- [ ] Documentation and a real support contact are included.
- [ ] Ownership/licensing of icons, custom meshes, and any third-party assets is documented.
- [ ] Make product screenshots and a short demo video for listing (keep videos outside package).
- [ ] Make accurate AI-assistance disclosure in the Unity listing if applicable.
- [ ] Tools → Asset Store → Validator reports no submission-blocking issues.

## Test results / bugs

| Test | Pass/Fail | Notes |
| --- | --- | --- |
| Compile | | |
| Popup switching | | |
| Popup cursor positioning | | |
| Imported meshes | | |
| Toolbar and transforms | | |
| Undo/Redo | | |
| Clean import | | |
| Validator | | |
