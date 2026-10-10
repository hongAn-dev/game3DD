using UnityEngine;

public enum HarmfulKind { Poison, AntiEnergy, Magnetic }

/// <summary>One-use harmful ground item. Its director owns warning/activation/placement; this component owns effect/consume.</summary>
[RequireComponent(typeof(Collider))]
public sealed class HarmfulPickup : MonoBehaviour {
    public HarmfulKind kind;
    public int damage;
    public float lifetime = 20f;
    public float blinkTime = 3f;

    public event System.Action<HarmfulPickup> Gone;
    public bool Consumed { get { return gone; } }

    float age;
    bool lifetimeStarted;
    bool gone;
    Transform model;
    Vector3 modelScale;

    void Awake() {
        model = transform.Find("Model");
        if (model != null)
            modelScale = model.localScale;
    }

    void OnTriggerEnter(Collider other) {
        if (gone || !GameFlow.IsGameplayActive || !other.CompareTag("Player"))
            return;
        Consume(other.gameObject);
    }

    /// <summary>Starts gameplay lifetime when the spawn director enables the trigger.</summary>
    public void BeginLifetime() {
        if (!gone) {
            age = 0f;
            lifetimeStarted = true;
        }
    }

    public bool Consume(GameObject robot) {
        if (gone || robot == null || !GameFlow.IsGameplayActive)
            return false;
        gone = true;   // one consume even when the player has multiple colliders
        ApplyEffect(robot);
        Sfx.Play(SfxEvent.Trap);
        if (Gone != null)
            Gone(this);
        Destroy(gameObject);
        return true;
    }

    void ApplyEffect(GameObject robot) {
        switch (kind) {
        case HarmfulKind.Poison: {
            Health health = robot.GetComponent<Health>();
            if (health == null)
                return;
            bool applied = health.TakeDamage(Mathf.Max(0, damage), DamageKind.Trap);
            if (applied && health.healthPoints <= 0f)
                GameFlow.ReportDeathCause("Robo chạm hóa chất độc hại");
            break;
        }
        case HarmfulKind.AntiEnergy:
            if (GameManager.gm != null)
                GameManager.gm.QueueScoreDelta(this, -1, true);
            break;
        case HarmfulKind.Magnetic: {
            SlowStatus slow = robot.GetComponent<SlowStatus>();
            if (slow == null)
                slow = robot.AddComponent<SlowStatus>();
            slow.Apply(SlowStatus.Duration);
            break;
        }
        }
    }

    void Update() {
        if (gone || !lifetimeStarted || !GameFlow.IsGameplayActive)
            return;
        age += Time.deltaTime;
        if (age >= lifetime) {
            Finish();
            return;
        }
        if (model != null && lifetime - age <= blinkTime)
            model.localScale = modelScale * (0.85f + 0.15f * (0.5f + 0.5f * Mathf.Cos(age * Mathf.PI * 4f)));
    }

    /// <summary>Safe to call from timeout, spawn cancellation, director disable, or pickup.</summary>
    public void Finish() {
        if (gone)
            return;
        gone = true;
        if (Gone != null)
            Gone(this);
        Destroy(gameObject);
    }
}
