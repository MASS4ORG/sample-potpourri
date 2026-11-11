# Scenes — author in Studio once Epic 1 components exist

These `.prefab` files are intentionally not scaffolded: they reference
`UiDocumentComponent` / `UiRaycasterComponent`, which do not exist yet, and their
`StartupScene` asset id in `../../project.data` must be the id the importer assigns
on first scan. Author them in Studio after issues #4/#5/#6 land, then set
`project.data` → `StartupScene`.

## `showcase.prefab` (screen-space overlay)

```
Root
├── Camera            CameraComponent (Priority 10)
└── UI                UiDocumentComponent {
                          RenderMode  = ScreenSpaceOverlay
                          Document    = Assets/UI/showcase.ui
                          ScaleMode   = ScaleWithScreenSize (reference 1920×1080)
                      }
```

## `computer.prefab` (world-space UI in a 3D scene)

```
Root
├── Camera            CameraComponent + FreeFlyCameraComponent
├── Room              ModelComponent (Assets/Models/room.glb, or a scaled cube)
├── KeyLight          LightComponent (Directional)
└── Monitor           ModelComponent (Assets/Models/monitor.glb, or a cube)
    └── Screen        (positioned/scaled to the monitor's screen face)
                      UiDocumentComponent {
                          RenderMode = WorldSpace
                          Document   = Assets/UI/diagnostics.ui
                          PanelSize  = 1024 × 640      (pixels)
                          WorldSize  = 0.52 × 0.325    (metres)
                      }
                      UiRaycasterComponent { }        // ray → panel UV → input
```

Verify headlessly:

```sh
dotnet run --project Turian.Editor.CLI -- screenshot ../../gamedev/turian-examples/example-03-ui \
    --scene Assets/Scenes/computer.prefab --out computer.png --load-usercode
```
