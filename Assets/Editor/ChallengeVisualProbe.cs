using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Renders the real serialized UI and cameras into review screenshots. This is a render/layout probe,
/// not a substitute for device safe-area, color-vision or runtime audio checks.
/// Invoke from the editor with ChallengeVisualProbe.Capture().
/// </summary>
public static class ChallengeVisualProbe {
	const string Output = "docs/validation/robo-challenge";
	static readonly Vector2Int[] Sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) };
	static readonly string[] ScenePaths = {
		"Assets/Scenes/MainMenu.unity",
		"Assets/Scenes/Level2.unity",
		"Assets/Scenes/Ending.unity"
	};

	[MenuItem("Tools/Robo Lac Loi/Capture Challenge Visuals")]
	public static void Capture() {
		Directory.CreateDirectory(Output);
		StringBuilder report = new StringBuilder();
		report.AppendLine("Robo Challenge visual probe");
		report.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
		report.AppendLine("Renders serialized scenes through their active camera with Canvas overlays temporarily routed to that camera.");
		report.AppendLine("Overlap rectangles are screen-space AABB checks over visible Graphics; ancestor/descendant panel pairs are omitted and Text bounds use preferred glyph dimensions. Contrast ratios use text RGB against a role-appropriate solid panel/background estimate, not sampled rendered pixels.");
		report.AppendLine("Device safe areas, touch target dp, runtime camera movement, and human readability still need manual validation.");

		foreach (string scenePath in ScenePaths) {
			Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
			if (scenePath.EndsWith("Ending.unity", StringComparison.OrdinalIgnoreCase)) {
				CaptureEnding(scene, report);
			} else {
				CaptureScene(scene, Path.GetFileNameWithoutExtension(scenePath), report);
			}
		}
		File.WriteAllText(Path.Combine(Output, "visual-probe.txt"), report.ToString(), Encoding.UTF8);
		AssetDatabase.Refresh();
		Debug.Log("ChallengeVisualProbe: screenshots and layout report written to " + Output);
	}

	static void CaptureScene(Scene scene, string prefix, StringBuilder report) {
		SetCanvasesForCapture(scene, out List<CanvasState> states);
		try {
			Canvas.ForceUpdateCanvases();
			CaptureAllSizes(scene, prefix, report);
		} finally {
			RestoreCanvases(states);
		}
	}

	static void CaptureEnding(Scene scene, StringBuilder report) {
		SetCanvasesForCapture(scene, out List<CanvasState> states);
		try {
			Canvas.ForceUpdateCanvases();
			EndingController ending = UnityEngine.Object.FindObjectOfType<EndingController>();
			if (ending == null || ending.director == null) {
				report.AppendLine("Ending: missing EndingController or PlayableDirector; screenshots skipped.");
				return;
			}
			ending.completionPanel.SetActive(false);
			if (ending.skipButton != null)
				ending.skipButton.SetActive(true);
			ending.director.time = Mathf.Min(25f, (float)ending.director.duration - 0.01f);
			ending.director.Evaluate();
			Canvas.ForceUpdateCanvases();
			CaptureAllSizes(scene, "Ending_t25", report);

			ending.Complete();
			Canvas.ForceUpdateCanvases();
			CaptureAllSizes(scene, "Ending_complete", report);
		} finally {
			RestoreCanvases(states);
		}
	}

	struct CanvasState {
		public Canvas canvas;
		public RenderMode mode;
		public Camera camera;
		public float planeDistance;
	}

	static void SetCanvasesForCapture(Scene scene, out List<CanvasState> states) {
		states = new List<CanvasState>();
		Camera camera = FindCamera(scene);
		if (camera == null)
			return;
		foreach (Canvas canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true)) {
			if (canvas.gameObject.scene != scene)
				continue;
			states.Add(new CanvasState { canvas = canvas, mode = canvas.renderMode, camera = canvas.worldCamera, planeDistance = canvas.planeDistance });
			if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) {
				canvas.renderMode = RenderMode.ScreenSpaceCamera;
				canvas.worldCamera = camera;
				canvas.planeDistance = Mathf.Max(camera.nearClipPlane + 0.1f, 1f);
			}
		}
	}

	static void RestoreCanvases(List<CanvasState> states) {
		foreach (CanvasState state in states) {
			if (state.canvas == null)
				continue;
			state.canvas.renderMode = state.mode;
			state.canvas.worldCamera = state.camera;
			state.canvas.planeDistance = state.planeDistance;
		}
	}

	static Camera FindCamera(Scene scene) {
		Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>(true).Where(c => c.gameObject.scene == scene && c.enabled && c.gameObject.activeInHierarchy).ToArray();
		return cameras.FirstOrDefault(c => c.CompareTag("MainCamera")) ?? cameras.FirstOrDefault(c => c.name == "Camera") ?? cameras.FirstOrDefault();
	}

	static void CaptureAllSizes(Scene scene, string prefix, StringBuilder report) {
		foreach (Vector2Int size in Sizes) {
			Camera camera = FindCamera(scene);
			if (camera == null) {
				report.AppendLine(prefix + " " + size.x + "x" + size.y + ": no enabled camera.");
				continue;
			}
			RenderTexture target = RenderTexture.GetTemporary(size.x, size.y, 24, RenderTextureFormat.ARGB32);
			RenderTexture previous = camera.targetTexture;
			RenderTexture oldActive = RenderTexture.active;
			try {
				camera.targetTexture = target;
				foreach (Canvas canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true))
					if (canvas.gameObject.scene == scene && canvas.renderMode == RenderMode.ScreenSpaceCamera) canvas.worldCamera = camera;
				Canvas.ForceUpdateCanvases();
				camera.Render();
				RenderTexture.active = target;
				Texture2D image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
				image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
				image.Apply();
				string filename = prefix + "_" + size.x + "x" + size.y + ".png";
				File.WriteAllBytes(Path.Combine(Output, filename), image.EncodeToPNG());
				UnityEngine.Object.DestroyImmediate(image);
				report.AppendLine("\n" + filename);
				AppendUiReport(scene, camera, size, report);
			} finally {
				camera.targetTexture = previous;
				RenderTexture.active = oldActive;
				RenderTexture.ReleaseTemporary(target);
			}
		}
	}

	static void AppendUiReport(Scene scene, Camera camera, Vector2Int size, StringBuilder report) {
		List<RectInfo> rects = new List<RectInfo>();
		foreach (RectTransform rect in UnityEngine.Object.FindObjectsOfType<RectTransform>(true)) {
			if (rect.gameObject.scene != scene || !rect.gameObject.activeInHierarchy || !Relevant(rect))
				continue;
			Graphic graphic = rect.GetComponent<Graphic>();
			if (graphic == null || !graphic.enabled || graphic.color.a <= 0.01f || EffectiveCanvasAlpha(rect) <= 0.01f)
				continue;
			Vector3[] corners = new Vector3[4];
			rect.GetWorldCorners(corners);
			Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
			for (int i = 0; i < corners.Length; i++) {
				Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
				min = Vector2.Min(min, point);
				max = Vector2.Max(max, point);
			}
			Text text = rect.GetComponent<Text>();
			Rect screenRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
			if (text != null)
				screenRect = GlyphRect(text, camera, screenRect);
			rects.Add(new RectInfo { name = FullName(rect), transform = rect, rect = screenRect, text = text });
		}
		foreach (RectInfo item in rects) {
			Color background = EstimateBackground(item.name);
			if (item.text != null)
				report.AppendLine("  contrast-estimate " + item.name + ": " + Contrast(item.text.color, background).ToString("0.00") + ":1 (foreground against " + ColorHex(background) + ")");
		}
		for (int i = 0; i < rects.Count; i++) {
			for (int j = i + 1; j < rects.Count; j++) {
				Transform a = rects[i].transform;
				Transform b = rects[j].transform;
				if (a != null && b != null && (IsAncestor(a, b) || IsAncestor(b, a)))
					continue;
				if (rects[i].rect.Overlaps(rects[j].rect))
					report.AppendLine("  AABB-overlap " + rects[i].name + " / " + rects[j].name);
			}
		}
	}

	struct RectInfo {
		public string name;
		public Transform transform;
		public Rect rect;
		public Text text;
	}

	static bool Relevant(RectTransform rect) {
		string n = rect.name.ToLowerInvariant();
		return rect.GetComponent<Text>() != null || n.Contains("caption") || n.Contains("skip") || n.Contains("title")
			|| n.Contains("sound") || n.Contains("music") || n.Contains("pause") || n.Contains("zone") || n.Contains("score")
			|| n.Contains("completion") || n.Contains("panel");
	}

	static float EffectiveCanvasAlpha(Transform transform) {
		float alpha = 1f;
		for (Transform t = transform; t != null; t = t.parent) {
			CanvasGroup group = t.GetComponent<CanvasGroup>();
			if (group != null)
				alpha *= group.alpha;
		}
		return alpha;
	}

	static Rect GlyphRect(Text text, Camera camera, Rect bounds) {
		RectTransform rect = text.rectTransform;
		if (text.font == null || string.IsNullOrEmpty(text.text) || rect.rect.width <= 0f || rect.rect.height <= 0f)
			return bounds;
		TextGenerator generator = new TextGenerator();
		TextGenerationSettings settings = text.GetGenerationSettings(rect.rect.size);
		float width = Mathf.Min(rect.rect.width, generator.GetPreferredWidth(text.text, settings));
		settings.generationExtents = new Vector2(Mathf.Max(1f, width), rect.rect.height);
		float height = Mathf.Min(rect.rect.height, generator.GetPreferredHeight(text.text, settings));
		if (width <= 0f || height <= 0f)
			return bounds;
		Vector2 center = (bounds.min + bounds.max) * 0.5f;
		float pxPerUnitX = bounds.width / rect.rect.width;
		float pxPerUnitY = bounds.height / rect.rect.height;
		float glyphWidth = width * pxPerUnitX;
		float glyphHeight = height * pxPerUnitY;
		float xMin = center.x - glyphWidth * 0.5f;
		if (text.alignment == TextAnchor.MiddleLeft || text.alignment == TextAnchor.UpperLeft || text.alignment == TextAnchor.LowerLeft)
			xMin = bounds.xMin;
		else if (text.alignment == TextAnchor.MiddleRight || text.alignment == TextAnchor.UpperRight || text.alignment == TextAnchor.LowerRight)
			xMin = bounds.xMax - glyphWidth;
		float yMin = center.y - glyphHeight * 0.5f;
		return Rect.MinMaxRect(xMin, yMin, xMin + glyphWidth, yMin + glyphHeight);
	}

	static bool IsAncestor(Transform ancestor, Transform child) {
		for (Transform t = child.parent; t != null; t = t.parent)
			if (t == ancestor)
				return true;
		return false;
	}

	static string FullName(Transform transform) {
		string result = transform.name;
		while (transform.parent != null) {
			transform = transform.parent;
			result = transform.name + "/" + result;
		}
		return result;
	}

	static Color EstimateBackground(string name) {
		string lower = name.ToLowerInvariant();
		if (lower.Contains("caption") || lower.Contains("completion") || lower.Contains("title backdrop"))
			return new Color32(16, 24, 32, 255);
		if (lower.Contains("zone") || lower.Contains("score") || lower.Contains("energy"))
			return new Color32(14, 19, 26, 230);
		return new Color32(14, 19, 26, 230);
	}

	static float Contrast(Color foreground, Color background) {
		float a = Luminance(foreground), b = Luminance(background);
		return (Mathf.Max(a, b) + 0.05f) / (Mathf.Min(a, b) + 0.05f);
	}

	static float Luminance(Color c) {
		return 0.2126f * Linear(c.r) + 0.7152f * Linear(c.g) + 0.0722f * Linear(c.b);
	}

	static float Linear(float c) {
		return c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
	}

	static string ColorHex(Color c) {
		return "#" + Mathf.RoundToInt(c.r * 255).ToString("X2") + Mathf.RoundToInt(c.g * 255).ToString("X2") + Mathf.RoundToInt(c.b * 255).ToString("X2");
	}
}
