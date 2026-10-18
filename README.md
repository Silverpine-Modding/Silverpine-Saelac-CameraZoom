# Camera Zoom

Version 1.0.1 — created by Saelac and ChatGPT for Silverpine 1.7.3.

[Download the latest release](https://github.com/Silverpine-Modding/Silverpine-Saelac-CameraZoom/releases/latest)

Adjust world magnification from **0.2x to 2.0x** in **0.1 steps**:

- **0.2x:** zoom out to see five times the normal width and height.
- **1.0x:** normal view; also available through Reset.
- **2.0x:** zoom in to make the world twice as large.

Open **Modding Tools → Camera Zoom** from the main menu, or open the
inventory's **Mods → Camera Zoom** during play. Use the slider, the +/-
buttons, or enter a number and press **Apply value** or **Enter**.
Direct entries round to the nearest 0.1; values outside 0.2–2.0 are rejected.
Decimal input follows your regional settings and also accepts a decimal point.
Drag the window by its title bar. Close or Escape returns to the tools menu.
The window has an opaque dark charcoal background. A framework overlay blocks
clicks, scrolling, and drags from reaching the menu behind it. Zoom updates do
not clear focus or release the slider's drag capture.

Changes apply immediately in gameplay and persist across characters and launches
in `BepInEx/config/Saelac.Silverpine.CameraZoom.cfg`. The default is 1.0x.
The main-menu setting takes effect when entering the world. Close the game
before editing the configuration file manually.

## Installation

Requires BepInEx 5 and **[Modding Tools 1.10.0 or newer](https://github.com/Silverpine-Modding/Silverpine-Saelac-Modding-Tools/releases)**. Put `CameraZoom.dll`
and this README in `BepInEx/plugins/CameraZoom/`, then start/restart Silverpine.
Use the existing Modding Tools installation; do not add a duplicate framework DLL.

The mod preserves the native camera angle and player following. Menus, portraits,
item previews, and UI size are unchanged. Unity culls rendering outside the
camera view. The fog grid fits the viewport with a small safety border: it
shrinks when zooming in and expands when zooming out, while retaining the
game's line-of-sight rules and 15-tile sight limit;
zooming out does not reveal hidden rooms or extend character vision. Native
pixel-perfect camera snapping is temporarily suspended for fractional zoom and
restored at 1.0x. Salt Extra Debug's whole-tile view takes priority until closed.
Grid rebuilding happens only when the view's required grid dimensions change,
or during the game's normal visibility initialization. Idle frames and player
movement do not rebuild the grid. Offscreen world simulation keeps its normal
behavior, so NPC routines continue running.

## Overlay coverage

The native scene was checked for camera-attached and full-screen effects:

- **Weather fog and snowstorm fog:** fit to the projected viewport at each
  sprite's depth, including camera shear and a small border outside the screen.
  This removes the old camera-sized grey rectangle without disabling weather.
- **Clouds, normal/thunderstorm rain and splashes, snow, and snowstorm:** the
  seven camera-attached particle systems' spawn coverage follows the view.
  Particle size, speed, emission rate and maximum count stay unchanged. Existing
  particles finish their normal lifetimes; changing zoom does not resimulate
  the systems or multiply their particle budget.
- **Lightning and scene lighting:** use lighting rather than a fixed rectangular
  overlay. Their behavior is unchanged.
- **Black-screen fades, dialogue, menus and other UI:** all seven native canvases
  use screen-space rendering and already cover the display independently of zoom.
- **Lower-layer rendering:** Silverpine Vertical Layers copies the main camera
  projection and refreshes when its size/aspect changes.

Weather sizing is updated only when the projection changes. Reset to 1.0x or
leaving gameplay restores the native weather transforms and particle settings.

## Build and validation

```powershell
dotnet build CameraZoom.csproj -c Release
dotnet run --project Tests/CameraZoom.Tests.csproj -c Release
```

When building elsewhere, pass `-p:SilverpineGameDir="C:\path\to\Silverpine"`.
The Windows test runner uses .NET Framework 4.8 with the installed Harmony
library. Version 1.0.1 passes 106 checks using a managed camera model and inspection
of the installed game's fog-grid IL. Release builds have no warnings or errors.
These checks do not replace a live Unity rendering check.
Runtime checks: load a save, try both endpoints and 1.0x, enter 0.8 and invalid
text, move near walls, resize the window, change maps, save/reload, and check
that zoom persists after a restart. Check fog and snowstorm coverage at 0.2x,
slider dragging over the inventory scroll area, focus retention, and charcoal
window opacity. Test the debug whole-tile view if installed.
