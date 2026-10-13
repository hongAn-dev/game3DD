using UnityEngine;

/// <summary>Temporary magnetic slow. Ball composes this multiplier with Overdrive and the base speed.</summary>
[RequireComponent(typeof(Ball))]
public sealed class SlowStatus : MonoBehaviour {
	public const float Duration = 5f;
	public const float SlowMultiplier = 0.5f;

    public bool Active { get { return remaining > 0f; } }
    public float Remaining { get { return remaining; } }

    Ball ball;
    float remaining;

    void Awake() {
        ball = GetComponent<Ball>();
    }

    void OnEnable() {
        GameFlow.Changed += OnFlowChanged;
    }

    void OnDestroy() {
        GameFlow.Changed -= OnFlowChanged;
    }

    void OnFlowChanged(FlowState state) {
        if (state == FlowState.Dead || state == FlowState.LevelComplete || state == FlowState.Transition)
            Clear();
    }

    public void Apply(float duration = Duration) {
        if (!GameFlow.IsGameplayActive || duration <= 0f)
            return;
        remaining = duration;
        ApplyMultiplier();
    }

    void Update() {
        if (!GameFlow.IsGameplayActive)
            return;
        if (remaining > 0f) {
            remaining = Mathf.Max(0f, remaining - Time.deltaTime);
            ApplyMultiplier();
        }
    }

    void ApplyMultiplier() {
        if (ball != null)
            ball.SlowMultiplier = Active ? SlowMultiplier : 1f;
    }

    void Clear() {
        remaining = 0f;
        ApplyMultiplier();
    }

    void OnDisable() {
        GameFlow.Changed -= OnFlowChanged;
        Clear();
    }
}
