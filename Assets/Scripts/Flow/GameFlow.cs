using UnityEngine;
using UnityEngine.SceneManagement;

public enum FlowState { Intro, Playing, Paused, Dead, LevelComplete, Transition }

/// <summary>
/// Single gameplay state every system reads (spec §3): AI, spawners, pickups and input run only while Playing.
/// Intro and Paused freeze time; every scene load starts again in Playing with timeScale 1.
/// </summary>
public static class GameFlow {

	public static FlowState State { get; private set; } = FlowState.Playing;
	public static string DeathCause { get; private set; } = "";
	public static event System.Action<FlowState> Changed;

	static string reportedCause = "";

	public static bool IsGameplayActive { get { return State == FlowState.Playing; } }

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	static void HookSceneLoads() {
		SceneManager.sceneLoaded += (scene, mode) => ResetForScene();
	}

	public static void ResetForScene() {
		State = FlowState.Playing;
		DeathCause = "";
		reportedCause = "";
		Time.timeScale = 1f;
	}

	public static void Enter(FlowState next) {
		if (State == next)
			return;
		State = next;
		Time.timeScale = next == FlowState.Paused || next == FlowState.Intro ? 0f : 1f;
		if (Changed != null)
			Changed(next);
	}

	public static bool Pause() {
		if (State != FlowState.Playing)
			return false;
		Enter(FlowState.Paused);
		return true;
	}

	public static bool Resume() {
		if (State != FlowState.Paused)
			return false;
		Enter(FlowState.Playing);
		return true;
	}

	/// <summary>Hazards call this just before applying damage so the game over text names the cause.</summary>
	public static void ReportDeathCause(string cause) {
		if (reportedCause.Length == 0 && !string.IsNullOrEmpty(cause))
			reportedCause = cause;
	}

	/// <summary>Ends the level as lost. Ignored once the level is already over.</summary>
	public static bool Die(string fallbackCause) {
		if (State == FlowState.Dead || State == FlowState.LevelComplete || State == FlowState.Transition)
			return false;
		DeathCause = reportedCause.Length > 0 ? reportedCause : fallbackCause;
		Enter(FlowState.Dead);
		return true;
	}

	/// <summary>Ends the level as won, only from Playing (death has priority, spec §3).</summary>
	public static bool CompleteLevel() {
		if (State != FlowState.Playing)
			return false;
		Enter(FlowState.LevelComplete);
		return true;
	}
}
