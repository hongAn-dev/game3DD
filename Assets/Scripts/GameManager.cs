using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.LowLevel;

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
	[Tooltip("Hidden on the lose/level-complete panels: minimap camera, robot HP bar (the main canvas hides too).")]
	public GameObject[] hideOnResult = new GameObject[0];

	[Tooltip("Only need to set if canBeatLevel is set to true.")]
	public GameObject beatLevelCanvas;

	public AudioSource backgroundMusic;

	/// <summary>Seconds (real time) the music takes to fade out after a death or a win.</summary>
	public const float MusicFade = 1f;
	float fadeFrom;
	float fadeStart;
	SoundGroup musicGroup;

	[Tooltip("Story dialog: New Game tutorial and the level's intro card.")]
	public DialogPanel dialog;

	private Health playerHealth;

	private LevelConfig config;
	private readonly Dictionary<int, int> pendingPickupDeltas = new Dictionary<int, int>();
	private readonly HashSet<int> feedbackPickupIds = new HashSet<int>();
	private int pendingAnonymousDelta;
	public event System.Action<int, int> ScoreDeltaCommitted;
	public event System.Action<int> ScoreFeedback;
	static bool postPhysicsHookInstalled;
	static bool postPhysicsHookAttempted;
	struct ScoreCommitAfterPhysics { }

	// The beat score level
	private int beatLevelScore = 0;

	public int BeatLevelScore { get { return beatLevelScore; } }
	public static bool HasPostPhysicsScoreHook { get { return postPhysicsHookInstalled; } }

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	static void ResetPostPhysicsHookState() {
		postPhysicsHookInstalled = false;
		postPhysicsHookAttempted = false;
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	static void InstallPostPhysicsScoreHook() {
		if (postPhysicsHookAttempted) return;
		postPhysicsHookAttempted = true;
		PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
		if (InsertAfterPhysics(ref loop)) {
			PlayerLoop.SetPlayerLoop(loop);
			postPhysicsHookInstalled = true;
		} else {
			Debug.LogError("GameManager: Unity PlayerLoop has no PhysicsFixedUpdate; score deltas will use the FixedUpdate fallback.");
		}
	}

	static bool InsertAfterPhysics(ref PlayerLoopSystem parent) {
		if (parent.subSystemList == null) return false;
		if (parent.type == typeof(UnityEngine.PlayerLoop.FixedUpdate)) {
			PlayerLoopSystem[] systems = parent.subSystemList;
			for (int i = 0; i < systems.Length; i++) {
				if (systems[i].type == typeof(ScoreCommitAfterPhysics)) return true;
				if (systems[i].type != typeof(UnityEngine.PlayerLoop.FixedUpdate.PhysicsFixedUpdate)) continue;
				var expanded = new PlayerLoopSystem[systems.Length + 1];
				System.Array.Copy(systems, 0, expanded, 0, i + 1);
				expanded[i + 1] = new PlayerLoopSystem { type = typeof(ScoreCommitAfterPhysics), updateDelegate = CommitAfterPhysics };
				System.Array.Copy(systems, i + 1, expanded, i + 2, systems.Length - i - 1);
				parent.subSystemList = expanded;
				return true;
			}
		}
		for (int i = 0; i < parent.subSystemList.Length; i++) {
			PlayerLoopSystem child = parent.subSystemList[i];
			if (InsertAfterPhysics(ref child)) { parent.subSystemList[i] = child; return true; }
		}
		return false;
	}

	static void CommitAfterPhysics() {
		if (gm != null) gm.CommitPhysicsStep();
	}

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
			StartStory ();
		}

		// Setup score display.
		Collect (0);
			
	}

	/// <summary>
	/// Spec §7.3/§7.4: after New Game the tutorial (its last page is the first level's card), otherwise the level's card
	/// on its first entry in this run; nothing on Retry. Gameplay stays frozen (Intro) until "Bắt đầu".
	/// </summary>
	void StartStory () {
		bool tutorial = config != null && config == LevelCatalog.First && CampaignProgress.TutorialPending;
		bool card = config != null && GameSettings.showIntroLevelMessage && !CampaignProgress.IntroSeen (config.levelId);
		CampaignProgress.TutorialPending = false;
		GameSettings.showIntroLevelMessage = false;
		if (dialog == null || (!tutorial && !card))
			return;
		GameFlow.Enter (FlowState.Intro);
		mainCanvas.SetActive (false);
		StoryPage[] pages = tutorial ? StoryText.Tutorial (config) : StoryText.LevelCards (config);
		int finalPage = pages.Length - 1;
		int skipTo = tutorial || pages.Length > 1 ? finalPage : -1;
		dialog.Show (pages, skipTo, "Bắt đầu", () => {
			CampaignProgress.MarkIntroSeen (config.levelId);
			mainCanvas.SetActive (true);
			if (GameFlow.State == FlowState.Intro)
				GameFlow.Enter (FlowState.Playing);
		});
	}

	/// <summary>
	/// Update is called once per frame.
	/// </summary>
	void Update () {
			switch (gameState)
		{
			case gameStates.Playing:
				CommitPendingScoreDeltas ();
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

	// Fallback if PlayerLoop injection is unavailable: FixedUpdate runs before simulation,
	// so it commits the preceding physics step. Update drains the last step of each frame.
	void FixedUpdate () {
		if (!postPhysicsHookInstalled && gameState == gameStates.Playing) {
			CommitPendingScoreDeltas ();
			ResolvePlaying ();
		}
	}

	void CommitPhysicsStep () {
		if (gameState != gameStates.Playing) return;
		CommitPendingScoreDeltas ();
		ResolvePlaying ();
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
			if (config != null)
				SaveProgress.RecordBest (config, score);
			// The panel only says "Bạn đã thua" (spec §7.1); the cause goes to the development log.
			if (Debug.isDebugBuild)
				Debug.Log ("GameManager: lost, cause: " + GameFlow.DeathCause);
			HideHud ();
			gameOverCanvas.SetActive (true);
			SelectButton ("Play Again Button");
			return;
		}
		if (canBeatLevel && score >= beatLevelScore && GameFlow.CompleteLevel ()) {
			gameState = gameStates.BeatLevel;
			BeginResult (config != null && config.IsFinal ? SfxEvent.CampaignWin : SfxEvent.LevelWin);
			if (config != null)
				CampaignProgress.CompleteLevel (config.levelId);
			if (config != null)
				SaveProgress.RecordBest (config, score);   // unlocks the next level for "Chơi tiếp"
			player.SetActive (false);
			HideHud ();
			if (config != null && config.IsFinal) {
				StartCoroutine (GoToEnding ());
			} else {
				if (config != null) {
					StoryPage done = StoryText.LevelComplete (config);
					beatLevelCanvas.transform.Find ("Congratulations Text").GetComponent<Text> ().text = done.title;
					beatLevelCanvas.transform.Find ("Complete Body").GetComponent<Text> ().text = done.body;
				}
				beatLevelCanvas.SetActive (true);
				SelectButton (isFinalLevel ? "Main Menu Button" : "Next Level Button");
			}
		}
	}

	void HideHud () {
		mainCanvas.SetActive (false);
		foreach (GameObject hud in hideOnResult)
			if (hud != null)
				hud.SetActive (false);
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
		if (amount == 0) { RefreshScoreDisplay (); return; }
		QueueScoreDelta (amount);
	}

	/// <summary>Queues one pickup's score change for the current physics simulation step. A source contributes once.</summary>
	public bool QueueScoreDelta (Object source, int delta) {
		return QueueScoreDelta (source, delta, false);
	}

	public bool QueueScoreDelta (Object source, int delta, bool showFeedback) {
		if (source == null || delta == 0 || GameFlow.State != FlowState.Playing) return false;
		int id = source.GetInstanceID ();
		if (pendingPickupDeltas.ContainsKey (id)) return false;
		pendingPickupDeltas.Add (id, delta);
		if (showFeedback) feedbackPickupIds.Add (id);
		return true;
	}

	/// <summary>Queues a non-pickup change for the current physics step.</summary>
	public bool QueueScoreDelta (int delta) {
		if (delta == 0 || GameFlow.State != FlowState.Playing) return false;
		pendingAnonymousDelta += delta;
		return true;
	}

	void CommitPendingScoreDeltas () {
		if (GameFlow.State != FlowState.Playing) { pendingPickupDeltas.Clear (); feedbackPickupIds.Clear (); pendingAnonymousDelta = 0; return; }
		if (pendingPickupDeltas.Count == 0 && pendingAnonymousDelta == 0) return;
		int delta = pendingAnonymousDelta;
		pendingAnonymousDelta = 0;
		int negativeFeedbacks = 0;
		foreach (KeyValuePair<int, int> entry in pendingPickupDeltas) {
			delta += entry.Value;
			if (entry.Value < 0 && feedbackPickupIds.Contains (entry.Key))
				negativeFeedbacks++;
		}
		pendingPickupDeltas.Clear ();
		feedbackPickupIds.Clear ();
		int before = score;
		int upper = canBeatLevel && beatLevelScore > 0 ? beatLevelScore : int.MaxValue;
		score = Mathf.Clamp (score + delta, 0, upper);
		int applied = score - before;
		RefreshScoreDisplay ();
		if (ScoreDeltaCommitted != null) ScoreDeltaCommitted (applied, score);
		int lossesToShow = Mathf.Min(before, negativeFeedbacks);
		for (int i = 0; i < lossesToShow; i++)
			if (ScoreFeedback != null) ScoreFeedback (-1);
	}

	void RefreshScoreDisplay () {
		if (mainScoreDisplay == null) return;
		int shown = Mathf.Max (0, score);
		if (canBeatLevel) {
			if (beatLevelScore > 0) shown = Mathf.Min (shown, beatLevelScore);
			mainScoreDisplay.text = StoryText.EnergyLabel (shown, beatLevelScore);
		} else {
			mainScoreDisplay.text = shown.ToString ();
		}
	}
}
