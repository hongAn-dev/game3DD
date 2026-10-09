using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Main Game Manager.
/// </summary>
public class GameManager : MonoBehaviour {

	public static GameManager gm;

	[Tooltip("If not set, the player will default to the gameObject tagged as Player.")]
	public GameObject player;

	public enum gameStates {Playing, Death, GameOver, BeatLevel};
	public gameStates gameState = gameStates.Playing;

	public int score = 0;

	public bool canBeatLevel = false;

	[Tooltip("Mark this is current scene/level is the final one.")]
	public bool isFinalLevel = false;

	[Tooltip("The score necessary to beat the level in each difficulty.")]
	public int beatEasyLevelScore = 0;
	public int beatNormalLevelScore = 0;
	public int beatHardLevelScore = 0;

	public GameObject mainCanvas;
	public Text mainScoreDisplay;
	public GameObject gameOverCanvas;
	public Text gameOverScoreDisplay;

	[Tooltip("Only need to set if canBeatLevel is set to true.")]
	public GameObject beatLevelCanvas;

	public AudioSource backgroundMusic;

	/// <summary>Seconds (real time) the music takes to fade out after a death or a win.</summary>
	public const float MusicFade = 1f;
	float fadeFrom;
	float fadeStart;
	SoundGroup musicGroup;

	[Tooltip("Only need to set if canBeatLevel is set to true.")]
	public GameObject introBeatLevelCanvas;

	[Tooltip("Only need to set if canBeatLevel is set to true.")]
	public Text introBeatLevelText;

	private float introBeatLevelTextDuration = 2.0f;
	private float introSavedTime;

	private Health playerHealth;

	private LevelConfig config;

	// The beat score level
	private int beatLevelScore = 0;

	public int BeatLevelScore { get { return beatLevelScore; } }

	/// <summary>
	/// Use this for initialization.
	/// </summary>
	void Start () {
		gm = this;

		if (player == null) {
			player = GameObject.FindWithTag("Player");
		}

		playerHealth = player.GetComponent<Health>();

		// Make other UI inactive.
		gameOverCanvas.SetActive (false);
		if (canBeatLevel) {
			// Set beat level score based on game difficulty.
			switch (GameSettings.difficulty) {
				case GameSettings.gameDifficulties.Hard:
					GameObject.FindGameObjectWithTag ("EasyModeCanvas").SetActive (false);
					GameObject.FindGameObjectWithTag ("NormalModeCanvas").SetActive (false);
					GameObject.FindGameObjectWithTag ("HardModeCanvas").SetActive (true);
					beatLevelScore = beatHardLevelScore;
					break;
				case GameSettings.gameDifficulties.Normal:
					GameObject.FindGameObjectWithTag ("EasyModeCanvas").SetActive (false);
					GameObject.FindGameObjectWithTag ("NormalModeCanvas").SetActive (true);
					GameObject.FindGameObjectWithTag ("HardModeCanvas").SetActive (false);
					beatLevelScore = beatNormalLevelScore;
					break;
				// Easy is the default
				default:
					GameObject.FindGameObjectWithTag ("EasyModeCanvas").SetActive (true);
					GameObject.FindGameObjectWithTag ("NormalModeCanvas").SetActive (false);
					GameObject.FindGameObjectWithTag ("HardModeCanvas").SetActive (false);
					beatLevelScore = beatEasyLevelScore;
					break;
			}

			config = LevelCatalog.Get (SceneManager.GetActiveScene ().name);
			if (config != null)
				beatLevelScore = config.energyTarget;

			beatLevelCanvas.SetActive (false);
			// Show intro level goal message (Only at first level load, doesnt show after a gameover)
			if (GameSettings.showIntroLevelMessage) {
				introBeatLevelText.text = "<color=#2EE6E6>" + (config != null ? config.displayName : "").ToUpperInvariant () + "</color>\nTHU " + beatLevelScore.ToString () + " LÕI NĂNG LƯỢNG";
				GameSettings.showIntroLevelMessage = false;
				StartCoroutine (ShowIntroBeatLevelCanvas ());
			}
		}

		// Setup score display.
		Collect (0);
			
	}

	/// <summary>
	/// Show intro level message with information about the coins that needs to be collected by the user to beat the level.
	/// </summary>
	public IEnumerator ShowIntroBeatLevelCanvas() {
		introBeatLevelCanvas.SetActive (true);
		mainCanvas.SetActive (false);
		GameFlow.Enter (FlowState.Intro);
		// Ends early when the card is tapped (UIButtonResumeGame moves GameFlow to Playing).
		float end = Time.realtimeSinceStartup + introBeatLevelTextDuration;
		while (GameFlow.State == FlowState.Intro && Time.realtimeSinceStartup < end)
			yield return null;
		if (GameFlow.State == FlowState.Intro)
			GameFlow.Enter (FlowState.Playing);
		introBeatLevelCanvas.SetActive (false);
		mainCanvas.SetActive (true);
	}

	/// <summary>
	/// Update is called once per frame.
	/// </summary>
	void Update () {
		switch (gameState)
		{
			case gameStates.Playing:
				ResolvePlaying ();
				break;
			case gameStates.Death:
			case gameStates.BeatLevel:
				// Fade by time, not per frame; the result cue already played once when the state began.
				float left = 1f - (Time.unscaledTime - fadeStart) / MusicFade;
				// Through the music's SoundGroup so a sound-settings change on the result screen keeps it faded.
				if (musicGroup != null)
					musicGroup.Fade = left;
				else if (backgroundMusic != null)
					backgroundMusic.volume = fadeFrom * Mathf.Max (0f, left);
				if (left <= 0f) {
					// If pass on current level should show set to true to show the intro message on the next level.
					if (gameState == gameStates.BeatLevel)
						GameSettings.showIntroLevelMessage = true;
					gameState = gameStates.GameOver;
				}
				break;
			case gameStates.GameOver:
				// Do nothing
				break;
		}

	}

	// Death and win are decided here only, death first (spec §3), independent of Health/GameManager Update order.
	void ResolvePlaying () {
		if (GameFlow.State != FlowState.Playing && GameFlow.State != FlowState.Dead)
			return;
		// GameFlow.Die from a hazard counts too, even before Health reacts.
		bool dead = GameFlow.State == FlowState.Dead || playerHealth == null || !playerHealth.isAlive
			|| (playerHealth.healthPoints <= 0 && playerHealth.numberOfLives <= 1);
		if (dead) {
			GameFlow.Die ("MẤT KẾT NỐI");
			gameState = gameStates.Death;
			BeginResult (SfxEvent.Lose);
			gameOverScoreDisplay.text = mainScoreDisplay.text;
			Transform cause = gameOverCanvas.transform.Find ("Lost Title");
			if (cause != null)
				cause.GetComponent<Text> ().text = GameFlow.DeathCause.ToUpperInvariant ();
			mainCanvas.SetActive (false);
			gameOverCanvas.SetActive (true);
			SelectButton ("Play Again Button");
			return;
		}
		if (canBeatLevel && score >= beatLevelScore && GameFlow.CompleteLevel ()) {
			gameState = gameStates.BeatLevel;
			BeginResult (config != null && config.IsFinal ? SfxEvent.CampaignWin : SfxEvent.LevelWin);
			if (config != null)
				CampaignProgress.CompleteLevel (config.levelId);
			player.SetActive (false);
			mainCanvas.SetActive (false);
			if (config != null && config.IsFinal) {
				StartCoroutine (GoToEnding ());
			} else {
				beatLevelCanvas.SetActive (true);
				SelectButton (isFinalLevel ? "Main Menu Button" : "Next Level Button");
			}
		}
	}

	void BeginResult (SfxEvent cue) {
		Sfx.Play (cue);
		fadeFrom = backgroundMusic != null ? backgroundMusic.volume : 0f;
		musicGroup = backgroundMusic != null ? backgroundMusic.GetComponent<SoundGroup> () : null;
		fadeStart = Time.unscaledTime;
	}

	// Fades Level4 to black before the Ending (which fades in from black), spec §8 "Fade từ L4".
	IEnumerator GoToEnding () {
		GameObject overlay = new GameObject ("Ending Fade", typeof (Canvas), typeof (UnityEngine.UI.Image));
		Canvas canvas = overlay.GetComponent<Canvas> ();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 1000;
		UnityEngine.UI.Image black = overlay.GetComponent<UnityEngine.UI.Image> ();
		black.raycastTarget = false;
		// As long as the music fade; the campaign win cue plays on into the Ending.
		for (float t = 0f; t < MusicFade; t += Time.unscaledDeltaTime) {
			black.color = new Color (0f, 0f, 0f, t / MusicFade);
			yield return null;
		}
		black.color = Color.black;
		SceneRouter.ToEnding ();
	}

	void SelectButton (string name) {
		GameObject eventSystem = GameObject.Find ("EventSystem");
		GameObject button = GameObject.Find (name);
		if (eventSystem != null && button != null)
			eventSystem.GetComponent<UnityEngine.EventSystems.EventSystem> ().SetSelectedGameObject (button);
	}

	/// <summary>
	/// Update the score text.
	/// </summary>
	public void Collect(int amount) {
		if (amount != 0 && GameFlow.State != FlowState.Playing)
			return;
		score += amount;
		if (canBeatLevel) {
			mainScoreDisplay.text = StoryText.EnergyLabel (score, beatLevelScore);
		} else {
			mainScoreDisplay.text = score.ToString ();
		}

	}
}
