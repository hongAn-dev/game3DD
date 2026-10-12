using UnityEngine;
using UnityEngine.UI;

/// <summary>Brief score-loss feedback, kept separate from the persistent energy counter.</summary>
public sealed class ScorePickupFeedback : MonoBehaviour {
    public Text label;
    GameManager manager;
    float remaining;
    void Start() {
        manager = GameManager.gm;
        if (manager != null) manager.ScoreFeedback += Show;
        if (label != null) label.gameObject.SetActive(false);
    }
    void OnDestroy() { if (manager != null) manager.ScoreFeedback -= Show; }
    void Show(int delta) {
        if (delta >= 0 || label == null) return;
        label.text = "−1 năng lượng";
        remaining = 1.5f;
        label.gameObject.SetActive(true);
    }
    void Update() {
        if (GameFlow.State == FlowState.Dead || GameFlow.State == FlowState.LevelComplete || GameFlow.State == FlowState.Transition) {
            remaining = 0f;
            if (label != null) label.gameObject.SetActive(false);
            return;
        }
        if (!GameFlow.IsGameplayActive || remaining <= 0f) return;
        remaining = Mathf.Max(0f, remaining - Time.deltaTime);
        if (remaining <= 0f && label != null) label.gameObject.SetActive(false);
    }
}
