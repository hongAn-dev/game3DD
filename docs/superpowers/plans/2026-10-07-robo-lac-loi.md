# Robo Lạc Lối Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Re-theme game3DD into "Robo Lạc Lối" (robot ball in a post-apocalyptic low-poly world) across all 4 levels, with new effects, sounds and a control-panel UI, without changing the game rules.

**Architecture:** Swap visuals at prefab level (one prefab change reaches all 4 levels) using the existing `AssetReplacer` editor script. Per-level look (terrain colors, sky, fog, light, decorations, ambient and win/lose sounds) is applied by a new `ZoneDresser` editor script driven by one data table per zone. Runtime code changes stay small: a `Zones` lookup, a `RobotLights` component, a `WinBeacons` component and text changes in `GameManager`. Everything that edits assets runs from batch mode so it is reproducible.

**Tech Stack:** Unity 2020.3.19f1 (Built-in RP, legacy UI `Text`, Input Manager), C# 7.3, Unity Test Framework 1.1.29 (PlayMode + EditMode), Blender 4.2 (Python, background mode) for the robot and energy core models, Kenney / Quaternius CC0 packs.

**Spec:** `docs/superpowers/specs/2026-10-07-robo-lac-loi-design.md`

## Global Constraints

- Unity **2020.3.19f1**, Built-in Render Pipeline. Launch with the `unity` wrapper (`~/.local/bin/unity`), never the raw editor binary.
- Asset licenses: **CC0** only (Kenney, Quaternius); font is OpenSans already in `Assets/Fonts/OpenSans` (Apache 2.0). Every new `Assets/ThirdParty/<Pack>/` folder gets a `License.txt` with author, URL, license.
- Do **not** change game rules: beat-level scores, death on water/void, checkpoints, random spawners, difficulty values.
- Do **not** change terrain geometry or paths of the 4 levels.
- Keep the public members of `GameManager`, `Health`, `Damage`, `SpawnGameObjects`, `SetupLevel`, `BallUserControl`, `MonsterChaser`, `Treasure` (adding members is fine).
- Game title: **"Robo Lạc Lối"**. Zone names: Level1 "Bãi phế liệu", Level2 "Khu công nghiệp bỏ hoang", Level3 "Vùng hoang hóa", Level4 "Trạm căn cứ".
- Accent colors: energy cyan `#2EE6E6` (46,230,230), robot warm yellow `#FFC44D` (255,196,77), danger red-orange `#FF5A2E` (255,90,46). UI panels dark grey `#1E2328` at 85% alpha.
- `/tmp` is full on this machine: write all logs, test results and scratch files under `~/Downloads/claude/work/` (create `logs/` there).
- Unity Editor must be **closed** before any batch-mode command (Unity locks the project).
- Keep source files' existing encoding: game scripts are UTF-8 with BOM and CRLF; new files are UTF-8 (no BOM) with LF.

## Review Focus

1. **Restart after death / next level**: `GameManager.gm` is static; after `SceneManager.LoadScene` the new scene's manager must take over (Unity fake-null). Expect score display and intro card to work on the second level loaded in one session. Test: `ScoreDisplayIsResetAfterReload` (Task 1).
2. **Coin Bouncy / Player trails**: replacing a prefab's visual must not delete its `TrailRenderer`. Test: `ReplaceVisualKeepsTrailRenderer` (Task 2).
3. **Falling scrap (Enemy - Crater) with Rigidbody**: its rebuilt `MeshCollider` must stay `convex`, or Unity throws "Non-convex MeshCollider with non-kinematic Rigidbody". Test: `ReplaceVisualKeepsConvexFlag` (Task 2).
4. **Vietnamese text renders**: every UI `Text` must use OpenSans so diacritics show (MotionControl-Bold has no Vietnamese glyphs). Test: `AllUiTextsUseOpenSans` (Task 10).
5. **Decorations swapped onto the path**: a replaced decoration must keep the old footprint so it cannot block a corridor that was free before. Test: `DecorationFootprintNotLarger` (Task 6).

---

## File Structure

| Path | Responsibility |
|---|---|
| `Tools/blender/glb_to_fbx.py` | Convert a Poly Pizza GLB to FBX for Unity (removes stray `Icosphere`) |
| `Tools/blender/make_robot.py` | Build player robot ball FBX |
| `Tools/blender/make_core.py` | Build energy core FBX + HUD icon PNG |
| `Assets/ThirdParty/RoboLacLoi/` | Our own models, materials, sprites (`Models/`, `Materials/`, `Sprites/`) |
| `Assets/ThirdParty/QuaterniusRobotEnemy/` | Patrol robot FBX + License |
| `Assets/ThirdParty/KenneyCityKitIndustrial/`, `KenneySurvivalKit/`, `KenneySpaceStationKit/` | Zone props (`Models/*.fbx`, `Models/Textures/colormap.png`) + License |
| `Assets/ThirdParty/KenneyAudio/` | Chosen `.ogg` sound effects + License |
| `Assets/Editor/AssetReplacer.cs` | (modify) prefab visual swaps: player, cores, patrol robot, scrap |
| `Assets/Editor/ZoneDresser.cs` | Per-zone terrain colors, sky, fog, light, decorations, ambient, win/lose sounds |
| `Assets/Editor/EffectsTheme.cs` | Particle colors + sound clips on the 3 explosion prefabs |
| `Assets/Editor/UiTheme.cs` | Fonts, colors, Vietnamese strings, HUD icon, remove GitHub button, product name |
| `Assets/Editor/BuildScript.cs` | Batch Linux build |
| `Assets/Editor/ThirdPartyModelPostprocessor.cs` | (modify) import settings for new packs |
| `Assets/Scripts/Zones.cs` | Scene name → zone title/goal text |
| `Assets/Scripts/RobotLights.cs` | Robot light brightness by energy, red flash on hit |
| `Assets/Scripts/WinBeacons.cs` | Turn lights on when level is beaten |
| `Assets/Scripts/GameManager.cs` | (modify) "x / y" score, Vietnamese intro, `BeatLevelScore` getter |
| `Assets/Tests/PlayMode/GameplayTests.cs` | Play mode regression tests |
| `Assets/Tests/Editor/ThemeEditorTests.cs` | Edit mode tests for editor tooling |

Standard commands used below (run from `/home/ubuntu/Game3D/repo`):

```bash
mkdir -p ~/Downloads/claude/work/logs
# Run an editor method in batch mode
unity -batchmode -nographics -quit -projectPath . -executeMethod <Class.Method> -logFile ~/Downloads/claude/work/logs/<name>.log
grep -E "error CS|Exception|^<Class>:" ~/Downloads/claude/work/logs/<name>.log
# Run play mode tests
unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults ~/Downloads/claude/work/logs/playmode.xml -logFile ~/Downloads/claude/work/logs/playmode.log
grep -oE 'testcasecount="[0-9]+"|result="[A-Za-z]+"|passed="[0-9]+"|failed="[0-9]+"' ~/Downloads/claude/work/logs/playmode.xml | head -4
# Run edit mode tests
unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults ~/Downloads/claude/work/logs/editmode.xml -logFile ~/Downloads/claude/work/logs/editmode.log
grep -oE 'testcasecount="[0-9]+"|result="[A-Za-z]+"|passed="[0-9]+"|failed="[0-9]+"' ~/Downloads/claude/work/logs/editmode.xml | head -4
```

---

### Task 1: Test infrastructure and gameplay regression tests

**Files:**
- Modify: `ProjectSettings/ProjectSettings.asset` (`playModeTestRunnerEnabled: 0` → `1`)
- Create: `Assets/Tests/PlayMode/GameplayTests.cs`

Scene-dependent tests are `[UnityTest]` so `[UnitySetUp]` (which loads Level1) runs before them; pure logic tests are plain `[Test]`.

**Interfaces:**
- Consumes: `GameManager.gm`, `GameManager.score`, `GameManager.mainScoreDisplay`, `GameManager.beatEasyLevelScore`, `Treasure.value`, `Health.isAlive`, `GameSettings`.
- Produces: test class `GameplayTests` that later tasks extend.

`playModeTestRunnerEnabled: 1` makes Unity add NUnit and the test runner to `Assembly-CSharp`, so tests can use game classes without an asmdef (game scripts have no asmdef).

- [ ] **Step 1: Enable play mode tests for all assemblies**

```bash
sed -i 's/^  playModeTestRunnerEnabled: 0$/  playModeTestRunnerEnabled: 1/' ProjectSettings/ProjectSettings.asset
grep -n "playModeTestRunnerEnabled" ProjectSettings/ProjectSettings.asset
```
Expected: `playModeTestRunnerEnabled: 1`

- [ ] **Step 2: Write the tests**

`Assets/Tests/PlayMode/GameplayTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameplayTests {

	[UnitySetUp]
	public IEnumerator LoadLevel1() {
		GameSettings.difficulty = GameSettings.gameDifficulties.Easy;
		GameSettings.showIntroLevelMessage = false;
		Time.timeScale = 1;
		SceneManager.LoadScene("Level1");
		yield return null;
		yield return null;
	}

	[UnityTest]
	public IEnumerator CollectingEnergyCoreIncreasesScore() {
		GameObject player = GameObject.FindWithTag("Player");
		Treasure core = Object.FindObjectOfType<Treasure>();
		int expected = GameManager.gm.score + core.value;

		player.GetComponent<Rigidbody>().position = core.transform.position;
		yield return new WaitForFixedUpdate();
		yield return new WaitForFixedUpdate();

		Assert.AreEqual(expected, GameManager.gm.score);
	}

	[UnityTest]
	public IEnumerator PatrolRobotKillsPlayerOnContact() {
#if UNITY_EDITOR
		GameObject player = GameObject.FindWithTag("Player");
		Health health = player.GetComponent<Health>();
		GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy - Monster.prefab");
		Object.Instantiate(prefab, player.transform.position + new Vector3(3, 1, 0), Quaternion.identity);

		float end = Time.time + 6f;
		while (Time.time < end && health != null && health.isAlive)
			yield return null;

		Assert.IsTrue(health == null || !health.isAlive, "Player should die when the patrol robot reaches it");
#else
		yield return null;
#endif
	}

	[UnityTest]
	public IEnumerator ScoreDisplayIsResetAfterReload() {
		GameManager.gm.Collect(3);
		SceneManager.LoadScene("Level1");
		yield return null;
		yield return null;

		Assert.AreEqual(0, GameManager.gm.score);
		StringAssert.StartsWith("0", GameManager.gm.mainScoreDisplay.text);
	}
}
```

- [ ] **Step 3: Run tests on the current game (characterization)**

Run the play mode test command.
Expected: `testcasecount="3"`, `result="Passed"`, `failed="0"`. These pin current behaviour before any asset change. If `ScoreDisplayIsResetAfterReload` fails because `GameManager.gm` still points at the destroyed manager, fix `GameManager.Start` to always assign: replace `if (gm == null) gm = gameObject.GetComponent<GameManager>();` with `gm = this;`, re-run, expect pass.

- [ ] **Step 4: Commit**

```bash
git add ProjectSettings/ProjectSettings.asset Assets/Tests Assets/Scripts/GameManager.cs
git commit -m "test: add play mode gameplay regression tests"
```

---

### Task 2: Make `ReplaceVisual` safe for trails and convex colliders

**Files:**
- Modify: `Assets/Editor/AssetReplacer.cs` (`ReplaceVisual`, make it `public static`)
- Create: `Assets/Tests/Editor/ThemeEditorTests.cs`

**Interfaces:**
- Produces: `public static GameObject AssetReplacer.ReplaceVisual(GameObject root, GameObject model, bool removeAllChildren)` — used by Tasks 4, 5, 6.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/ThemeEditorTests.cs`:

```csharp
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ThemeEditorTests {

	static GameObject Model() {
		return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/KenneyNatureKit/Models/stone_largeA.fbx");
	}

	[Test]
	public void ReplaceVisualKeepsTrailRenderer() {
		GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		root.AddComponent<TrailRenderer>();

		AssetReplacer.ReplaceVisual(root, Model(), false);

		Assert.IsNotNull(root.GetComponent<TrailRenderer>());
		Assert.IsNull(root.GetComponent<MeshRenderer>());
		Object.DestroyImmediate(root);
	}

	[Test]
	public void ReplaceVisualKeepsConvexFlag() {
		GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
		Object.DestroyImmediate(root.GetComponent<BoxCollider>());
		root.AddComponent<MeshCollider>().convex = true;

		GameObject model = AssetReplacer.ReplaceVisual(root, Model(), false);

		MeshCollider[] colliders = model.GetComponentsInChildren<MeshCollider>();
		Assert.IsNotEmpty(colliders);
		foreach (MeshCollider collider in colliders)
			Assert.IsTrue(collider.convex);
		Object.DestroyImmediate(root);
	}
}
```

- [ ] **Step 2: Run edit mode tests, verify failure**

Expected: compile error `'AssetReplacer.ReplaceVisual(GameObject, GameObject, bool)' is inaccessible due to its protection level` (log shows `error CS0122`).

- [ ] **Step 3: Implement**

In `Assets/Editor/AssetReplacer.cs`:

1. Change `static GameObject ReplaceVisual(` to `public static GameObject ReplaceVisual(`.
2. Record convexity before removal: after `bool hadMeshCollider = ...;` add
   ```csharp
   bool convex = root.GetComponentsInChildren<MeshCollider>(true).Any(c => c.convex);
   ```
3. Replace the root renderer removal loop
   ```csharp
   foreach (Renderer renderer in root.GetComponents<Renderer>())
   	Object.DestroyImmediate(renderer);
   ```
   with (trails and particle renderers are not part of the visual):
   ```csharp
   foreach (Renderer renderer in root.GetComponents<Renderer>()) {
   	if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
   		Object.DestroyImmediate(renderer);
   }
   ```
4. In the mesh collider re-creation loop, set the flag:
   ```csharp
   foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true)) {
   	MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
   	collider.sharedMesh = filter.sharedMesh;
   	collider.convex = convex;
   }
   ```
5. In the child removal condition, only count mesh renderers so a child holding only a trail survives:
   `child.GetComponentsInChildren<Renderer>(true).Any(r => r is MeshRenderer || r is SkinnedMeshRenderer)`.

- [ ] **Step 4: Run edit mode tests, verify pass**

Expected: `testcasecount="2"`, `failed="0"`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/AssetReplacer.cs Assets/Tests/Editor
git commit -m "fix: keep trails and convex colliders when replacing visuals"
```

---

### Task 3: Robot ball and energy core models (Blender)

**Files:**
- Create: `Tools/blender/make_robot.py`, `Tools/blender/make_core.py`, `Tools/blender/glb_to_fbx.py`
- Create (generated): `Assets/ThirdParty/RoboLacLoi/Models/RoboBall.fbx`, `Assets/ThirdParty/RoboLacLoi/Models/EnergyCore.fbx`, `Assets/ThirdParty/RoboLacLoi/Sprites/core_icon.png`, `Assets/ThirdParty/RoboLacLoi/License.txt`

**Interfaces:**
- Produces: FBX material slot names used by Task 4: robot `RoboShell`, `RoboPanel`, `RoboLight`, `RoboEye`, `RoboRust`; core `CoreGlow`, `CoreFrame`. Mesh names `RoboBall`, `EnergyCore`.

Blender binary: `~/.local/blender-4.2.3-linux-x64/blender`.

- [ ] **Step 1: Write `Tools/blender/make_robot.py`**

```python
"""Build the player robot ball: radius 0.5 sphere, panel seams, cyan equator light, yellow eye, rust spots.
Usage: blender -b --python Tools/blender/make_robot.py -- <out.fbx>"""
import bpy, bmesh, math, random, sys

out = sys.argv[sys.argv.index('--') + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
random.seed(7)

def material(name, rgb):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    return m

mats = [material('RoboShell', (0.62, 0.64, 0.66)), material('RoboPanel', (0.22, 0.24, 0.27)),
        material('RoboLight', (0.18, 0.90, 0.90)), material('RoboEye', (1.0, 0.77, 0.30)),
        material('RoboRust', (0.45, 0.25, 0.12))]
SHELL, PANEL, LIGHT, EYE, RUST = range(5)

bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, radius=0.5)
obj = bpy.context.active_object
obj.name = obj.data.name = 'RoboBall'
for m in mats:
    obj.data.materials.append(m)

bm = bmesh.new()
bm.from_mesh(obj.data)
for f in bm.faces:
    c = f.calc_center_median()
    lat = math.degrees(math.asin(max(-1, min(1, c.z / 0.5))))
    lon = math.degrees(math.atan2(c.y, c.x))
    if abs(lat) < 6:
        f.material_index = LIGHT
    elif c.y < -0.38 and abs(c.x) < 0.12 and 8 < lat < 30:
        f.material_index = EYE
    elif abs((lon + 180) % 60) < 4 or abs(abs(lat) - 45) < 3:
        f.material_index = PANEL
    elif random.random() < 0.06:
        f.material_index = RUST
    else:
        f.material_index = SHELL
bm.to_mesh(obj.data)
bm.free()
bpy.ops.object.shade_flat()

dims = obj.dimensions
assert all(abs(d - 1.0) < 0.01 for d in dims), dims
used = {p.material_index for p in obj.data.polygons}
assert used == {SHELL, PANEL, LIGHT, EYE, RUST}, used
bpy.ops.export_scene.fbx(filepath=out, use_selection=False, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', bake_space_transform=True)
print('ROBOT_OK', out)
```

- [ ] **Step 2: Run it**

```bash
mkdir -p Assets/ThirdParty/RoboLacLoi/Models Assets/ThirdParty/RoboLacLoi/Sprites
~/.local/blender-4.2.3-linux-x64/blender -b --python Tools/blender/make_robot.py -- Assets/ThirdParty/RoboLacLoi/Models/RoboBall.fbx > ~/Downloads/claude/work/logs/robot.log 2>&1
grep -E "ROBOT_OK|Error|AssertionError" ~/Downloads/claude/work/logs/robot.log
```
Expected: `ROBOT_OK .../RoboBall.fbx`

- [ ] **Step 3: Write `Tools/blender/make_core.py`**

```python
"""Build the energy core pickup (cyan crystal in a 4-bar metal frame) and render a HUD icon.
Usage: blender -b --python Tools/blender/make_core.py -- <out.fbx> <icon.png>"""
import bpy, math, sys

out_fbx, out_png = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)

def material(name, rgb, emit=0.0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    m.use_nodes = True
    bsdf = m.node_tree.nodes['Principled BSDF']
    bsdf.inputs['Base Color'].default_value = (*rgb, 1)
    bsdf.inputs['Emission Color'].default_value = (*rgb, 1)
    bsdf.inputs['Emission Strength'].default_value = emit
    return m

glow = material('CoreGlow', (0.18, 0.90, 0.90), emit=3.0)
frame = material('CoreFrame', (0.45, 0.47, 0.50))

parts = []
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=0.5)
crystal = bpy.context.active_object
crystal.scale = (0.45, 0.45, 0.8)
crystal.data.materials.append(glow)
parts.append(crystal)
for i in range(4):
    a = math.radians(45 + 90 * i)
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=0.04, depth=0.9,
                                        location=(0.32 * math.cos(a), 0.32 * math.sin(a), 0))
    bar = bpy.context.active_object
    bar.data.materials.append(frame)
    parts.append(bar)
for z in (-0.45, 0.45):
    bpy.ops.mesh.primitive_torus_add(major_radius=0.32, minor_radius=0.04, major_segments=12, minor_segments=4,
                                     location=(0, 0, z))
    ring = bpy.context.active_object
    ring.data.materials.append(frame)
    parts.append(ring)

bpy.ops.object.select_all(action='DESELECT')
for p in parts:
    p.select_set(True)
bpy.context.view_layer.objects.active = crystal
bpy.ops.object.join()
core = bpy.context.active_object
core.name = core.data.name = 'EnergyCore'
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
assert {m.name for m in core.data.materials} == {'CoreGlow', 'CoreFrame'}
bpy.ops.export_scene.fbx(filepath=out_fbx, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', bake_space_transform=True)

# HUD icon: orthographic front view, transparent background.
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'FLAT'
scene.display.shading.color_type = 'MATERIAL'
scene.render.film_transparent = True
scene.render.resolution_x = scene.render.resolution_y = 256
bpy.ops.object.camera_add(location=(0, -5, 0), rotation=(math.radians(90), 0, 0))
cam = bpy.context.active_object
cam.data.type = 'ORTHO'
cam.data.ortho_scale = 1.9
scene.camera = cam
scene.render.filepath = out_png
bpy.ops.render.render(write_still=True)
print('CORE_OK', out_fbx, out_png)
```

- [ ] **Step 4: Run it**

```bash
~/.local/blender-4.2.3-linux-x64/blender -b --python Tools/blender/make_core.py -- Assets/ThirdParty/RoboLacLoi/Models/EnergyCore.fbx Assets/ThirdParty/RoboLacLoi/Sprites/core_icon.png > ~/Downloads/claude/work/logs/core.log 2>&1
grep -E "CORE_OK|Error" ~/Downloads/claude/work/logs/core.log
```
Expected: `CORE_OK ...`. Open `core_icon.png` with the Read tool: a cyan crystal with grey frame on a transparent background.

- [ ] **Step 5: Write `Tools/blender/glb_to_fbx.py`** (used in Task 5; same conversion that produced `BlueDemon.fbx`)

```python
"""Convert a glTF/GLB character to FBX with baked animations. Removes stray 'Icosphere' meshes.
Usage: blender -b --python Tools/blender/glb_to_fbx.py -- <in.glb> <out.fbx>"""
import bpy, sys

src, dst = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
for o in [o for o in bpy.data.objects if o.name.startswith('Icosphere')]:
    bpy.data.objects.remove(o, do_unlink=True)
print('ACTIONS', [a.name for a in bpy.data.actions])
bpy.ops.export_scene.fbx(filepath=dst, apply_scale_options='FBX_SCALE_ALL', bake_anim=True,
                         bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, add_leaf_bones=False,
                         axis_forward='-Z', axis_up='Y', path_mode='COPY', embed_textures=True)
print('FBX_OK', dst)
```

- [ ] **Step 6: Add license file and commit**

`Assets/ThirdParty/RoboLacLoi/License.txt`:
```
Robo Lac Loi models (RoboBall, EnergyCore, core_icon) were made for this project with Tools/blender/*.py.
License: same as the project (MIT).
```

```bash
git add Tools/blender Assets/ThirdParty/RoboLacLoi
git commit -m "feat: generate robot ball and energy core models with Blender"
```

---

### Task 4: Player robot and energy cores in prefabs

**Files:**
- Modify: `Assets/Editor/AssetReplacer.cs` (add `RoboMaterials`, `EnsureMaterial`, `RemapMaterials`, `ReplacePlayer`, `ReplaceCores`; call them from `ReplaceAll`)
- Modify: `Assets/Tests/PlayMode/GameplayTests.cs` (add `PlayerUsesRobotBall`, `CoinsUseEnergyCore`)
- Prefabs changed by the run: `Player`, `Coin`, `Coin Bouncy` (and nested `Sil Coins`, `Vin Coins` follow automatically)

**Interfaces:**
- Consumes: `ReplaceVisual` (Task 2), material slot names (Task 3).
- Produces: `static Material AssetReplacer.EnsureMaterial(string path, Color color, Color emission)` and `static void AssetReplacer.RemapMaterials(string modelFolder, string materialFolder)` reused by Tasks 5 and 6. Material asset `Assets/ThirdParty/RoboLacLoi/Materials/RoboLight.mat` used by `RobotLights` (Task 8).

- [ ] **Step 1: Write failing tests** (append to `GameplayTests`)

```csharp
	[UnityTest]
	public IEnumerator PlayerUsesRobotBall() {
		yield return null;
		GameObject player = GameObject.FindWithTag("Player");
		Assert.AreEqual("RoboBall", player.GetComponent<MeshFilter>().sharedMesh.name);
		Assert.IsNotNull(player.GetComponent<TrailRenderer>());
	}

	[UnityTest]
	public IEnumerator CoinsUseEnergyCore() {
		yield return null;
		Treasure core = Object.FindObjectOfType<Treasure>();
		MeshFilter filter = core.GetComponentInChildren<MeshFilter>();
		Assert.AreEqual("EnergyCore", filter.sharedMesh.name);
	}
```

- [ ] **Step 2: Run play mode tests, verify the 2 new tests fail**

Expected: `failed="2"` (mesh names `Sphere` / `PickupPrototype...`).

- [ ] **Step 3: Implement in `AssetReplacer.cs`**

Add constants and data:

```csharp
	const string RoboFolder = "Assets/ThirdParty/RoboLacLoi/";
	static readonly Color Cyan = new Color32(46, 230, 230, 255);

	// Material name -> (albedo, emission). Emission black = not emissive.
	static readonly Dictionary<string, Color[]> RoboMaterials = new Dictionary<string, Color[]> {
		{ "RoboShell", new[] { (Color)new Color32(158, 163, 168, 255), Color.black } },
		{ "RoboPanel", new[] { (Color)new Color32(56, 61, 69, 255), Color.black } },
		{ "RoboLight", new[] { Cyan, Cyan * 0.3f } },
		{ "RoboEye", new[] { (Color)new Color32(255, 196, 77, 255), new Color32(255, 196, 77, 255) * 0.8f } },
		{ "RoboRust", new[] { (Color)new Color32(115, 64, 31, 255), Color.black } },
		{ "CoreGlow", new[] { Cyan, Cyan * 1.5f } },
		{ "CoreFrame", new[] { (Color)new Color32(115, 120, 128, 255), Color.black } },
	};
```

Add helpers (generalises the existing Kenney remap; refactor `RemapKenneyMaterials` to call `RemapMaterials(KenneyModels.TrimEnd('/'), KenneyModels + "Materials")` after creating its materials):

```csharp
	public static Material EnsureMaterial(string path, Color color, Color emission) {
		Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
		if (material == null) {
			material = new Material(Shader.Find("Standard"));
			AssetDatabase.CreateAsset(material, path);
		}
		material.color = color;
		material.SetFloat("_Glossiness", 0.25f);
		if (emission.maxColorComponent > 0f) {
			material.EnableKeyword("_EMISSION");
			material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
		} else {
			material.DisableKeyword("_EMISSION");
		}
		material.SetColor("_EmissionColor", emission);
		EditorUtility.SetDirty(material);
		return material;
	}

	/// <summary>
	/// Remaps every model in modelFolder to same-named materials found in materialFolder.
	/// </summary>
	public static void RemapMaterials(string modelFolder, string materialFolder) {
		AssetDatabase.SaveAssets();
		foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { modelFolder })) {
			ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
			foreach (string matGuid in AssetDatabase.FindAssets("t:Material", new[] { materialFolder })) {
				Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(matGuid));
				importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name), material);
			}
			importer.SaveAndReimport();
		}
	}
```

`AddRemap` for a material name the model does not use is harmless.

Add the two replacements:

```csharp
	static void ReplacePlayer() {
		string folder = RoboFolder + "Materials";
		if (!AssetDatabase.IsValidFolder(folder))
			AssetDatabase.CreateFolder(RoboFolder.TrimEnd('/'), "Materials");
		foreach (KeyValuePair<string, Color[]> pair in RoboMaterials)
			EnsureMaterial(folder + "/" + pair.Key + ".mat", pair.Value[0], pair.Value[1]);
		RemapMaterials(RoboFolder + "Models", folder);

		string modelPath = RoboFolder + "Models/RoboBall.fbx";
		Mesh mesh = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Mesh>().First();
		Material[] materials = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath)
			.GetComponentInChildren<MeshRenderer>().sharedMaterials;

		GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
		try {
			// The ball keeps its rigidbody, sphere collider (radius 0.5) and trail; only the mesh changes.
			root.GetComponent<MeshFilter>().sharedMesh = mesh;
			root.GetComponent<MeshRenderer>().sharedMaterials = materials;
			TrailRenderer trail = root.GetComponent<TrailRenderer>();
			if (trail != null) {
				trail.startColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f);
				trail.endColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0f);
			}
			PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Player.prefab");
			Debug.Log("AssetReplacer: Player -> RoboBall");
		} finally {
			PrefabUtility.UnloadPrefabContents(root);
		}
	}

	static void ReplaceCores() {
		GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(RoboFolder + "Models/EnergyCore.fbx");
		foreach (string name in new[] { "Coin", "Coin Bouncy" }) {
			string path = "Assets/Prefabs/" + name + ".prefab";
			GameObject root = PrefabUtility.LoadPrefabContents(path);
			try {
				if (root.transform.Find(ModelChildName) != null)
					continue;
				GameObject instance = ReplaceVisual(root, model, false);
				// Spec: about 1.3x the old coin so it reads from the gameplay camera.
				instance.transform.localScale *= 1.3f;
				PrefabUtility.SaveAsPrefabAsset(root, path);
				Debug.Log("AssetReplacer: " + name + " -> EnergyCore");
			} finally {
				PrefabUtility.UnloadPrefabContents(root);
			}
		}
	}
```

In `ReplaceAll()`, after `ReplaceMonsterPrefab();` add `ReplacePlayer(); ReplaceCores();`.

- [ ] **Step 4: Run the replacer, then play mode tests**

```bash
unity -batchmode -nographics -quit -projectPath . -executeMethod AssetReplacer.ReplaceAll -logFile ~/Downloads/claude/work/logs/replace.log
grep -E "error CS|Exception|AssetReplacer: (Player|Coin)" ~/Downloads/claude/work/logs/replace.log
```
Expected: `AssetReplacer: Player -> RoboBall`, `AssetReplacer: Coin -> EnergyCore`, `AssetReplacer: Coin Bouncy -> EnergyCore`, no errors. Then play mode tests: `testcasecount="5"`, `failed="0"`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/AssetReplacer.cs Assets/Tests Assets/Prefabs Assets/ThirdParty/RoboLacLoi
git commit -m "feat: robot ball player and energy core pickups"
```

---

### Task 5: Patrol robot enemy and falling scrap

**Files:**
- Create: `Assets/ThirdParty/QuaterniusRobotEnemy/RobotEnemy.fbx`, `Assets/ThirdParty/QuaterniusRobotEnemy/License.txt`
- Create: `Assets/ThirdParty/KenneySpaceStationKit/Models/container.fbx` (+ `Models/Textures/colormap.png`, `License.txt`) — the full kit is added in Task 6; this task only needs `container.fbx`, so copy the whole `Models/FBX format` folder now.
- Modify: `Assets/Editor/AssetReplacer.cs` (`MonsterModel` constant, add `ReplaceScrap`), `Assets/Editor/ThirdPartyModelPostprocessor.cs` (monster folder)

**Interfaces:**
- Consumes: `glb_to_fbx.py` (Task 3), `ReplaceVisual` (Task 2), `PatrolRobotKillsPlayerOnContact` test (Task 1).
- Produces: prefab `Enemy - Monster` with `MonsterChaser.runClip == "Run"`, `attackClip == "Attack"`.

Source: Poly Pizza "Robot Enemy" by Quaternius (CC0), `https://poly.pizza/m/1gNo5ezvmr`, GLB `https://static.poly.pizza/9c45ab2b-c46a-4319-bc2a-88d6dbbc8e42.glb` (already downloaded to `~/Downloads/claude/work/r_1gNo5ezvmr.glb`). Clips: Attack, Death, Idle, Jump, Run, Shoot, Walk.

- [ ] **Step 1: Write the failing test** (append to `GameplayTests`)

```csharp
	[Test]
	public void PatrolRobotUsesRobotClips() {
#if UNITY_EDITOR
		GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy - Monster.prefab");
		MonsterChaser chaser = prefab.GetComponent<MonsterChaser>();
		Assert.AreEqual("Run", chaser.runClip);
		Assert.AreEqual("Attack", chaser.attackClip);
		Assert.IsNotNull(prefab.GetComponentInChildren<Animation>().GetClip("Attack"));
#endif
	}
```

- [ ] **Step 2: Run play mode tests, verify it fails**

Expected: `PatrolRobotUsesRobotClips` fails (`attackClip` is `Punch`).

- [ ] **Step 3: Convert and copy assets**

```bash
mkdir -p Assets/ThirdParty/QuaterniusRobotEnemy
~/.local/blender-4.2.3-linux-x64/blender -b --python Tools/blender/glb_to_fbx.py -- ~/Downloads/claude/work/r_1gNo5ezvmr.glb Assets/ThirdParty/QuaterniusRobotEnemy/RobotEnemy.fbx > ~/Downloads/claude/work/logs/enemy.log 2>&1
grep -E "ACTIONS|FBX_OK" ~/Downloads/claude/work/logs/enemy.log
P=~/Downloads/claude/work/packs/kenney_space-station-kit
mkdir -p Assets/ThirdParty/KenneySpaceStationKit
cp -r "$P/Models/FBX format" Assets/ThirdParty/KenneySpaceStationKit/Models
cp "$P/License.txt" Assets/ThirdParty/KenneySpaceStationKit/License.txt
ls Assets/ThirdParty/KenneySpaceStationKit/Models/container.fbx Assets/ThirdParty/KenneySpaceStationKit/Models/Textures/colormap.png
```

`Assets/ThirdParty/QuaterniusRobotEnemy/License.txt`:
```
Robot Enemy by Quaternius - https://poly.pizza/m/1gNo5ezvmr
License: CC0 1.0 Universal (Public Domain) - https://creativecommons.org/publicdomain/zero/1.0/
RobotEnemy.fbx was converted from the GLB with Tools/blender/glb_to_fbx.py.
```

- [ ] **Step 4: Implement**

`ThirdPartyModelPostprocessor.cs`: change `const string MonstersFolder = "Assets/ThirdParty/QuaterniusUltimateMonsters/";` to accept both folders:

```csharp
	static bool IsMonster(string path) {
		return path.StartsWith("Assets/ThirdParty/QuaterniusUltimateMonsters/") || path.StartsWith("Assets/ThirdParty/QuaterniusRobotEnemy/");
	}
```
and use `IsMonster(assetPath)` in both `OnPreprocessModel` and `OnPreprocessAnimation`. Also add Kenney kits to the static-prop branch:
```csharp
		if (assetPath.StartsWith(KenneyFolder) || assetPath.StartsWith("Assets/ThirdParty/KenneyCityKitIndustrial/")
			|| assetPath.StartsWith("Assets/ThirdParty/KenneySurvivalKit/") || assetPath.StartsWith("Assets/ThirdParty/KenneySpaceStationKit/")) {
```
`OnPreprocessAnimation` already strips `CharacterArmature|` and `_CharacterArmature`; add `"Run"`-style loop detection unchanged.

`AssetReplacer.cs`:
- `const string MonsterModel = "Assets/ThirdParty/QuaterniusRobotEnemy/RobotEnemy.fbx";`
- attack clip pick: `AnimationClip attack = clips.FirstOrDefault(c => c.name == "Attack") ?? clips.FirstOrDefault(c => c.name == "Punch");`
- The monster prefab already holds the Blue Demon `Model` child, so the "already replaced" guard would skip it. Change the guard in `ReplaceMonsterPrefab` to skip only when the child's source model is the current one:
  ```csharp
  Transform existing = root.transform.Find(ModelChildName);
  if (existing != null && PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == model) {
  	Debug.Log("AssetReplacer: " + MonsterPrefab + " already replaced, skipping");
  	return;
  }
  ```
  `ReplaceVisual(root, model, true)` then removes the Blue Demon child (it holds renderers) and the capsule collider is rebuilt because `hadChildCollider` is false now — so also remove the old root `CapsuleCollider` before calling `ReplaceVisual` and set `hadChildCollider` by passing it: simplest is
  ```csharp
  foreach (CapsuleCollider capsule in root.GetComponents<CapsuleCollider>())
  	Object.DestroyImmediate(capsule);
  GameObject instance = ReplaceVisual(root, model, true);
  if (root.GetComponent<CapsuleCollider>() == null)
  	AddFittedCapsule(root);
  ```
  and extract the capsule-fitting block at the end of `ReplaceVisual` into `static void AddFittedCapsule(GameObject root)` (called from both places).
- Add scrap:
  ```csharp
  static void ReplaceScrap() {
  	GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/KenneySpaceStationKit/Models/container.fbx");
  	string path = "Assets/Prefabs/Enemy - Crater.prefab";
  	GameObject root = PrefabUtility.LoadPrefabContents(path);
  	try {
  		if (root.transform.Find(ModelChildName) != null)
  			return;
  		ReplaceVisual(root, model, false);
  		PrefabUtility.SaveAsPrefabAsset(root, path);
  		Debug.Log("AssetReplacer: Enemy - Crater -> container");
  	} finally {
  		PrefabUtility.UnloadPrefabContents(root);
  	}
  }
  ```
  and call `ReplaceScrap();` in `ReplaceAll()`.
- `MonsterChaser` default `attackClip` stays `"Anim_Attack"` (prefab value is what matters).

- [ ] **Step 5: Run replacer and tests**

Run replacer; expect `AssetReplacer: Assets/Prefabs/Enemy - Monster.prefab -> Assets/ThirdParty/QuaterniusRobotEnemy/RobotEnemy.fbx (Attack, Death, Idle, Jump, Run, Shoot, Walk)` and `AssetReplacer: Enemy - Crater -> container`. Play mode tests: `testcasecount="6"`, `failed="0"` (includes `PatrolRobotKillsPlayerOnContact`). Edit mode tests still pass.

- [ ] **Step 6: Commit**

```bash
git add Assets/Editor Assets/ThirdParty/QuaterniusRobotEnemy Assets/ThirdParty/KenneySpaceStationKit Assets/Prefabs Assets/Tests
git commit -m "feat: patrol robot enemy and falling scrap container"
```

---

### Task 6: Zone dresser + Level1 "Bãi phế liệu" (test slice)

**Files:**
- Create: `Assets/ThirdParty/KenneyCityKitIndustrial/{Models,License.txt}`, `Assets/ThirdParty/KenneySurvivalKit/{Models,License.txt}` (copy `Models/FBX format` folder incl. `Textures/`)
- Create: `Assets/Scripts/WinBeacons.cs`
- Create: `Assets/Editor/ZoneDresser.cs`
- Modify: `Assets/Tests/Editor/ThemeEditorTests.cs` (add `DecorationFootprintNotLarger`)

**Interfaces:**
- Consumes: `AssetReplacer.ReplaceVisual`, `AssetReplacer.EnsureMaterial`.
- Produces: `public static void ZoneDresser.DressAll()` and `public static void ZoneDresser.Dress(string sceneName)`; `class ZoneDresser.Zone` data table extended in Task 7; `WinBeacons.lights` (`Light[]`).

- [ ] **Step 1: Copy the two kits**

```bash
for k in "city-kit-industrial_2.0:KenneyCityKitIndustrial" "survival-kit:KenneySurvivalKit"; do
  src=~/Downloads/claude/work/packs/kenney_${k%%:*}; dst=Assets/ThirdParty/${k##*:}
  mkdir -p "$dst"; cp -r "$src/Models/FBX format" "$dst/Models"; cp "$src/License.txt" "$dst/License.txt"
done
ls Assets/ThirdParty/KenneyCityKitIndustrial/Models/shipping-container-a.fbx Assets/ThirdParty/KenneySurvivalKit/Models/barrel.fbx
```

- [ ] **Step 2: Write the failing edit mode test**

```csharp
	[Test]
	public void DecorationFootprintNotLarger() {
		GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
		root.transform.localScale = new Vector3(2, 1, 2);
		Bounds before = root.GetComponent<Renderer>().bounds;
		GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/KenneyCityKitIndustrial/Models/shipping-container-a.fbx");

		ZoneDresser.SwapDecoration(root, model);

		Bounds after = new Bounds(root.transform.position, Vector3.zero);
		foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
			after.Encapsulate(r.bounds);
		Assert.LessOrEqual(Mathf.Max(after.size.x, after.size.z), Mathf.Max(before.size.x, before.size.z) + 0.01f);
		Object.DestroyImmediate(root);
	}
```

- [ ] **Step 3: Run edit mode tests, verify failure**

Expected: `error CS0103: The name 'ZoneDresser' does not exist`.

- [ ] **Step 4: Write `Assets/Scripts/WinBeacons.cs`**

```csharp
using UnityEngine;

/// <summary>
/// Turns the base station lights on when the level is beaten.
/// </summary>
public class WinBeacons : MonoBehaviour {

	public Light[] lights;

	void Start() {
		SetLights(false);
	}

	void Update() {
		if (GameManager.gm != null && GameManager.gm.gameState != GameManager.gameStates.Playing
			&& GameManager.gm.gameState != GameManager.gameStates.Death) {
			SetLights(true);
			enabled = false;
		}
	}

	void SetLights(bool on) {
		foreach (Light light in lights)
			if (light != null)
				light.enabled = on;
	}
}
```

> `GameOver` state is also reached after a death; guard by checking the player is alive: replace the condition with `GameManager.gm.gameState == GameManager.gameStates.BeatLevel || (GameManager.gm.gameState == GameManager.gameStates.GameOver && GameManager.gm.player != null && GameManager.gm.player.GetComponent<Health>() != null && GameManager.gm.player.GetComponent<Health>().isAlive)`. Keep it in one private `bool LevelBeaten()` method.

- [ ] **Step 5: Write `Assets/Editor/ZoneDresser.cs`**

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Applies the Robo Lac Loi zone look to each level: terrain colors, sky, fog, sun, decorations,
/// ambient loop and win/lose sounds. Run with -executeMethod ZoneDresser.DressAll
/// </summary>
public static class ZoneDresser {

	public class Zone {
		public string scene;
		public Color ground, accent, rock;
		public Color skyTint, ground_sky, fog, sun;
		public float fogDensity, sunIntensity;
		public string ambient;     // Assets path of a looping clip, or null
		public bool beacons;       // add win beacons on decorations
		public Dictionary<string, string> props;  // old prefab name -> "Pack/model"
	}

	const string ThirdParty = "Assets/ThirdParty/";
	const string ZoneMaterials = "Assets/ThirdParty/RoboLacLoi/Materials/Zones/";

	static Color C(int r, int g, int b) { return new Color32((byte)r, (byte)g, (byte)b, 255); }

	public static readonly Zone[] Zones = {
		new Zone {
			scene = "Level1",
			ground = C(196, 160, 98), accent = C(122, 74, 44), rock = C(110, 104, 96),
			skyTint = C(214, 178, 120), ground_sky = C(120, 98, 70), fog = C(204, 176, 128), sun = C(255, 226, 180),
			fogDensity = 0.012f, sunIntensity = 1.1f,
			ambient = "Assets/ThirdParty/KenneyAudio/computerNoise_000.ogg",
			props = new Dictionary<string, string> {
				{ "Tree_1", "KenneyCityKitIndustrial/shipping-container-a" },
				{ "Tree_2", "KenneyCityKitIndustrial/detail-tank" },
				{ "Rock_1", "KenneySurvivalKit/barrel" },
				{ "Rock_5", "KenneySurvivalKit/box-large" },
				{ "Rock_6", "KenneySpaceStationKit/skip-rocks" },
				{ "Stone_1", "KenneySurvivalKit/metal-panel-screws" },
				{ "Log_2", "KenneySpaceStationKit/pipe" },
			},
		},
	};

	[MenuItem("Tools/Robo Lac Loi/Dress All Zones")]
	public static void DressAll() {
		foreach (Zone zone in Zones)
			Dress(zone.scene);
		Debug.Log("ZoneDresser: done");
	}

	public static void Dress(string sceneName) {
		Zone zone = Zones.First(z => z.scene == sceneName);
		if (!AssetDatabase.IsValidFolder(ZoneMaterials.TrimEnd('/')))
			AssetDatabase.CreateFolder("Assets/ThirdParty/RoboLacLoi/Materials", "Zones");

		var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
		ColorTerrain(zone);
		SetSkyFogSun(zone);
		List<GameObject> swapped = SwapDecorations(zone, scene);
		AddAmbient(zone);
		if (zone.beacons)
			AddBeacons(swapped);
		EditorSceneManager.MarkSceneDirty(scene);
		EditorSceneManager.SaveScene(scene);
		Debug.Log("ZoneDresser: " + sceneName + " dressed, " + swapped.Count + " decorations swapped");
	}

	static Material ZoneMaterial(Zone zone, string role, Color color) {
		return AssetReplacer.EnsureMaterial(ZoneMaterials + zone.scene + "_" + role + ".mat", color, Color.black);
	}

	// The terrain prefab of each level ("Level1 Terrain") uses the shared color materials in Assets/Models/Materials.
	// Ground-like colors become the zone ground, browns the accent, everything else the rock color.
	static void ColorTerrain(Zone zone) {
		string path = "Assets/Prefabs/" + zone.scene + " Terrain.prefab";
		Material ground = ZoneMaterial(zone, "Ground", zone.ground);
		Material accent = ZoneMaterial(zone, "Accent", zone.accent);
		Material rock = ZoneMaterial(zone, "Rock", zone.rock);

		GameObject root = PrefabUtility.LoadPrefabContents(path);
		try {
			foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) {
				Material[] materials = renderer.sharedMaterials;
				for (int i = 0; i < materials.Length; i++) {
					string name = materials[i] != null ? materials[i].name : "";
					if (name.StartsWith(zone.scene + "_"))
						continue;
					if (name.StartsWith("Green") || name.StartsWith("Yellow") || name.StartsWith("Orange") || name.StartsWith("Pink"))
						materials[i] = ground;
					else if (name.StartsWith("Brown"))
						materials[i] = accent;
					else
						materials[i] = rock;
				}
				renderer.sharedMaterials = materials;
			}
			PrefabUtility.SaveAsPrefabAsset(root, path);
		} finally {
			PrefabUtility.UnloadPrefabContents(root);
		}
	}

	static void SetSkyFogSun(Zone zone) {
		string skyPath = ZoneMaterials + zone.scene + "_Sky.mat";
		Material sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
		if (sky == null) {
			sky = new Material(Shader.Find("Skybox/Procedural"));
			AssetDatabase.CreateAsset(sky, skyPath);
		}
		sky.SetColor("_SkyTint", zone.skyTint);
		sky.SetColor("_GroundColor", zone.ground_sky);
		sky.SetFloat("_AtmosphereThickness", 1.4f);
		sky.SetFloat("_Exposure", 1.1f);
		EditorUtility.SetDirty(sky);

		RenderSettings.skybox = sky;
		RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
		RenderSettings.fog = true;
		RenderSettings.fogMode = FogMode.ExponentialSquared;
		RenderSettings.fogColor = zone.fog;
		RenderSettings.fogDensity = zone.fogDensity;

		foreach (Light light in Object.FindObjectsOfType<Light>().Where(l => l.type == LightType.Directional)) {
			light.color = zone.sun;
			light.intensity = zone.sunIntensity;
		}
	}

	static GameObject LoadProp(string packAndModel) {
		string[] parts = packAndModel.Split('/');
		string path = ThirdParty + parts[0] + "/Models/" + parts[1] + ".fbx";
		GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
		if (model == null)
			throw new System.Exception("ZoneDresser: missing model " + path);
		return model;
	}

	/// <summary>
	/// Replaces the visual of one decoration with model, keeping position, rotation and footprint.
	/// </summary>
	public static void SwapDecoration(GameObject decoration, GameObject model) {
		if (PrefabUtility.IsPartOfPrefabInstance(decoration))
			PrefabUtility.UnpackPrefabInstance(decoration, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
		AssetReplacer.ReplaceVisual(decoration, model, false);
		decoration.name = model.name;
	}

	static List<GameObject> SwapDecorations(Zone zone, UnityEngine.SceneManagement.Scene scene) {
		var swapped = new List<GameObject>();
		var targets = new List<KeyValuePair<GameObject, string>>();
		foreach (GameObject root in scene.GetRootGameObjects()) {
			foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) {
				if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
					continue;
				GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
				string model;
				if (source != null && zone.props.TryGetValue(source.name, out model))
					targets.Add(new KeyValuePair<GameObject, string>(t.gameObject, model));
			}
		}
		foreach (var target in targets) {
			SwapDecoration(target.Key, LoadProp(target.Value));
			swapped.Add(target.Key);
		}
		return swapped;
	}

	static void AddAmbient(Zone zone) {
		GameManager manager = Object.FindObjectOfType<GameManager>();
		if (manager == null || zone.ambient == null)
			return;
		AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(zone.ambient);
		Transform existing = manager.transform.Find("Ambient");
		GameObject ambient = existing != null ? existing.gameObject : new GameObject("Ambient");
		ambient.transform.SetParent(manager.transform, false);
		AudioSource source = ambient.GetComponent<AudioSource>() ?? ambient.AddComponent<AudioSource>();
		source.clip = clip;
		source.loop = true;
		source.playOnAwake = true;
		source.volume = 0.15f;
		source.spatialBlend = 0f;
	}

	static void AddBeacons(List<GameObject> decorations) {
		GameManager manager = Object.FindObjectOfType<GameManager>();
		WinBeacons beacons = manager.GetComponent<WinBeacons>() ?? manager.gameObject.AddComponent<WinBeacons>();
		var lights = new List<Light>();
		foreach (GameObject decoration in decorations.Take(8)) {
			GameObject go = new GameObject("Beacon Light");
			go.transform.SetParent(decoration.transform, false);
			go.transform.localPosition = Vector3.up * 2f;
			Light light = go.AddComponent<Light>();
			light.type = LightType.Point;
			light.color = new Color32(46, 230, 230, 255);
			light.range = 10f;
			light.intensity = 2.5f;
			lights.Add(light);
		}
		beacons.lights = lights.ToArray();
	}
}
```

> `AddAmbient` needs the clip in `Assets/ThirdParty/KenneyAudio/`, created in Task 9. Until then `LoadAssetAtPath` returns null and the source has no clip; that is fine for this task.

- [ ] **Step 6: Run edit mode tests, verify pass**

Expected: `testcasecount="3"`, `failed="0"`.

- [ ] **Step 7: Dress Level1 and take screenshots**

```bash
unity -batchmode -nographics -quit -projectPath . -executeMethod ZoneDresser.DressAll -logFile ~/Downloads/claude/work/logs/zones.log
grep -E "error CS|Exception|ZoneDresser:" ~/Downloads/claude/work/logs/zones.log
```
Expected: `ZoneDresser: Level1 dressed, N decorations swapped` with N between 5 and 12, then `ZoneDresser: done`.

Screenshot (temporary script, not committed): create `Assets/Editor/TmpShot.cs`:

```csharp
using UnityEditor; using UnityEditor.SceneManagement; using UnityEngine;
public static class TmpShot {
	public static void Run() {
		string dir = System.Environment.GetEnvironmentVariable("SHOT_DIR");
		foreach (string level in new[] { "Level1", "Level2", "Level3", "Level4" }) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level + ".unity");
			Camera cam = Camera.main;
			GameObject player = GameObject.FindWithTag("Player");
			cam.transform.position = player.transform.position + new Vector3(0, 7, -9);
			cam.transform.LookAt(player.transform);
			var rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt; cam.Render();
			RenderTexture.active = rt; var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
			tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
			System.IO.File.WriteAllBytes(dir + "/" + level + ".png", tex.EncodeToPNG());
			cam.targetTexture = null; RenderTexture.active = null;
		}
	}
}
```
```bash
SHOT_DIR=~/Downloads/claude/work unity -batchmode -quit -projectPath . -executeMethod TmpShot.Run -logFile ~/Downloads/claude/work/logs/shot.log
```
Open `~/Downloads/claude/work/Level1.png` with the Read tool. Check: robot ball, cores and props are clearly distinguishable; sand/rust palette; nothing blocks the path near the player. **Show the image to the user and wait for approval of the look before Task 7** (spec §4 step 1 gate). Delete `Assets/Editor/TmpShot.cs*` before committing.

- [ ] **Step 8: Run all tests, commit**

Play mode and edit mode tests pass.

```bash
git add Assets/Editor/ZoneDresser.cs Assets/Scripts/WinBeacons.cs Assets/ThirdParty Assets/Scenes/Level1.unity Assets/Prefabs Assets/Tests
git commit -m "feat: zone dresser and Level1 scrapyard zone"
```

---

### Task 7: Zones Level2–4

**Files:**
- Modify: `Assets/Editor/ZoneDresser.cs` (add 3 entries to `Zones`)
- Scenes changed by the run: `Level2.unity`, `Level3.unity`, `Level4.unity`, terrain prefabs `Level2..4 Terrain.prefab`

**Interfaces:**
- Consumes: `ZoneDresser.Zone`, `ZoneDresser.DressAll`.

- [ ] **Step 1: Write the failing test** (append to `ThemeEditorTests`)

```csharp
	[Test]
	public void EveryLevelHasAZone() {
		foreach (string level in new[] { "Level1", "Level2", "Level3", "Level4" })
			Assert.IsTrue(ZoneDresser.Zones.Any(z => z.scene == level), level);
	}
```
(add `using System.Linq;` at the top of the test file)

Run edit mode tests → `EveryLevelHasAZone` fails for Level2.

- [ ] **Step 2: Add the zones** (inside the `Zones` array, after Level1)

```csharp
		new Zone {
			scene = "Level2",
			ground = C(118, 122, 126), accent = C(140, 58, 40), rock = C(84, 88, 94),
			skyTint = C(150, 165, 180), ground_sky = C(70, 74, 80), fog = C(150, 158, 166), sun = C(220, 230, 240),
			fogDensity = 0.014f, sunIntensity = 0.95f,
			ambient = "Assets/ThirdParty/KenneyAudio/spaceEngineLow_000.ogg",
			props = new Dictionary<string, string> {
				{ "Rock_1", "KenneyCityKitIndustrial/detail-tank-large" },
				{ "Rock_2", "KenneyCityKitIndustrial/shipping-container-b" },
				{ "Rock_3", "KenneyCityKitIndustrial/chimney-small" },
				{ "Rock_4", "KenneyCityKitIndustrial/shipping-container-c" },
				{ "Rock_5", "KenneyCityKitIndustrial/detail-tank" },
				{ "Stone_1", "KenneySurvivalKit/barrel" },
			},
		},
		new Zone {
			scene = "Level3",
			ground = C(104, 132, 72), accent = C(120, 110, 96), rock = C(132, 134, 128),
			skyTint = C(160, 190, 170), ground_sky = C(80, 96, 70), fog = C(170, 190, 168), sun = C(250, 244, 220),
			fogDensity = 0.010f, sunIntensity = 1.05f,
			ambient = null,
			props = new Dictionary<string, string> {
				{ "Stone_1", "KenneySurvivalKit/metal-panel-screws-half" },
				{ "Rock_4", "KenneySurvivalKit/structure-metal-wall" },
				{ "Rock_6", "KenneySpaceStationKit/skip-rocks" },
			},
		},
		new Zone {
			scene = "Level4",
			ground = C(92, 112, 124), accent = C(70, 82, 94), rock = C(120, 132, 140),
			skyTint = C(110, 140, 170), ground_sky = C(50, 60, 72), fog = C(120, 140, 160), sun = C(200, 220, 255),
			fogDensity = 0.012f, sunIntensity = 0.9f,
			ambient = "Assets/ThirdParty/KenneyAudio/spaceEngineLow_000.ogg",
			beacons = true,
			props = new Dictionary<string, string> {
				{ "Tree_1", "KenneySpaceStationKit/structure" },
				{ "Tree_2", "KenneyCityKitIndustrial/solar-panel-portrait" },
				{ "Tree_3", "KenneyCityKitIndustrial/chimney-basic" },
				{ "Bush_1", "KenneySpaceStationKit/container" },
				{ "Bush_2", "KenneySpaceStationKit/container-wide" },
				{ "Bush_3", "KenneySpaceStationKit/computer-system" },
				{ "Rock_2", "KenneySpaceStationKit/container-tall" },
				{ "Rock_5", "KenneySpaceStationKit/pipe-ring" },
				{ "Log_1", "KenneySpaceStationKit/pipe" },
			},
		},
```

- [ ] **Step 3: Run edit mode tests → pass; dress the zones**

`ZoneDresser.DressAll` re-runs Level1 too; it is idempotent (already-swapped objects are no longer prefab instances; terrain materials prefixed with the scene name are skipped).
Expected log: four `ZoneDresser: LevelN dressed` lines. Level4 decorations swapped > 50.

- [ ] **Step 4: Screenshots of all 4 levels** (TmpShot from Task 6, then delete it). Check each zone palette reads differently and the path stays clear. Show the 4 images to the user.

- [ ] **Step 5: Play mode tests pass; commit**

```bash
git add Assets/Editor/ZoneDresser.cs Assets/Scenes Assets/Prefabs Assets/ThirdParty/RoboLacLoi Assets/Tests
git commit -m "feat: industrial, wilds and base station zones"
```

---

### Task 8: Robot lights

**Files:**
- Create: `Assets/Scripts/RobotLights.cs`
- Modify: `Assets/Scripts/GameManager.cs` (add `public int BeatLevelScore { get { return beatLevelScore; } }`)
- Modify: `Assets/Editor/AssetReplacer.cs` (`ReplacePlayer` adds `RobotLights` to the Player prefab)
- Modify: `Assets/Tests/PlayMode/GameplayTests.cs`

**Interfaces:**
- Produces: `public static float RobotLights.Brightness(int score, int target)` → 0.3 at 0, 1.0 at or above target; `RobotLights.lightSlotName = "RoboLight"`.

- [ ] **Step 1: Failing tests**

```csharp
	[Test]
	public void RobotLightBrightnessFollowsEnergy() {
		Assert.AreEqual(0.3f, RobotLights.Brightness(0, 10), 0.001f);
		Assert.AreEqual(0.65f, RobotLights.Brightness(5, 10), 0.001f);
		Assert.AreEqual(1f, RobotLights.Brightness(15, 10), 0.001f);
		Assert.AreEqual(1f, RobotLights.Brightness(0, 0), 0.001f);
	}

	[UnityTest]
	public IEnumerator PlayerHasRobotLights() {
		yield return null;
		Assert.IsNotNull(GameObject.FindWithTag("Player").GetComponent<RobotLights>());
	}
```

Run → compile error `RobotLights` not found.

- [ ] **Step 2: Implement `Assets/Scripts/RobotLights.cs`**

```csharp
using UnityEngine;

/// <summary>
/// Robot light strip: brighter as energy is collected, flashes red when the robot is hit.
/// </summary>
public class RobotLights : MonoBehaviour {

	public string lightSlotName = "RoboLight";
	public Color energyColor = new Color32(46, 230, 230, 255);
	public Color hitColor = new Color32(255, 90, 46, 255);
	public float hitFlashSeconds = 0.3f;

	private Material lightMaterial;
	private Health health;
	private float lastHealth;
	private float hitUntil;

	public static float Brightness(int score, int target) {
		if (target <= 0)
			return 1f;
		return Mathf.Lerp(0.3f, 1f, Mathf.Clamp01((float)score / target));
	}

	void Start() {
		health = GetComponent<Health>();
		lastHealth = health != null ? health.healthPoints : 0f;
		// renderer.materials creates instances, so other robots are not affected.
		foreach (Material material in GetComponent<Renderer>().materials)
			if (material.name.StartsWith(lightSlotName))
				lightMaterial = material;
	}

	void Update() {
		if (lightMaterial == null)
			return;
		if (health != null && health.healthPoints < lastHealth)
			hitUntil = Time.time + hitFlashSeconds;
		if (health != null)
			lastHealth = health.healthPoints;

		Color color;
		if (Time.time < hitUntil) {
			color = hitColor;
		} else {
			GameManager gm = GameManager.gm;
			float brightness = gm != null ? Brightness(gm.score, gm.BeatLevelScore) : 0.3f;
			color = energyColor * brightness;
		}
		lightMaterial.SetColor("_EmissionColor", color);
	}
}
```

Add the getter to `GameManager` next to `private int beatLevelScore = 0;` (keep CRLF/BOM):
```csharp
	public int BeatLevelScore { get { return beatLevelScore; } }
```

In `AssetReplacer.ReplacePlayer`, before `SaveAsPrefabAsset`:
```csharp
			if (root.GetComponent<RobotLights>() == null)
				root.AddComponent<RobotLights>();
```
`ReplacePlayer` has no "already replaced" guard, so re-running `AssetReplacer.ReplaceAll` applies it.

- [ ] **Step 3: Run replacer, play mode tests pass** (`failed="0"`).

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/RobotLights.cs Assets/Scripts/GameManager.cs Assets/Editor/AssetReplacer.cs Assets/Prefabs/Player.prefab Assets/Tests
git commit -m "feat: robot light strip reacts to energy and hits"
```

---

### Task 9: Effects and sounds

**Files:**
- Create: `Assets/ThirdParty/KenneyAudio/` (chosen `.ogg` files + `License.txt`)
- Create: `Assets/Editor/EffectsTheme.cs`
- Modify: `Assets/Editor/ZoneDresser.cs` (`gameOverSFX`, `beatLevelSFX` per scene)
- Modify: `Assets/Tests/Editor/ThemeEditorTests.cs`

**Interfaces:**
- Produces: `public static void EffectsTheme.Apply()`.

Clips (from `~/Downloads/claude/work/packs/`):

| Use | File |
|---|---|
| Core pickup (`ExplodeCoin Particle`) | `kenney_sci-fi-sounds/Audio/forceField_000.ogg` |
| Robot destroyed (`ExplodeEnemy Particle`) | `kenney_sci-fi-sounds/Audio/explosionCrunch_000.ogg` |
| Player destroyed (`ExplodePlayer Particle`) | `kenney_sci-fi-sounds/Audio/lowFrequency_explosion_000.ogg` |
| Lose ("Mất kết nối", `GameManager.gameOverSFX`) | `kenney_interface-sounds/Audio/glitch_004.ogg` |
| Win ("Đủ năng lượng", `GameManager.beatLevelSFX`) | `kenney_interface-sounds/Audio/confirmation_004.ogg` |
| Button click | `kenney_interface-sounds/Audio/click_002.ogg` |
| Ambient Level1 | `kenney_sci-fi-sounds/Audio/computerNoise_000.ogg` |
| Ambient Level2, Level4 | `kenney_sci-fi-sounds/Audio/spaceEngineLow_000.ogg` |

- [ ] **Step 1: Copy clips**

```bash
P=~/Downloads/claude/work/packs; D=Assets/ThirdParty/KenneyAudio; mkdir -p $D
for f in kenney_sci-fi-sounds/Audio/forceField_000.ogg kenney_sci-fi-sounds/Audio/explosionCrunch_000.ogg kenney_sci-fi-sounds/Audio/lowFrequency_explosion_000.ogg kenney_interface-sounds/Audio/glitch_004.ogg kenney_interface-sounds/Audio/confirmation_004.ogg kenney_interface-sounds/Audio/click_002.ogg kenney_sci-fi-sounds/Audio/computerNoise_000.ogg kenney_sci-fi-sounds/Audio/spaceEngineLow_000.ogg; do cp "$P/$f" $D/; done
cp $P/kenney_sci-fi-sounds/License.txt $D/License.txt
ls $D | wc -l
```
Expected: `9`.

- [ ] **Step 2: Failing test**

```csharp
	[Test]
	public void ExplosionPrefabsUseThemeSounds() {
		var expected = new System.Collections.Generic.Dictionary<string, string> {
			{ "ExplodeCoin Particle", "forceField_000" },
			{ "ExplodeEnemy Particle", "explosionCrunch_000" },
			{ "ExplodePlayer Particle", "lowFrequency_explosion_000" },
		};
		foreach (var pair in expected) {
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + pair.Key + ".prefab");
			Assert.AreEqual(pair.Value, prefab.GetComponentInChildren<AudioSource>().clip.name, pair.Key);
		}
	}
```
Run edit mode → fails (`CoinExplosion` etc.).

- [ ] **Step 3: Implement `Assets/Editor/EffectsTheme.cs`**

```csharp
using UnityEditor;
using UnityEngine;

/// <summary>
/// Recolors the three explosion particle prefabs and assigns the theme sounds.
/// Run with -executeMethod EffectsTheme.Apply
/// </summary>
public static class EffectsTheme {

	const string Audio = "Assets/ThirdParty/KenneyAudio/";

	[MenuItem("Tools/Robo Lac Loi/Apply Effects")]
	public static void Apply() {
		Theme("ExplodeCoin Particle", "forceField_000", new Color32(46, 230, 230, 255), new Color32(220, 255, 255, 255));
		Theme("ExplodeEnemy Particle", "explosionCrunch_000", new Color32(255, 140, 50, 255), new Color32(90, 90, 90, 255));
		Theme("ExplodePlayer Particle", "lowFrequency_explosion_000", new Color32(255, 90, 46, 255), new Color32(255, 200, 80, 255));
		AssetDatabase.SaveAssets();
		Debug.Log("EffectsTheme: done");
	}

	static void Theme(string prefabName, string clipName, Color a, Color b) {
		string path = "Assets/Prefabs/" + prefabName + ".prefab";
		GameObject root = PrefabUtility.LoadPrefabContents(path);
		try {
			foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true)) {
				ParticleSystem.MainModule main = system.main;
				main.startColor = new ParticleSystem.MinMaxGradient(a, b);
			}
			foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
				source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Audio + clipName + ".ogg");
			PrefabUtility.SaveAsPrefabAsset(root, path);
			Debug.Log("EffectsTheme: " + prefabName);
		} finally {
			PrefabUtility.UnloadPrefabContents(root);
		}
	}
}
```

In `ZoneDresser.Dress`, after `AddAmbient(zone);` add:
```csharp
		GameManager manager = Object.FindObjectOfType<GameManager>();
		if (manager != null) {
			manager.gameOverSFX = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ThirdParty/KenneyAudio/glitch_004.ogg");
			manager.beatLevelSFX = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ThirdParty/KenneyAudio/confirmation_004.ogg");
		}
```

- [ ] **Step 4: Run `EffectsTheme.Apply` and `ZoneDresser.DressAll`; edit mode + play mode tests pass.**

- [ ] **Step 5: Commit**

```bash
git add Assets/ThirdParty/KenneyAudio Assets/Editor Assets/Prefabs Assets/Scenes Assets/Tests
git commit -m "feat: themed explosion effects, win/lose sounds and zone ambience"
```

---

### Task 10: Control-panel UI and Vietnamese text

**Files:**
- Create: `Assets/Scripts/Zones.cs`
- Create: `Assets/Editor/UiTheme.cs`
- Modify: `Assets/Scripts/GameManager.cs` (`Collect`, intro text)
- Modify: `ProjectSettings/ProjectSettings.asset` (`productName`)
- Modify: tests

**Interfaces:**
- Produces: `public static string Zones.Title(string sceneName)`; `public static void UiTheme.Apply()`.

- [ ] **Step 1: Failing tests**

PlayMode (`GameplayTests`):
```csharp
	[UnityTest]
	public IEnumerator ScoreShowsCollectedOverTarget() {
		yield return null;
		GameManager gm = GameManager.gm;
		Assert.AreEqual("0 / " + gm.beatEasyLevelScore, gm.mainScoreDisplay.text);
	}

	[Test]
	public void ZoneTitlesAreVietnamese() {
		Assert.AreEqual("Bãi phế liệu", Zones.Title("Level1"));
		Assert.AreEqual("Khu công nghiệp bỏ hoang", Zones.Title("Level2"));
		Assert.AreEqual("Vùng hoang hóa", Zones.Title("Level3"));
		Assert.AreEqual("Trạm căn cứ", Zones.Title("Level4"));
		Assert.AreEqual("", Zones.Title("MainMenu"));
	}
```

EditMode (`ThemeEditorTests`):
```csharp
	[Test]
	public void AllUiTextsUseOpenSans() {
		foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })) {
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
			foreach (UnityEngine.UI.Text text in prefab.GetComponentsInChildren<UnityEngine.UI.Text>(true))
				StringAssert.StartsWith("OpenSans", text.font.name, prefab.name + "/" + text.name);
		}
	}
```
Run → failures.

- [ ] **Step 2: `Assets/Scripts/Zones.cs`**

```csharp
/// <summary>
/// Zone names of the levels, shown on the HUD and the intro card.
/// </summary>
public static class Zones {

	public static string Title(string sceneName) {
		switch (sceneName) {
			case "Level1": return "Bãi phế liệu";
			case "Level2": return "Khu công nghiệp bỏ hoang";
			case "Level3": return "Vùng hoang hóa";
			case "Level4": return "Trạm căn cứ";
			default: return "";
		}
	}
}
```

- [ ] **Step 3: `GameManager` text changes** (keep CRLF + BOM)

- In `Collect`: `mainScoreDisplay.text = score.ToString () + " / " + beatLevelScore.ToString ();`
- Intro: `introBeatLevelText.text = Zones.Title (SceneManager.GetActiveScene ().name).ToUpper () + "\nTHU " + beatLevelScore.ToString () + " LÕI NĂNG LƯỢNG";`

- [ ] **Step 4: `Assets/Editor/UiTheme.cs`**

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Control-panel look for all UI: OpenSans font, dark panels with cyan accents, Vietnamese strings,
/// energy core icon next to the score, no GitHub button. Run with -executeMethod UiTheme.Apply
/// </summary>
public static class UiTheme {

	static readonly Color Panel = new Color32(30, 35, 40, 217);
	static readonly Color Cyan = new Color32(46, 230, 230, 255);
	static readonly Color TextColor = new Color32(235, 242, 245, 255);

	// Old text (prefix match, case sensitive) -> new text.
	static readonly Dictionary<string, string> Strings = new Dictionary<string, string> {
		{ "LiBot Adventure", "Robo Lạc Lối" },
		{ "Play Again", "Thử lại" },
		{ "Play", "Chơi" },
		{ "Quit", "Thoát" },
		{ "Easy", "Dễ" },
		{ "Normal", "Thường" },
		{ "Hard", "Khó" },
		{ "Level Victory!", "Đủ năng lượng!" },
		{ "Main Menu", "Menu chính" },
		{ "Next Level", "Khu tiếp theo" },
		{ "CONGRATULATIONS!", "ĐÃ VỀ TỚI CĂN CỨ!\nRobo đã được sạc đầy." },
		{ "Thanks for playing", "Cảm ơn bạn đã chơi!" },
	};

	[MenuItem("Tools/Robo Lac Loi/Apply UI Theme")]
	public static void Apply() {
		Font bold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/OpenSans/OpenSansBold.ttf");
		Sprite icon = CoreIcon();

		foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })) {
			string path = AssetDatabase.GUIDToAssetPath(guid);
			GameObject root = PrefabUtility.LoadPrefabContents(path);
			try {
				if (Style(root, bold, icon))
					PrefabUtility.SaveAsPrefabAsset(root, path);
			} finally {
				PrefabUtility.UnloadPrefabContents(root);
			}
		}
		foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes) {
			var scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
			bool changed = false;
			foreach (GameObject root in scene.GetRootGameObjects())
				changed |= Style(root, bold, icon);
			if (changed)
				EditorSceneManager.SaveScene(scene);
		}
		PlayerSettings.productName = "Robo Lac Loi";
		AssetDatabase.SaveAssets();
		Debug.Log("UiTheme: done");
	}

	static Sprite CoreIcon() {
		string path = "Assets/ThirdParty/RoboLacLoi/Sprites/core_icon.png";
		TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
		if (importer.textureType != TextureImporterType.Sprite) {
			importer.textureType = TextureImporterType.Sprite;
			importer.alphaIsTransparency = true;
			importer.SaveAndReimport();
		}
		return AssetDatabase.LoadAssetAtPath<Sprite>(path);
	}

	/// <summary>Returns true when anything under root changed.</summary>
	static bool Style(GameObject root, Font font, Sprite icon) {
		bool changed = false;

		foreach (Button button in root.GetComponentsInChildren<Button>(true).ToList()) {
			Text label = button.GetComponentInChildren<Text>(true);
			if (label != null && label.text.StartsWith("Open Github")) {
				Object.DestroyImmediate(button.gameObject);
				changed = true;
				continue;
			}
			Image image = button.GetComponent<Image>();
			if (image != null) {
				image.color = Panel;
				Outline outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
				outline.effectColor = Cyan;
				outline.effectDistance = new Vector2(2, -2);
			}
			ColorBlock colors = button.colors;
			colors.highlightedColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.6f);
			colors.selectedColor = colors.highlightedColor;
			colors.pressedColor = Cyan;
			button.colors = colors;
			changed = true;
		}

		foreach (Text text in root.GetComponentsInChildren<Text>(true)) {
			text.font = font;
			text.color = TextColor;
			foreach (KeyValuePair<string, string> pair in Strings) {
				if (text.text.StartsWith(pair.Key)) {
					text.text = pair.Value;
					break;
				}
			}
			changed = true;
		}

		// Energy core icon left of the score text ("Score Text" is the GameManager.mainScoreDisplay in each level).
		foreach (Text score in root.GetComponentsInChildren<Text>(true).Where(t => t.name == "Score Text")) {
			if (score.transform.parent.Find("Core Icon") != null)
				continue;
			GameObject go = new GameObject("Core Icon", typeof(RectTransform), typeof(Image));
			go.transform.SetParent(score.transform.parent, false);
			RectTransform rect = go.GetComponent<RectTransform>();
			RectTransform scoreRect = score.GetComponent<RectTransform>();
			rect.anchorMin = rect.anchorMax = scoreRect.anchorMin;
			rect.pivot = new Vector2(1f, 0.5f);
			rect.sizeDelta = new Vector2(scoreRect.rect.height, scoreRect.rect.height);
			rect.anchoredPosition = scoreRect.anchoredPosition - new Vector2(scoreRect.rect.width * scoreRect.pivot.x + 8f, 0f);
			go.GetComponent<Image>().sprite = icon;
			go.GetComponent<Image>().preserveAspect = true;
			changed = true;
		}
		return changed;
	}
}
```

Lose message: the `GameOver Canvas` prefab shows the score; add a title. In `Style`, after the text loop:
```csharp
		if (root.name == "GameOver Canvas" && root.transform.Find("Lost Title") == null) {
			GameObject title = new GameObject("Lost Title", typeof(RectTransform), typeof(Text));
			title.transform.SetParent(root.transform, false);
			RectTransform rect = title.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0f, 0.62f);
			rect.anchorMax = new Vector2(1f, 0.82f);
			rect.offsetMin = rect.offsetMax = Vector2.zero;
			Text text = title.GetComponent<Text>();
			text.text = "MẤT KẾT NỐI";
			text.font = font;
			text.fontSize = 64;
			text.alignment = TextAnchor.MiddleCenter;
			text.color = new Color32(255, 90, 46, 255);
			changed = true;
		}
```
If the GameOver canvas has an English "Game Over" title image (a non-built-in sprite), disable that `Image` in the same block: `foreach (Image i in root.GetComponentsInChildren<Image>(true)) if (i.sprite != null && AssetDatabase.GetAssetPath(i.sprite).StartsWith("Assets/") && i.GetComponent<Button>() == null) i.enabled = false;` — check first with a screenshot that such an image exists; skip if not.

- [ ] **Step 5: Run `UiTheme.Apply`, all tests pass**

```bash
unity -batchmode -nographics -quit -projectPath . -executeMethod UiTheme.Apply -logFile ~/Downloads/claude/work/logs/ui.log
grep -E "error CS|Exception|UiTheme:" ~/Downloads/claude/work/logs/ui.log
```
Expected: `UiTheme: done`. Edit + play mode tests: `failed="0"`. Screenshot MainMenu and Level1 HUD (extend TmpShot with `EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity")`; for screen-space-overlay canvases switch the canvas to `RenderMode.ScreenSpaceCamera` with `Camera.main` inside the temporary script only). Verify diacritics render and the icon sits left of the score. Show to user.

- [ ] **Step 6: Update README title/credits, commit**

README: title `# Robo Lạc Lối`, add rows to the Assets table: Kenney City Kit (Industrial), Survival Kit, Space Station Kit, Sci-fi/Interface Sounds (CC0); Quaternius Robot Enemy (CC0); OpenSans (Apache 2.0).

```bash
git add Assets/Scripts Assets/Editor/UiTheme.cs Assets/Prefabs Assets/Scenes ProjectSettings/ProjectSettings.asset Assets/Tests README.md
git commit -m "feat: control-panel UI, Vietnamese text and zone intro"
```

---

### Task 11: PC build and final pass

**Files:**
- Create: `Assets/Editor/BuildScript.cs`

- [ ] **Step 1: `Assets/Editor/BuildScript.cs`**

```csharp
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildScript {

	[MenuItem("Tools/Robo Lac Loi/Build Linux")]
	public static void BuildLinux() {
		string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
		BuildReport report = BuildPipeline.BuildPlayer(scenes, "Builds/Linux/RoboLacLoi.x86_64",
			BuildTarget.StandaloneLinux64, BuildOptions.None);
		Debug.Log("BuildScript: " + report.summary.result + " " + report.summary.totalSize + " bytes");
		if (report.summary.result != BuildResult.Succeeded)
			EditorApplication.Exit(1);
	}
}
```

- [ ] **Step 2: Build**

```bash
unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildLinux -logFile ~/Downloads/claude/work/logs/build.log
grep -E "BuildScript:|error" ~/Downloads/claude/work/logs/build.log | tail -3
ls -la Builds/Linux/RoboLacLoi.x86_64
```
Expected: `BuildScript: Succeeded ...`. `Builds/` is already in `.gitignore`. (The Linux editor tarball includes Linux Mono standalone support; if the log says the module is missing, report it to the user instead of installing anything.)

- [ ] **Step 3: Full test run** — edit mode and play mode, both `failed="0"`.

- [ ] **Step 4: Smoke run the build** — launch `Builds/Linux/RoboLacLoi.x86_64 -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile ~/Downloads/claude/work/logs/player.log` in the background for 20 s, then check `player.log` has no `Exception`. Ask the user to play through Level1→Level4.

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/BuildScript.cs
git commit -m "build: batch Linux build script"
```
