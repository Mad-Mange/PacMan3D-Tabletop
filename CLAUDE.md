# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Pac-Man 3D is an arcade-accurate Pac-Man in Unity 6.6 (6000.6.5f1) with URP, rendered as a 3D-printed tabletop maze
(reference: `inspiration/InspirationPicture.png`, kept local and git-ignored because it is not our photo). Nearly everything is built from code at runtime. The only scene
is `Assets/Scenes/PacMan3D.unity`: camera, sun, a global volume and one `GameController`. The controller creates
the camera, light and volume itself if they are missing.

## Commands

Unity: `"C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe"`. Blender: `"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe"`.
Batch runs fail if the project is open in the editor (project lock), so close it first or use the editor menus.

- **Tests:** `Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults <xml> -logFile <log>`
  - Covers `Assets/Tests/EditMode/GameSimTests.cs` (NUnit) and only touches `PacMan.Core`.
- **Setup:** `-executeMethod PacMan.EditorTools.ProjectSetup.RunBatch -quit` (menu *Pac-Man > Setup Project*)
  - Regenerates the textures (`Assets/Art/Textures`), the materials (`Assets/Resources/Materials`), `Assets/Resources/PacManVolume.asset` and the scene.
  - Assets are overwritten in place, so GUIDs survive. Material colours live in `ProjectSetup.BuildMaterials`, so edit them there rather than in the .mat files.
- **Snapshots:** `-executeMethod PacMan.EditorTools.Snapshots.RunBatch -snapshotDir <dir>`
  - Drives `GameController.Step` manually in edit mode and saves PNGs, HUD included, via `GameController.Capture`.
  - This is the fastest way to check visual changes. Particles don't simulate in these shots.
- **Build:** `-executeMethod PacMan.EditorTools.BuildPlayer.Windows`. Output goes to `Builds/Windows/PacMan3D.exe`.
- **Models:** `blender.exe -b --factory-startup --python Blender/build_models.py` (characters, fruit) and `Blender/build_props.py` (table props) write `Assets/Resources/Models/*.fbx`.
- **Snapshot details:**
  - Outside play mode the first render lacks the colour LUT, so `Capture` renders twice.
  - Particles are advanced with `Tabletop.SimulateParticles`.
  - `-noShadows`, `-noSun` and `-noDof` switch those off when you are chasing an artefact.
  - Snapshots, like `-autoplay`, set `Settings.Persist = false`, so they never write PlayerPrefs (options or high score).

## Architecture

**`PacMan.Core` (Assets/Scripts/Core) is the whole rule set, with no rendering.**
- `GameSim.Tick(dt)` is stepped at a fixed 1/120 s and reports what happened through `Events`.
- Positions are floats in tile units. Tile (x, y) has its centre at (x, y), rows grow downward, and `Grid.TileOf` rounds halves up.
- Movement (`Advance`) snaps to each tile centre it reaches and asks a decide callback for the next direction. Turns happen at centres and reversals happen at once.
- Pac-Man may also turn within `CornerWindow` of a centre (`TryCorner`). He then glides back onto the lane while moving.
- The tunnel loops through hidden tiles: x = -2 and x = 29 are the same spot.
- Ghost targeting, waves, fright, the house release counters and Elroy follow the arcade (see README).
- `LevelSpec` holds the per-level tables.
- `Autopilot` is a BFS steering AI, used by the demo mode, `-autoplay` and the snapshots.

**`PacMan.Game` (Assets/Scripts/Game) mirrors the sim onto the 3D board.**
- Grid → world: `Board.ToWorld(tile) = (x, h, -y)`, so north is +z and the camera sits south.
- `Board` builds the walls procedurally:
  - It splits the solid tiles into rectangles; unreachable pockets count as solid.
  - Sides facing open tiles are inset by `Inset`; buried sides reach into their neighbour.
  - Blocks of 3x3 tiles or more become hollow boxes.
  - The walls are one mesh with three submeshes: side, top and rim.
- `ActorViews.cs` holds the Pac-Man, ghost and fruit views on top of the Blender models.
- `Art.Spawn` swaps imported FBX materials for `Resources/Materials/<same name>`. The Blender material names must match the material assets.
- `CameraRig` has three views:
  - Overview: pitch 54°, distance bisected to fit the board for the current aspect.
  - Follow: toggled with C.
  - Orbit: behind the title screen.
- The HUD is a bold 5x7 pixel font drawn with `Graphics.DrawTexture` (`Hud`, `GameController.Hud.cs`). The same code draws into render textures for snapshots.
- **Menus** (`GameController.Menu.cs`):
  - Pages: Title, Pause, Options. A page other than `None` takes all input in `HandleMenu`.
  - Hit rects for the mouse are recorded while drawing and used on the next frame.
  - The options live in `Settings` (PlayerPrefs). `Sfx.ApplyVolumes` maps them onto four AudioSources: effects, chomp, loop (siren/fright/eyes) and music (intro and title tune). Master goes on `AudioListener.volume`.
- **`Tabletop`** dresses the table and makes it react:
  - It splits the instantiated `Props` model into parts by name. Joystick and plant parts keep their FBX offsets under a group, so they pivot correctly.
  - The sun gets the `Resources/Textures/WindowCookie` light cookie; its offset is computed so the window centres on the board. Cloud dimming is Perlin noise on the sun's intensity.
  - The underglow follows `Tabletop.Mood`, computed by `GameController.Mood()`.
  - Spare-life figures follow `Sim.Lives - 1`: a new one pops in, and a lost one hops onto the board.
  - Won fruit, cheers and the start button are driven from `OnGameEvent`.
- Use `Art.Kill` rather than `Destroy` in view code: it also works outside play mode, where the snapshot tool runs.
- `PixelArt` holds the font, the icons and the fruit loader. `Resources/PixelArt/fruits.json` is shared with the Blender script.

**Models from Blender:**
- Modelled facing -Y with Z up, and exported as FBX (forward -Z, up Y, baked space transform).
- In Unity they import with identity transforms, front = +Z and 1 unit = 1 m.
- Pac-Man's jaws hinge about local X.

**Input** uses the Input System package (`activeInputHandler: 1`). `Controls.cs` also has a legacy fallback behind `#if`.
