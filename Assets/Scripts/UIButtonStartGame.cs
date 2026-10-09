using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

/// <summary>
/// A class that deals with Load Level events.
/// </summary>
public class UIButtonStartGame : MonoBehaviour {

	/// <summary>
	/// Load a level (scene) by name..
	/// </summary>
	public void loadLevelEasy() {
		SceneRouter.StartCampaign(GameSettings.gameDifficulties.Easy, true);
	}

	/// <summary>
	/// Load a level (scene) by name..
	/// </summary>
	public void loadLevelNormal() {
		SceneRouter.StartCampaign(GameSettings.gameDifficulties.Normal, true);
	}

	/// <summary>
	/// Load a level (scene) by name..
	/// </summary>
	public void loadLevelHard() {
		SceneRouter.StartCampaign(GameSettings.gameDifficulties.Hard, true);
	}

}