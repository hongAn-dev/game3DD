using NUnit.Framework;
using UnityEngine;

public class SaveProgressTests {

	// The tests share the device's PlayerPrefs with the editor: keep and restore the real progress.
	int[] saved;

	[SetUp]
	public void Keep() {
		saved = new int[LevelCatalog.All.Count];
		for (int i = 0; i < saved.Length; i++)
			saved[i] = SaveProgress.Best(LevelCatalog.All[i]);
		SaveProgress.Reset();
	}

	[TearDown]
	public void Restore() {
		SaveProgress.Reset();
		for (int i = 0; i < saved.Length; i++)
			SaveProgress.RecordBest(LevelCatalog.All[i], saved[i]);
	}

	[Test]
	public void LevelsUnlockInOrderAndTotalsAreCapped() {
		LevelConfig l1 = LevelCatalog.Get("Level1"), l2 = LevelCatalog.Get("Level2"), l3 = LevelCatalog.Get("Level3");
		Assert.IsTrue(SaveProgress.Unlocked(l1), "level 1 is always open");
		Assert.IsFalse(SaveProgress.Unlocked(l2));
		Assert.AreEqual(48, SaveProgress.TotalTarget());
		SaveProgress.RecordBest(l1, 4);
		Assert.IsFalse(SaveProgress.Unlocked(l2), "not finished yet");
		SaveProgress.RecordBest(l1, 3);
		Assert.AreEqual(4, SaveProgress.Best(l1), "keeps the best");
		SaveProgress.RecordBest(l1, 9);
		Assert.AreEqual(6, SaveProgress.Best(l1), "capped at the target");
		Assert.IsTrue(SaveProgress.Unlocked(l2));
		Assert.IsFalse(SaveProgress.Unlocked(l3));
		SaveProgress.RecordBest(l2, 7);
		Assert.AreEqual(13, SaveProgress.TotalEnergy());
		SaveProgress.Reset();
		Assert.AreEqual(0, SaveProgress.TotalEnergy());
		Assert.IsFalse(SaveProgress.Unlocked(l2), "New Game locks everything after level 1");
	}
}
