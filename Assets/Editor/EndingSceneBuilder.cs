using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;

/// <summary>
/// Builds Assets/Scenes/Ending.unity (spec §8): the old launch pad with the damaged spaceship, a cinematic Robo, and a
/// 27 s Timeline (Assets/Scenes/Ending/EndingTimeline.playable, previewable in the Timeline window):
/// 0–3 fade in on the ship · 3–8 Robo rolls to the charging port · 8–13 energy beam, four ship lights come on,
/// fuel 100% · 13–17 the rear ramp opens, Robo rolls in, ramp closes · 17–23 take-off, camera pulls back ·
/// 23–27 space shot of the planet and the ship flying away. Then the completion screen (EndingController).
/// No gameplay objects. Re-running rebuilds the scene and the timeline. Run UiTheme.Apply afterwards.
/// </summary>
public static class EndingSceneBuilder {

	const string ScenePath = "Assets/Scenes/Ending.unity";
	const string Folder = "Assets/Scenes/Ending";
	const string TimelinePath = Folder + "/EndingTimeline.playable";
	const string Models = "Assets/ThirdParty/RoboLacLoi/Models/";
	const string Materials = "Assets/ThirdParty/RoboLacLoi/Materials/";
	public const float Duration = 27f;
	static readonly Vector3 SpaceOrigin = new Vector3(0f, 3000f, 0f);

	[MenuItem("Tools/Robo Lac Loi/Build Ending Scene")]
	public static void Build() {
		var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
		if (!AssetDatabase.IsValidFolder(Folder))
			AssetDatabase.CreateFolder("Assets/Scenes", "Ending");

		Camera camera = Camera.main;
		Object.DestroyImmediate(camera.GetComponent<AudioListener>());
		new GameObject("Listener", typeof(AudioListener));
		camera.farClipPlane = 600f;
		Light sun = Object.FindObjectOfType<Light>();
		sun.color = new Color32(200, 220, 255, 255);
		sun.intensity = 1f;
		sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
		RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Zones/Level4_Sky.mat");
		RenderSettings.fog = false;

		// ---------- set ----------
		Material ground = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Zones/Level4_Ground.mat");
		Material accent = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Zones/Level4_Accent.mat");
		Material glow = AssetDatabase.LoadAssetAtPath<Material>(Materials + "CoreGlow.mat");
		Disc("Launch Field", Vector3.zero, 140f, ground);
		Disc("Launch Pad", Vector3.up * 0.02f, 18f, accent);

		GameObject ship = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Models + "Spaceship.fbx"));
		ship.name = "Spaceship";
		ship.AddComponent<Animator>();
		Transform door = Child(ship, "Door"), port = Child(ship, "Port");
		Bounds hull = Child(ship, "Hull").GetComponent<Renderer>().bounds;
		Vector3 centre = new Vector3(hull.center.x, 0f, hull.center.z);
		Vector3 rear = Flat(door.position - centre).normalized;
		Vector3 side = Flat(port.position - centre - Vector3.Project(port.position - centre, rear)).normalized;   // towards the port
		Vector3 forward = -rear;

		// Props stay in front of the ship, away from every camera position (those are behind and to the sides).
		Prop("KenneySpaceStationKit/container-tall", centre + forward * 16f - side * 7f, 3f, 20f);
		Prop("KenneySpaceStationKit/container-wide", centre + forward * 15f + side * 9f, 4f, -35f);
		Prop("KenneyCityKitIndustrial/solar-panel-portrait", centre + forward * 22f, 3f, 80f);
		Prop("KenneyCityKitIndustrial/chimney-basic", centre + forward * 26f - side * 14f, 3f, 0f);
		Prop("KenneySpaceStationKit/computer-system", centre + forward * 11f + side * 3f, 2f, 140f);

		// Robo starts behind the ship on the port side, so its roll to the port never passes under the hull.
		GameObject robo = Robo(centre + rear * 14f + side * 11f + Vector3.up * 0.5f);
		Vector3 start = robo.transform.position;
		Vector3 charge = new Vector3(port.position.x, 0.5f, port.position.z) + side * 3f;
		Vector3 hinge = door.position;
		float doorLength = Child(ship, "Door").GetComponent<Renderer>().bounds.size.y;
		float drop = Mathf.Asin(Mathf.Clamp01((hinge.y - 0.05f) / doorLength)) * Mathf.Rad2Deg;
		Vector3 rampFoot = hinge + rear * doorLength * Mathf.Cos(drop * Mathf.Deg2Rad);
		rampFoot.y = 0.5f;
		Vector3 inside = hinge - rear * 2f + Vector3.up * 0.5f;
		Vector3 corner = new Vector3(charge.x, 0.5f, charge.z) + rear * Vector3.Dot(rampFoot - charge, rear);

		// Energy beam from Robo to the port, glows over the four light pods, engine flames.
		GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		beam.name = "Energy Beam";
		Object.DestroyImmediate(beam.GetComponent<Collider>());
		beam.transform.position = (charge + port.position) / 2f;
		beam.transform.up = (port.position - charge).normalized;
		beam.transform.localScale = new Vector3(0.22f, Vector3.Distance(charge, port.position) / 2f, 0.22f);
		beam.GetComponent<Renderer>().sharedMaterial = glow;
		beam.SetActive(false);
		var glows = new List<GameObject>();
		for (int i = 1; i <= 4; i++) {
			Transform pod = Child(ship, "Pod_" + i);
			GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			g.name = "Ship Light " + i;
			Object.DestroyImmediate(g.GetComponent<Collider>());
			g.transform.SetParent(ship.transform, true);
			g.transform.position = pod.GetComponent<Renderer>().bounds.center;
			g.transform.localScale = Vector3.one * 0.75f / ship.transform.lossyScale.x;
			g.GetComponent<Renderer>().sharedMaterial = glow;
			Light light = new GameObject("Light").AddComponent<Light>();
			light.transform.SetParent(g.transform, false);
			light.type = LightType.Point;
			light.color = new Color32(46, 230, 230, 255);
			light.range = 6f;
			light.intensity = 2f;
			g.SetActive(false);
			glows.Add(g);
		}
		GameObject flameL = Child(ship, "Flame_L").gameObject, flameR = Child(ship, "Flame_R").gameObject;
		flameL.SetActive(false);
		flameR.SetActive(false);

		// ---------- space set (same scene, far above): planet, stars, a second ship flying away ----------
		Camera spaceCamera = new GameObject("Space Camera").AddComponent<Camera>();
		spaceCamera.transform.position = SpaceOrigin;
		spaceCamera.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
		spaceCamera.clearFlags = CameraClearFlags.SolidColor;
		spaceCamera.backgroundColor = new Color32(3, 5, 12, 255);
		spaceCamera.farClipPlane = 2500f;
		spaceCamera.gameObject.SetActive(false);
		GameObject planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		planet.name = "Planet";
		Object.DestroyImmediate(planet.GetComponent<Collider>());
		planet.transform.position = SpaceOrigin + new Vector3(0f, -520f, 900f);
		planet.transform.localScale = Vector3.one * 900f;
		planet.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Acid.mat");
		Stars(SpaceOrigin);
		GameObject spaceShip = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Models + "Spaceship.fbx"));
		spaceShip.name = "Spaceship (space)";
		spaceShip.AddComponent<Animator>();
		Child(spaceShip, "Flame_L").gameObject.SetActive(true);
		Child(spaceShip, "Flame_R").gameObject.SetActive(true);
		foreach (int i in new[] { 1, 2, 3, 4 })
			Object.Instantiate(glows[i - 1], spaceShip.transform, false).SetActive(true);

		// ---------- audio ----------
		AudioSource wind = Audio("Audio Weak Power", "computerNoise_000"), charge_ = Audio("Audio Charge", "forceField_000"),
			full = Audio("Audio Fuel Full", "confirmation_004"), ignition = Audio("Audio Ignition", "lowFrequency_explosion_000"),
			engine = Audio("Audio Engine", "spaceEngineLow_000");

		// ---------- UI ----------
		Font bold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ChakraPetch/ChakraPetch-Bold.ttf");
		Font medium = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ChakraPetch/ChakraPetch-Medium.ttf");
		new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
		GameObject canvasGo = new GameObject("Ending Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
		canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
		CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = 0.5f;

		GameObject fade = Stretch(new GameObject("Fade", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(Animator)), canvasGo.transform);
		fade.GetComponent<Image>().color = Color.black;
		fade.GetComponent<Image>().raycastTarget = false;
		GameObject charging = Label(canvasGo.transform, "Caption Charging", "ĐANG NẠP NĂNG LƯỢNG…", medium, 52, -380f, new Vector2(1200f, 80f)).gameObject;
		GameObject fuel = Label(canvasGo.transform, "Caption Fuel", "NHIÊN LIỆU 100%", bold, 64, -380f, new Vector2(1200f, 90f)).gameObject;
		GameObject escaped = Label(canvasGo.transform, "Caption Escaped", "Robo đã thoát khỏi hành tinh. Hành trình tiếp tục.", medium, 52, -380f, new Vector2(1700f, 80f)).gameObject;
		foreach (GameObject caption in new[] { charging, fuel, escaped })
			caption.SetActive(false);

		GameObject panel = Stretch(new GameObject("Completion Panel", typeof(RectTransform)), canvasGo.transform);
		panel.SetActive(false);
		Label(panel.transform, "Title", "ROBO ĐÃ THOÁT KHỎI HÀNH TINH", bold, 84, 260f, new Vector2(1700f, 120f));
		Label(panel.transform, "Subtitle", "Hành trình tiếp tục.", medium, 48, 160f, new Vector2(1200f, 70f));

		EndingController controller = canvasGo.AddComponent<EndingController>();
		controller.completionPanel = panel;
		UnityEventTools.AddPersistentListener(Button(panel.transform, "Play Again Button", "CHƠI LẠI TỪ ĐẦU", bold, -40f).onClick, controller.PlayAgain);
		UnityEventTools.AddPersistentListener(Button(panel.transform, "Main Menu Button", "VỀ MENU", bold, -180f).onClick, controller.BackToMenu);
		Button skip = Button(canvasGo.transform, "Skip Button", "BỎ QUA", bold, 0f);
		RectTransform skipRect = (RectTransform)skip.transform;
		skipRect.anchorMin = skipRect.anchorMax = skipRect.pivot = new Vector2(1f, 0f);
		skipRect.anchoredPosition = new Vector2(-60f, 60f);
		skipRect.sizeDelta = new Vector2(320f, 100f);
		UnityEventTools.AddPersistentListener(skip.onClick, controller.Skip);
		skip.gameObject.SetActive(false);
		controller.skipButton = skip.gameObject;

		// ---------- timeline ----------
		AssetDatabase.DeleteAsset(TimelinePath);
		TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
		AssetDatabase.CreateAsset(timeline, TimelinePath);
		timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
		timeline.fixedDuration = Duration;

		GameObject directorGo = new GameObject("Ending Director");
		PlayableDirector director = directorGo.AddComponent<PlayableDirector>();
		director.playableAsset = timeline;
		director.playOnAwake = false;
		director.extrapolationMode = DirectorWrapMode.Hold;
		controller.director = director;

		// Robo: still, roll to the port, wait, round the ship to the ramp foot, up the ramp; hidden once inside.
		Robo(timeline, director, robo, new[] {
			K(0f, start), K(3.2f, start), K(5.5f, Vector3.Lerp(start, charge, 0.55f)), K(8f, charge), K(13.2f, charge),
			K(13.9f, corner), K(14.5f, rampFoot), K(15.6f, inside) });
		Activation(timeline, director, "Robo visible", robo, 0f, 15.8f);
		robo.SetActive(true);

		// Ship: ramp door open/close, then take-off.
		Quaternion closed = door.localRotation;
		Quaternion open = OpenDoor(door, rear, side, drop);
		var shipKeys = new List<KeyValuePair<float, Pose>> {
			Kp(0f, ship.transform.position, ship.transform.rotation), Kp(17f, ship.transform.position, ship.transform.rotation),
			Kp(18f, ship.transform.position + Vector3.up * 0.4f, ship.transform.rotation),
			Kp(19.5f, ship.transform.position + Vector3.up * 6f, ship.transform.rotation),
			Kp(21.2f, ship.transform.position + Vector3.up * 18f + forward * 10f, Quaternion.AngleAxis(-12f, Vector3.Cross(Vector3.up, forward)) * ship.transform.rotation),
			Kp(Duration, ship.transform.position + Vector3.up * 70f + forward * 90f, Quaternion.AngleAxis(-18f, Vector3.Cross(Vector3.up, forward)) * ship.transform.rotation),
		};
		AnimationClip shipClip = PoseClip("Ship", shipKeys, "");
		RotationCurve(shipClip, AnimationUtility.CalculateTransformPath(door, ship.transform), new[] {
			new KeyValuePair<float, Quaternion>(0f, closed), new KeyValuePair<float, Quaternion>(13.2f, closed),
			new KeyValuePair<float, Quaternion>(14.2f, open), new KeyValuePair<float, Quaternion>(15.9f, open),
			new KeyValuePair<float, Quaternion>(16.9f, closed), new KeyValuePair<float, Quaternion>(Duration, closed) });
		Animate(timeline, director, "Spaceship", ship.GetComponent<Animator>(), shipClip);
		Activation(timeline, director, "Energy beam", beam, 8f, 5f);
		for (int i = 0; i < 4; i++)
			Activation(timeline, director, "Ship light " + (i + 1), glows[i], 9f + i * 0.9f, Duration - 9f - i * 0.9f);
		Activation(timeline, director, "Flame L", flameL, 17f, Duration - 17f);
		Activation(timeline, director, "Flame R", flameR, 17f, Duration - 17f);

		// Camera: wide, follow Robo from behind, the charge, the ramp, pull back on take-off; then the space camera.
		Vector3 Follow(float t, Vector3 at, Vector3 dir) { return at - dir.normalized * 5.5f + Vector3.up * 2.4f + Vector3.Cross(Vector3.up, dir).normalized * 1.5f; }
		Vector3 travel = Flat(charge - start);
		Vector3 shipAt(float t) { return Sample(shipKeys, t).position; }
		var cam = new List<KeyValuePair<float, Pose>> {
			Look(0f, centre - side * 26f + rear * 14f + Vector3.up * 8f, centre + Vector3.up * 2f),
			Look(3f, centre - side * 22f + rear * 12f + Vector3.up * 6f, centre + Vector3.up * 2f),
			Look(3.6f, Follow(3.6f, start, travel), start),
			Look(5.5f, Follow(5.5f, Vector3.Lerp(start, charge, 0.55f), travel), Vector3.Lerp(start, charge, 0.55f)),
			Look(7.6f, Follow(7.6f, charge, travel), charge + Vector3.up * 0.8f),
			Look(8.4f, charge + side * 4f + rear * 6f + Vector3.up * 3f, (charge + port.position) / 2f + Vector3.up * 1.2f),
			Look(12.8f, charge + side * 6f + rear * 3f + Vector3.up * 5f, centre + Vector3.up * 3.5f),
			Look(13.3f, rampFoot + rear * 7f + side * 3f + Vector3.up * 2.8f, hinge),
			Look(16.9f, rampFoot + rear * 6f + side * 2.5f + Vector3.up * 2.2f, hinge + Vector3.up),
			Look(17.2f, centre - side * 20f + rear * 16f + Vector3.up * 4f, shipAt(17.2f) + Vector3.up * 3f),
			Look(19.5f, centre - side * 24f + rear * 22f + Vector3.up * 5f, shipAt(19.5f) + Vector3.up * 3f),
			Look(21.2f, centre - side * 30f + rear * 30f + Vector3.up * 7f, shipAt(21.2f) + Vector3.up * 3f),
			Look(23f, centre - side * 36f + rear * 38f + Vector3.up * 9f, shipAt(23f) + Vector3.up * 3f),
		};
		Animate(timeline, director, "Camera", camera.gameObject.AddComponent<Animator>(), PoseClip("Camera", cam, ""));
		Activation(timeline, director, "Pad camera", camera.gameObject, 0f, 23f);
		camera.gameObject.SetActive(true);
		Activation(timeline, director, "Space camera", spaceCamera.gameObject, 23f, Duration - 23f);
		Vector3 sA = SpaceOrigin + new Vector3(-18f, -22f, 70f), sB = SpaceOrigin + new Vector3(30f, 14f, 260f);
		Quaternion sR = Quaternion.LookRotation(sB - sA);
		spaceShip.transform.SetPositionAndRotation(sA, sR);
		Animate(timeline, director, "Spaceship (space)", spaceShip.GetComponent<Animator>(),
			PoseClip("SpaceShip", new List<KeyValuePair<float, Pose>> { Kp(0f, sA, sR), Kp(23f, sA, sR), Kp(Duration, sB, sR) }, ""));

		// UI and sound.
		AnimationClip fadeClip = new AnimationClip { name = "Fade" };
		fadeClip.SetCurve("", typeof(CanvasGroup), "m_Alpha", new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1.5f, 0f), new Keyframe(Duration, 0f)));
		AssetDatabase.AddObjectToAsset(fadeClip, timeline);
		Animate(timeline, director, "Fade", fade.GetComponent<Animator>(), fadeClip);
		Activation(timeline, director, "Caption charging", charging, 8.5f, 3.5f);
		Activation(timeline, director, "Caption fuel", fuel, 12f, 2f);
		Activation(timeline, director, "Caption escaped", escaped, 23.5f, Duration - 23.5f);
		Sound(timeline, director, wind, 0f, 8f);
		Sound(timeline, director, charge_, 8f, 5f);
		Sound(timeline, director, full, 12f, 1.5f);
		Sound(timeline, director, ignition, 17f, 2f);
		Sound(timeline, director, engine, 13.5f, 9.5f);

		EditorUtility.SetDirty(timeline);
		AssetDatabase.SaveAssets();
		EditorSceneManager.SaveScene(scene, ScenePath);
		if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
			EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
		Debug.Log("EndingSceneBuilder: done, " + timeline.GetOutputTracks().Count() + " tracks, " + timeline.duration + " s");
	}

	// ---------- set helpers ----------

	static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

	static Transform Child(GameObject root, string name) {
		return root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
	}

	static void Disc(string name, Vector3 position, float diameter, Material material) {
		GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		disc.name = name;
		disc.transform.position = position + Vector3.down * 0.05f;
		disc.transform.localScale = new Vector3(diameter, 0.05f, diameter);
		disc.GetComponent<Renderer>().sharedMaterial = material;
	}

	static void Prop(string packAndModel, Vector3 position, float size, float yaw) {
		string[] parts = packAndModel.Split('/');
		GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/" + parts[0] + "/Models/" + parts[1] + ".fbx");
		GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
		instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
		Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
		Bounds b = renderers[0].bounds;
		foreach (Renderer r in renderers)
			b.Encapsulate(r.bounds);
		instance.transform.localScale *= size / Mathf.Max(b.size.x, b.size.z);
		instance.transform.position = position;
	}

	// The robot ball's look (mesh + materials of the Player prefab) without any gameplay component.
	static GameObject Robo(Vector3 position) {
		GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
		GameObject robo = new GameObject("Robo");
		robo.AddComponent<MeshFilter>().sharedMesh = player.GetComponent<MeshFilter>().sharedMesh;
		robo.AddComponent<MeshRenderer>().sharedMaterials = player.GetComponent<MeshRenderer>().sharedMaterials;
		robo.transform.localScale = player.transform.localScale;
		robo.transform.position = position;
		robo.AddComponent<RollVisual>();
		robo.AddComponent<Animator>();
		return robo;
	}

	static void Stars(Vector3 origin) {
		var random = new System.Random(5);
		var vertices = new List<Vector3>();
		var triangles = new List<int>();
		for (int i = 0; i < 500; i++) {
			Vector3 dir = new Vector3((float)random.NextDouble() - 0.5f, (float)random.NextDouble() * 0.8f - 0.1f, (float)random.NextDouble() * 0.8f + 0.2f).normalized;
			Vector3 c = dir * 1800f;
			Vector3 a = Vector3.Cross(dir, Vector3.up).normalized * 2.2f, b = Vector3.Cross(dir, a).normalized * 2.2f;
			int n = vertices.Count;
			vertices.AddRange(new[] { c - a - b, c - a + b, c + a + b, c + a - b });
			triangles.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
		}
		Mesh mesh = new Mesh { name = "Starfield" };
		mesh.SetVertices(vertices);
		mesh.SetTriangles(triangles, 0);
		AssetDatabase.DeleteAsset(Folder + "/Starfield.asset");
		AssetDatabase.CreateAsset(mesh, Folder + "/Starfield.asset");
		GameObject stars = new GameObject("Stars", typeof(MeshFilter), typeof(MeshRenderer));
		stars.transform.position = origin;
		stars.GetComponent<MeshFilter>().sharedMesh = mesh;
		Material white = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Stars.mat");
		if (white == null) {
			white = new Material(Shader.Find("Unlit/Color")) { color = Color.white };
			AssetDatabase.CreateAsset(white, Folder + "/Stars.mat");
		}
		stars.GetComponent<MeshRenderer>().sharedMaterial = white;
	}

	static AudioSource Audio(string name, string clip) {
		AudioSource source = new GameObject(name).AddComponent<AudioSource>();
		source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ThirdParty/KenneyAudio/" + clip + ".ogg");
		source.playOnAwake = false;
		source.spatialBlend = 0f;
		source.volume = 0.6f;
		return source;
	}

	// Rotation that swings the closed door (hinge at its origin) down behind the ship until its edge meets the ground.
	static Quaternion OpenDoor(Transform door, Vector3 rear, Vector3 side, float drop) {
		Vector3 axis = Vector3.Cross(Vector3.up, rear).normalized;
		Vector3 centre = door.GetComponent<Renderer>().bounds.center - door.position;
		float angle = 90f + drop;
		Quaternion best = Quaternion.identity;
		float bestDot = float.MinValue;
		foreach (float sign in new[] { 1f, -1f }) {
			Quaternion swing = Quaternion.AngleAxis(sign * angle, axis);
			float dot = Vector3.Dot(swing * centre, rear);
			if (dot > bestDot) {
				bestDot = dot;
				best = swing;
			}
		}
		Quaternion world = best * door.rotation;
		return Quaternion.Inverse(door.parent.rotation) * world;
	}

	// ---------- timeline helpers ----------

	static KeyValuePair<float, Vector3> K(float t, Vector3 p) { return new KeyValuePair<float, Vector3>(t, p); }
	static KeyValuePair<float, Pose> Kp(float t, Vector3 p, Quaternion r) { return new KeyValuePair<float, Pose>(t, new Pose(p, r)); }
	static KeyValuePair<float, Pose> Look(float t, Vector3 from, Vector3 at) { return Kp(t, from, Quaternion.LookRotation(at - from)); }

	static Pose Sample(List<KeyValuePair<float, Pose>> keys, float t) {
		for (int i = 1; i < keys.Count; i++)
			if (t <= keys[i].Key) {
				float u = Mathf.InverseLerp(keys[i - 1].Key, keys[i].Key, t);
				return new Pose(Vector3.Lerp(keys[i - 1].Value.position, keys[i].Value.position, u), Quaternion.Slerp(keys[i - 1].Value.rotation, keys[i].Value.rotation, u));
			}
		return keys[keys.Count - 1].Value;
	}

	static AnimationCurve Curve(IEnumerable<KeyValuePair<float, float>> keys) {
		var curve = new AnimationCurve(keys.Select(k => new Keyframe(k.Key, k.Value)).ToArray());
		for (int i = 0; i < curve.length; i++)
			AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
		for (int i = 0; i < curve.length; i++)
			AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
		return curve;
	}

	static void PositionCurve(AnimationClip clip, string path, IList<KeyValuePair<float, Vector3>> keys) {
		string[] axes = { "x", "y", "z" };
		for (int a = 0; a < 3; a++) {
			int axis = a;
			AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition." + axes[a]),
				Curve(keys.Select(k => new KeyValuePair<float, float>(k.Key, k.Value[axis]))));
		}
	}

	static void RotationCurve(AnimationClip clip, string path, IList<KeyValuePair<float, Quaternion>> keys) {
		// Keep consecutive quaternions in the same hemisphere so the interpolation takes the short way.
		var fixedKeys = new List<KeyValuePair<float, Quaternion>>();
		foreach (var k in keys) {
			Quaternion q = k.Value;
			if (fixedKeys.Count > 0 && Quaternion.Dot(fixedKeys[fixedKeys.Count - 1].Value, q) < 0f)
				q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
			fixedKeys.Add(new KeyValuePair<float, Quaternion>(k.Key, q));
		}
		string[] axes = { "x", "y", "z", "w" };
		for (int a = 0; a < 4; a++) {
			int axis = a;
			AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + axes[a]),
				Curve(fixedKeys.Select(k => new KeyValuePair<float, float>(k.Key, k.Value[axis]))));
		}
	}

	static AnimationClip PoseClip(string name, List<KeyValuePair<float, Pose>> keys, string path) {
		var clip = new AnimationClip { name = name };
		PositionCurve(clip, path, keys.Select(k => new KeyValuePair<float, Vector3>(k.Key, k.Value.position)).ToList());
		RotationCurve(clip, path, keys.Select(k => new KeyValuePair<float, Quaternion>(k.Key, k.Value.rotation)).ToList());
		return clip;
	}

	static void Robo(TimelineAsset timeline, PlayableDirector director, GameObject robo, KeyValuePair<float, Vector3>[] keys) {
		var clip = new AnimationClip { name = "Robo" };
		PositionCurve(clip, "", keys);
		Animate(timeline, director, "Robo", robo.GetComponent<Animator>(), clip);
	}

	static void Animate(TimelineAsset timeline, PlayableDirector director, string name, Animator target, AnimationClip clip) {
		if (!AssetDatabase.Contains(clip))
			AssetDatabase.AddObjectToAsset(clip, timeline);
		AnimationTrack track = timeline.CreateTrack<AnimationTrack>(null, name);
		TimelineClip timelineClip = track.CreateDefaultClip();
		AnimationPlayableAsset asset = (AnimationPlayableAsset)timelineClip.asset;
		asset.clip = clip;
		asset.removeStartOffset = false;   // keys are world positions, not deltas from the object's start
		track.trackOffset = TrackOffset.ApplyTransformOffsets;   // zero offset: absolute
		timelineClip.start = 0;
		timelineClip.duration = Duration;
		director.SetGenericBinding(track, target);
	}

	static void Activation(TimelineAsset timeline, PlayableDirector director, string name, GameObject target, float start, float duration) {
		ActivationTrack track = timeline.CreateTrack<ActivationTrack>(null, name);
		TimelineClip clip = track.CreateDefaultClip();
		clip.start = start;
		clip.duration = duration;
		track.postPlaybackState = ActivationTrack.PostPlaybackState.LeaveAsIs;
		director.SetGenericBinding(track, target);
	}

	static void Sound(TimelineAsset timeline, PlayableDirector director, AudioSource source, float start, float duration) {
		AudioTrack track = timeline.CreateTrack<AudioTrack>(null, source.name);
		TimelineClip clip = track.CreateDefaultClip();
		((AudioPlayableAsset)clip.asset).clip = source.clip;
		clip.start = start;
		clip.duration = duration;
		director.SetGenericBinding(track, source);
	}

	// ---------- UI helpers ----------

	static GameObject Stretch(GameObject go, Transform parent) {
		go.transform.SetParent(parent, false);
		RectTransform rect = (RectTransform)go.transform;
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = rect.offsetMax = Vector2.zero;
		return go;
	}

	static Text Label(Transform parent, string name, string value, Font font, int size, float y, Vector2 box) {
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
		go.transform.SetParent(parent, false);
		RectTransform rect = (RectTransform)go.transform;
		rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = box;
		rect.anchoredPosition = new Vector2(0f, y);
		Text text = go.GetComponent<Text>();
		text.text = value;
		text.font = font;
		text.fontSize = size;
		text.alignment = TextAnchor.MiddleCenter;
		text.color = Color.white;
		return text;
	}

	static Button Button(Transform parent, string name, string label, Font font, float y) {
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
		go.transform.SetParent(parent, false);
		RectTransform rect = (RectTransform)go.transform;
		rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = new Vector2(460f, 110f);
		rect.anchoredPosition = new Vector2(0f, y);
		Text text = Label(go.transform, name.Replace("Button", "Text").Trim(), label, font, 48, 0f, new Vector2(420f, 90f));
		RectTransform textRect = text.rectTransform;
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.offsetMin = textRect.offsetMax = Vector2.zero;
		return go.GetComponent<Button>();
	}
}
