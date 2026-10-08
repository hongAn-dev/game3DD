using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

/// <summary>
/// A class that deals with Load Level events.
/// </summary>
public class UIButtonLevelLoad : MonoBehaviour {

	/// <summary>
	/// Load a level (scene) by name..
	/// </summary>
	public void loadLevel(string levelName) {
		if (levelName == SceneRouter.MainMenuScene)
			SceneRouter.ToMenu();
		else
			SceneRouter.Load(levelName);
	}

	/// <summary>
	/// Load the next level based on the build index of the active scene.
	/// </summary>
	public void loadNextLevel() {
		SceneRouter.Next();
	}

	/// <summary>
	/// Reload the current active level.
	/// </summary>
	public void reloadLevel() {
		SceneRouter.Retry();
	}
}