using UnityEngine;
using System.Collections;

/// <summary>
/// Resume Game handler.
/// </summary>
public class UIButtonResumeGame : MonoBehaviour {

	/// <summary>
	/// Resume the game after paused.
	/// </summary>
	public void resumeGame()
	{
		// Through GameFlow so state and timeScale stay in sync; intro dialogs start play only from their own button.
		GameFlow.Resume();
	}
}