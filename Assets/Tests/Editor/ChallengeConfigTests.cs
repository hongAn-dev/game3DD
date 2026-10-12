using NUnit.Framework;
using UnityEngine;

public class ChallengeConfigTests {
    [Test]
    public void GroundSpawnAndEnemyBudgetsMatchChallenge() {
        int[][] chasers = { new[] {1,2,2}, new[] {2,3,3}, new[] {3,4,5}, new[] {4,5,6} };
        int[] starts = {8,12,16,20}, caps = {11,15,19,23}, points = {20,40,64,88};
        int[] trapCaps = {0,2,4,6}, trapStarts = {0,1,2,3}, trapDamage = {0,15,20,25};
        int[] initialEnemy = {1,2,3,4}, enemyBatch = {1,1,2,3};
        float[] risk = {.15f,.30f,.45f,.55f};
        float[] windupMax = {.55f,.45f,.40f,.35f};
        for (int i=0;i<4;i++) {
            LevelConfig c=LevelCatalog.All[i];
            Assert.AreEqual(starts[i],c.energyAtStart,c.levelId);
            Assert.AreEqual(caps[i],c.energyCap,c.levelId);
            Assert.AreEqual(c.energyTarget + 2,c.EnergySupplyMin,c.levelId + " minimum energy supply");
            Assert.AreEqual(c.energyTarget + 5,c.EnergySupplyMax,c.levelId + " maximum energy supply");
            Assert.AreEqual(points[i],c.energyPointMinimum,c.levelId);
            Assert.AreEqual(risk[i],c.energyRiskFraction,.001f,c.levelId);
            Assert.AreEqual(windupMax[i],c.creepWindupMax,.001f,c.levelId);
            CollectionAssert.AreEqual(chasers[i],c.maxChasers,c.levelId);
            Assert.AreEqual(initialEnemy[i],c.enemyInitialBatch,c.levelId);
            Assert.AreEqual(enemyBatch[i],c.enemyBatchMax,c.levelId);
            Assert.AreEqual(8f,c.bossSpacing,.001f,c.levelId);
            Assert.AreEqual(trapCaps[i],c.harmfulCap,c.levelId);
            Assert.AreEqual(trapStarts[i],c.harmfulAtStart,c.levelId);
            Assert.AreEqual(trapDamage[i],c.harmfulDamage,c.levelId);
            Assert.AreEqual(20f,c.harmfulLifetime,.001f,c.levelId);
        }
    }
}
