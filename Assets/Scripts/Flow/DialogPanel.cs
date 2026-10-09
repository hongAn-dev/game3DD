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
	public Button backButton;
	public Button skipButton;
	public Button nextButton;
	public Button startButton;

	StoryPage[] pages = new StoryPage[0];
	int page;
	int skipTo = -1;
	System.Action finished;

	public bool IsOpen { get { return gameObject.activeSelf; } }
	public int Page { get { return page; } }

	/// <param name="skipTo">Page "Bỏ qua hướng dẫn" jumps to, or -1 for no skip button.</param>
	public void Show(StoryPage[] pages, int skipTo, string finishLabel, System.Action onFinish) {
		this.pages = pages;
		this.skipTo = skipTo;
		finished = onFinish;
		page = 0;
		startButton.GetComponentInChildren<Text>(true).text = finishLabel;
		gameObject.SetActive(true);
		Render();
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
		if (skipTo >= 0 && page < skipTo) {
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

	void Render() {
		title.text = pages[page].title;
		body.text = pages[page].body;
		bool last = page == pages.Length - 1;
		backButton.gameObject.SetActive(page > 0);
		skipButton.gameObject.SetActive(skipTo >= 0 && page < skipTo);
		nextButton.gameObject.SetActive(!last);
		startButton.gameObject.SetActive(last);
		// Keyboard/gamepad players land on the forward button.
		if (EventSystem.current != null)
			EventSystem.current.SetSelectedGameObject((last ? startButton : nextButton).gameObject);
	}
}
