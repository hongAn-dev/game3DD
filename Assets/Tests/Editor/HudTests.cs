using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class HudTests {

	[Test]
	public void HealthBarColorsFollowThresholds() {
		Assert.AreEqual(PlayerHealthBar.Green, PlayerHealthBar.ColorFor(1f));
		Assert.AreEqual(PlayerHealthBar.Green, PlayerHealthBar.ColorFor(0.51f));
		Assert.AreEqual(PlayerHealthBar.Yellow, PlayerHealthBar.ColorFor(0.5f));
		Assert.AreEqual(PlayerHealthBar.Yellow, PlayerHealthBar.ColorFor(0.26f));
		Assert.AreEqual(PlayerHealthBar.Red, PlayerHealthBar.ColorFor(0.25f));
		Assert.AreEqual(PlayerHealthBar.Red, PlayerHealthBar.ColorFor(0f));
		Assert.AreEqual("88/100", PlayerHealthBar.Label(88f, 100f));
		Assert.AreEqual("1/100", PlayerHealthBar.Label(0.4f, 100f), "a sliver of HP is still shown as alive");
	}

	[Test]
	public void EveryLevelHasAHealthBarWithoutRaycasts() {
		foreach (LevelConfig level in LevelCatalog.All) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			PlayerHealthBar bar = Object.FindObjectOfType<PlayerHealthBar>(true);
			Assert.IsNotNull(bar, level.levelId);
			Assert.AreEqual(Image.Type.Filled, bar.fill.type, level.levelId);
			Vector2 size = bar.fill.rectTransform.rect.size;
			Assert.That(size.x, Is.InRange(64f, 88f), level.levelId + " bar width");
			Assert.That(size.y, Is.InRange(6f, 8f), level.levelId + " bar height");
			foreach (Graphic g in bar.GetComponentsInChildren<Graphic>(true))
				Assert.IsFalse(g.raycastTarget, level.levelId + " " + g.name);
			Assert.AreEqual(RenderMode.ScreenSpaceOverlay, bar.GetComponentInParent<Canvas>().rootCanvas.renderMode, "screen overlay: never drawn in the minimap");
		}
	}
}
