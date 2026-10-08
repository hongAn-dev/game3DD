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
		// Closes the game.
		// Through GameFlow so state and timeScale stay in sync (tapping the intro card starts play).
		if (GameFlow.State == FlowState.Intro)
			GameFlow.Enter(FlowState.Playing);
		else
			GameFlow.Resume();
	}
}