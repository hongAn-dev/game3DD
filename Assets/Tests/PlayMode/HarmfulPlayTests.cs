using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class HarmfulPlayTests {
    GameObject player;
    Health health;
    HarmfulSpawnDirector director;
    HarmfulPickup trap;
    float healthBefore;
    int scoreBefore;
    float slowRemainingBefore;
    bool gone;
    int reservationsBeforeCancel;

    IEnumerator LoadLevel2() {
        trap = null; gone = false; reservationsBeforeCancel = 0;
        CampaignProgress.BeginRun(GameSettings.gameDifficulties.Normal);
        GameSettings.showIntroLevelMessage=false;
        SceneManager.LoadScene("Level2");
        yield return null;
        yield return null;
        player=GameObject.FindWithTag("Player");
        health=player.GetComponent<Health>();
        health.numberOfLives=999;
        Rigidbody body=player.GetComponent<Rigidbody>();
        if(body!=null){body.isKinematic=true;body.velocity=Vector3.zero;}
        EnemyDirector enemies=Object.FindObjectOfType<EnemyDirector>();
        if(enemies!=null)enemies.enabled=false;
        foreach(EnemyBrain enemy in Object.FindObjectsOfType<EnemyBrain>())enemy.enabled=false;
        director=Object.FindObjectOfType<HarmfulSpawnDirector>();
        Assert.IsNotNull(director);
        director.intervalOverride=1000f;
        director.pulseDuration=1.3f;
        healthBefore=health.healthPoints;
        scoreBefore=GameManager.gm.score;
        SlowStatus slow=player.GetComponent<SlowStatus>();
        slowRemainingBefore=slow!=null?slow.Remaining:0f;
    }

    IEnumerator FindWarning(bool waitForScaleIn) {
        float deadline=Time.time+10f;
        while(trap==null&&Time.time<deadline){trap=Object.FindObjectOfType<HarmfulPickup>();yield return null;}
        Assert.IsNotNull(trap,"Level2 starts with a harmful pickup warning");
        Collider trigger=trap.GetComponent<Collider>();
        Assert.IsNotNull(trigger);
        Assert.IsFalse(trigger.enabled,"warning and scale-in keep the trigger disabled");
        reservationsBeforeCancel=director.AliveCount;
        // Exercise the magnetic effect even though the Player prefab owns an inactive SlowStatus component.
        trap.kind=HarmfulKind.Magnetic;
        trap.Gone+=_=>gone=true;
        if(waitForScaleIn) {
            float scaleAtWarning=trap.transform.localScale.magnitude;
            deadline=Time.time+3f;
            while(trap!=null&&!trigger.enabled&&trap.transform.localScale.magnitude>scaleAtWarning*.7f&&Time.time<deadline)
                yield return null;
            Assert.IsNotNull(trap,"trap remains through the warning pulse");
            Assert.IsFalse(trigger.enabled,"trigger stays off through scale-in");
            Assert.Less(trap.transform.localScale.magnitude,scaleAtWarning*.7f,"scale-in has begun");
        }
    }

    void MovePlayerIntoTrap() {
        Collider trigger=trap.GetComponent<Collider>();
        SphereCollider sphere=trigger as SphereCollider;
        BoxCollider box=trigger as BoxCollider;
        CapsuleCollider capsule=trigger as CapsuleCollider;
        Vector3 triggerCenter=sphere!=null?sphere.transform.TransformPoint(sphere.center):
            box!=null?box.transform.TransformPoint(box.center):
            capsule!=null?capsule.transform.TransformPoint(capsule.center):trigger.transform.position;
        Collider playerCollider=player.GetComponent<Collider>();
        Vector3 playerCenterOffset=playerCollider!=null?playerCollider.bounds.center-player.transform.position:Vector3.zero;
        player.transform.position=triggerCenter-playerCenterOffset;
        Rigidbody body=player.GetComponent<Rigidbody>();
        if(body!=null){body.position=player.transform.position;body.velocity=Vector3.zero;}
        Physics.SyncTransforms();
    }

    IEnumerator AssertCanceledWithoutEffect() {
        float deadline=Time.time+2f;
        while(!gone&&Time.time<deadline)yield return null;
        Assert.IsTrue(gone,"overlapping warning cancels the pickup");
        Assert.AreEqual(Mathf.Max(0,reservationsBeforeCancel-1),director.AliveCount,"cancellation releases this pickup's reservation");
        Assert.AreEqual(healthBefore,health.healthPoints,"an unactivated poison warning cannot damage the robot");
        Assert.AreEqual(scoreBefore,GameManager.gm.score,"an unactivated anti-energy warning cannot change score");
        SlowStatus slow=player.GetComponent<SlowStatus>();
        Assert.IsNotNull(slow,"the player prefab owns its magnetic status component");
        Assert.LessOrEqual(slow.Remaining,slowRemainingBefore+0.02f,"an unactivated magnetic warning does not apply or refresh slow");
    }

    [TearDown]
    public void Reset() { GameFlow.ResetForScene(); }

    [UnityTest]
    public IEnumerator EnteringDuringPulseCancelsBeforeActivation() {
        yield return LoadLevel2();
        yield return FindWarning(false);
        MovePlayerIntoTrap();
        yield return AssertCanceledWithoutEffect();
    }

    [UnityTest]
    public IEnumerator EnteringDuringScaleInCancelsBeforeActivation() {
        yield return LoadLevel2();
        yield return FindWarning(true);
        MovePlayerIntoTrap();
        yield return AssertCanceledWithoutEffect();
    }

    [UnityTest]
    public IEnumerator LevelEndFinishesActiveTrapAndReleasesItsReservation() {
        yield return LoadLevel2();
        yield return FindWarning(false);
        Assert.Greater(director.AliveCount,0);
        GameFlow.Enter(FlowState.LevelComplete);
        yield return null;
        Assert.IsTrue(gone,"level completion finishes active harmful pickups");
        Assert.AreEqual(0,director.AliveCount,"level completion releases the harmful reservation");
        Assert.AreEqual(healthBefore,health.healthPoints);
    }
}
