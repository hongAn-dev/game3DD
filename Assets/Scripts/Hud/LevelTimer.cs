using UnityEngine;
using UnityEngine.UI;

/// <summary>Per-level completion timer. Intro and pause time do not consume the budget.</summary>
public sealed class LevelTimer : MonoBehaviour {
	public static LevelTimer Current { get; private set; }

	LevelConfig config;
	float remaining;
	float elapsed;
	Text text;
	GameObject panel;
	GameObject canvasRoot;
	bool expired;

	public float Remaining { get { return remaining; } }

	public void Configure(LevelConfig level) {
		config = level;
		if (config == null)
			return;
		remaining = config.timeLimitSeconds > 0f ? config.timeLimitSeconds : FallbackSeconds(config.order);
		elapsed = 0f;
		BuildUi();
		OnFlowChanged(GameFlow.State);
		Render();
	}

	void Awake() {
		Current = this;
		GameFlow.Changed += OnFlowChanged;
	}

	void OnDestroy() {
		GameFlow.Changed -= OnFlowChanged;
		if (Current == this)
			Current = null;
	}

	void OnFlowChanged(FlowState state) {
		if (panel == null)
			return;
		panel.SetActive(state == FlowState.Playing || state == FlowState.Paused);
	}

	void Update() {
		if (config == null || expired || !GameFlow.IsGameplayActive)
			return;
		float delta = Time.deltaTime;
		elapsed += delta;
		remaining = Mathf.Max(0f, remaining - delta);
		Render();
		if (remaining <= 0f) {
			expired = true;
			GameFlow.ReportDeathCause("Hết thời gian");
			GameFlow.Die("Hết thời gian");
		}
	}

	void BuildUi() {
		if (panel != null)
			return;
		canvasRoot = new GameObject("Level Timer Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
		canvasRoot.transform.SetParent(transform, false);
		Canvas canvas = canvasRoot.GetComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 20;
		CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = 0.5f;

		panel = new GameObject("Level Timer", typeof(RectTransform), typeof(Image));
		panel.transform.SetParent(canvasRoot.transform, false);
		RectTransform panelRect = panel.GetComponent<RectTransform>();
		panelRect.anchorMin = new Vector2(0f, 1f);
		panelRect.anchorMax = new Vector2(0f, 1f);
		panelRect.pivot = new Vector2(0f, 1f);
		panelRect.anchoredPosition = new Vector2(28f, -28f);
		// Keep the countdown compact while making the digits as prominent as the energy/map labels.
		panelRect.sizeDelta = new Vector2(220f, 64f);
		panel.GetComponent<Image>().color = new Color(0.02f, 0.05f, 0.08f, 0.82f);

		GameObject label = new GameObject("Countdown", typeof(RectTransform), typeof(Text));
		label.transform.SetParent(panel.transform, false);
		RectTransform labelRect = label.GetComponent<RectTransform>();
		labelRect.anchorMin = Vector2.zero;
		labelRect.anchorMax = Vector2.one;
		labelRect.offsetMin = new Vector2(8f, 0f);
		labelRect.offsetMax = new Vector2(-8f, 0f);
		text = label.GetComponent<Text>();
		text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
		text.fontSize = 40;
		text.fontStyle = FontStyle.Bold;
		text.alignment = TextAnchor.MiddleCenter;
		text.color = Color.white;
		text.raycastTarget = false;
	}

	void Render() {
		if (text == null)
			return;
		int whole = Mathf.CeilToInt(remaining);
		int minutes = whole / 60;
		int seconds = whole % 60;
		text.text = minutes.ToString("00") + ":" + seconds.ToString("00");
		text.color = remaining <= 10f ? new Color(1f, 0.38f, 0.3f) : remaining <= 30f ? new Color(1f, 0.82f, 0.35f) : Color.white;
	}

	static float FallbackSeconds(int order) {
		switch (order) {
		case 1: return 30f;
		case 2: return 90f;
		case 3: return 180f;
		default: return 300f;
		}
	}
}
