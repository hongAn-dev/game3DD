using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The story dialog (spec §7.3/§7.4): shows pages one at a time with Quay lại / Bỏ qua hướng dẫn / Tiếp theo, and
/// the finish button (Bắt đầu or Đóng) on the last page. Never closes by itself; the owner decides what finishing
/// means (start play, back to the menu). Built by FlowUiSetup; this object is the full-screen backdrop.
/// </summary>
public class DialogPanel : MonoBehaviour {

	public RectTransform box;
	public Text title;
	public Text body;
	/// <summary>Optional visual for guide pages. Older scenes leave this unset; it is created at runtime.</summary>
	public Image illustration;
	public Button backButton;
	public Button skipButton;
	public Button nextButton;
	public Button startButton;

	/// <summary>Seconds after a page change during which the forward buttons ignore taps (a double tap on "Tiếp
	/// theo" must not land on "Bắt đầu", which sits in the same place, and skip the last page).</summary>
	public const float TapGuard = 0.35f;

	StoryPage[] pages = new StoryPage[0];
	int page;
	int skipTo = -1;
	bool skipCloses;
	float shownAt;
	System.Action finished;

	public bool IsOpen { get { return gameObject.activeSelf; } }
	public int Page { get { return page; } }

	/// <param name="skipTo">Page "Bỏ qua hướng dẫn" jumps to, or -1 for no skip button.</param>
	public void Show(StoryPage[] pages, int skipTo, string finishLabel, System.Action onFinish) {
		Open(pages, skipTo, false, "Bỏ qua hướng dẫn", finishLabel, onFinish);
	}

	/// <summary>Review mode (main menu guide): the left-middle button closes the dialog from any page.</summary>
	public void ShowClosable(StoryPage[] pages, string closeLabel, System.Action onFinish) {
		Open(pages, -1, true, closeLabel, closeLabel, onFinish);
	}

	void Open(StoryPage[] pages, int skipTo, bool skipCloses, string skipLabel, string finishLabel, System.Action onFinish) {
		this.pages = pages;
		this.skipTo = skipTo;
		this.skipCloses = skipCloses;
		skipButton.GetComponentInChildren<Text>(true).text = skipLabel;
		finished = onFinish;
		page = 0;
		startButton.GetComponentInChildren<Text>(true).text = finishLabel;
		gameObject.SetActive(true);
		Render();
	}

	void EnsureIllustration() {
		if (illustration != null || box == null)
			return;
		GameObject go = new GameObject("Dialog Illustration", typeof(RectTransform), typeof(Image));
		go.transform.SetParent(box, false);
		illustration = go.GetComponent<Image>();
		RectTransform rect = (RectTransform)go.transform;
		rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
		rect.pivot = new Vector2(1f, 0.5f);
		rect.anchoredPosition = new Vector2(-48f, 0f);
		rect.sizeDelta = new Vector2(260f, 260f);
		illustration.preserveAspect = true;
		illustration.raycastTarget = false;
	}

	public void Next() {
		if (page < pages.Length - 1) {
			page++;
			Render();
		}
	}

	public void Back() {
		if (page > 0) {
			page--;
			Render();
		}
	}

	public void Skip() {
		if (skipCloses) {
			Finish();
		} else if (skipTo >= 0 && page < skipTo) {
			page = skipTo;
			Render();
		}
	}

	public void Finish() {
		if (!IsOpen)
			return;
		gameObject.SetActive(false);
		System.Action done = finished;
		finished = null;
		if (done != null)
			done();
	}

	// Button entry points: the same as Next/Finish, but a tap right after a page change is ignored.
	public void PressNext() {
		if (Time.unscaledTime - shownAt >= TapGuard)
			Next();
	}

	public void PressFinish() {
		if (Time.unscaledTime - shownAt >= TapGuard)
			Finish();
	}

	void Render() {
		EnsureIllustration();
		shownAt = Time.unscaledTime;
		title.text = pages[page].title;
		body.text = pages[page].body;
		if (illustration != null) {
			Sprite sprite = string.IsNullOrEmpty(pages[page].illustrationKey)
				? null : Resources.Load<Sprite>("GuideSprites/" + pages[page].illustrationKey);
			illustration.sprite = sprite;
			illustration.gameObject.SetActive(sprite != null);
			// Reserve the right side for the visual so explanatory text never runs underneath it.
			body.rectTransform.sizeDelta = sprite != null ? new Vector2(980f, body.rectTransform.sizeDelta.y) : new Vector2(1300f, body.rectTransform.sizeDelta.y);
			body.rectTransform.anchoredPosition = sprite != null ? new Vector2(-150f, body.rectTransform.anchoredPosition.y) : new Vector2(0f, body.rectTransform.anchoredPosition.y);
			FitBodyText();
		}
		bool last = page == pages.Length - 1;
		backButton.gameObject.SetActive(page > 0);
		skipButton.gameObject.SetActive(skipCloses ? !last : skipTo >= 0 && page < skipTo);
		nextButton.gameObject.SetActive(!last);
		startButton.gameObject.SetActive(last);
		// Keyboard/gamepad players land on the forward button.
		if (EventSystem.current != null)
			EventSystem.current.SetSelectedGameObject((last ? startButton : nextButton).gameObject);
	}

	void FitBodyText() {
		if (body == null)
			return;
		int original = body.fontSize;
		TextGenerationSettings settings = body.GetGenerationSettings(body.rectTransform.sizeDelta);
		for (int size = original; size >= 28; size--) {
			body.fontSize = size;
			settings.fontSize = size;
			float height = new TextGenerator().GetPreferredHeight(body.text, settings) / body.pixelsPerUnit;
			if (height <= body.rectTransform.sizeDelta.y)
				return;
		}
		body.fontSize = 28;
	}
}
