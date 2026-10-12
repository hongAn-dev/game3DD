using UnityEngine;

/// <summary>
/// Android and iOS run at 30 fps unless asked otherwise, which made the camera swipe look choppy on phones.
/// Ask for 60 (the display caps it; vSync off so targetFrameRate applies).
/// </summary>
public static class FrameRate {

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	static void Apply() {
		if (!Application.isMobilePlatform)
			return;
		QualitySettings.vSyncCount = 0;
		Application.targetFrameRate = 60;
	}
}
