using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class StoryTests {

	// Spec §7.2: display names change, scene IDs stay.
	[Test]
	public void LevelNamesFollowTheTable() {
		Assert.AreEqual("Đảo hoang", LevelCatalog.Get("Level1").displayName);
		Assert.AreEqual("Trạm khai thác bỏ hoang", LevelCatalog.Get("Level2").displayName);
		Assert.AreEqual("Thung lũng dung nham", LevelCatalog.Get("Level3").displayName);
		Assert.AreEqual("Bãi phóng bỏ hoang", LevelCatalog.Get("Level4").displayName);
		Assert.AreEqual("Hành trình trở về", StoryText.EndingName);
	}

	// Spec §7.4: goals use the config's target and name.
	[Test]
	public void GoalsComeFromConfig() {
		Assert.AreEqual("Thu thập đủ 6 viên năng lượng để qua màn. Hãy cố gắng né quái vật và đừng để Robo rơi khỏi đất liền. Nếu cần nghỉ, hãy chạm nút tạm dừng ở góc trên bên phải màn hình.",
			StoryText.Goal(LevelCatalog.Get("Level1")));
		StringAssert.StartsWith("Thu thập đủ 18 viên năng lượng để khởi động phi thuyền và hoàn thành hành trình.", StoryText.Goal(LevelCatalog.Get("Level4")));
		LevelConfig tuned = Object.Instantiate(LevelCatalog.Get("Level2"));
		tuned.energyTarget = 12;
		StringAssert.StartsWith("Thu thập đủ 12 viên năng lượng để qua màn.", StoryText.Goal(tuned));
		Object.DestroyImmediate(tuned);
	}

	// Spec §7.3: six pages, the sixth is the Đảo hoang card (lead + goal, goal once).
	[Test]
	public void TutorialHasSixPagesEndingOnTheL1Card() {
		StoryPage[] pages = StoryText.Tutorial(LevelCatalog.Get("Level1"));
		Assert.AreEqual(6, pages.Length);
		CollectionAssert.AreEqual(new[] { "Robo lạc lối", "Di chuyển và quan sát", "Thu thập năng lượng", "Bảo vệ Robo", "Vật phẩm hỗ trợ", "Đảo hoang" },
			pages.Select(p => p.title).ToArray());
		StoryPage card = StoryText.LevelCard(LevelCatalog.Get("Level1"));
		Assert.AreEqual(card.title, pages[5].title);
		Assert.AreEqual(card.body, pages[5].body);
		StringAssert.StartsWith("Robo tỉnh dậy trên một hòn đảo xa lạ", card.body);
		Assert.AreEqual(1, card.body.Split(new[] { "Thu thập đủ" }, System.StringSplitOptions.None).Length - 1, "goal once");
		StringAssert.Contains("Robo bắt đầu mỗi màn chơi với 100 HP.", pages[3].body);
	}

	static string[] AllTexts() {
		var texts = StoryText.Tutorial(LevelCatalog.Get("Level1")).SelectMany(p => new[] { p.title, p.body }).ToList();
		foreach (LevelConfig level in LevelCatalog.All) {
			StoryPage card = StoryText.LevelCard(level);
			texts.Add(card.title);
			texts.Add(card.body);
			if (!level.IsFinal) {
				StoryPage done = StoryText.LevelComplete(level);
				texts.Add(done.title);
				texts.Add(done.body);
			}
		}
		texts.AddRange(StoryText.Guide().SelectMany(p => new[] { p.title, p.body }));
		texts.AddRange(new[] { StoryText.Lost, StoryText.ShipReady.title, StoryText.ShipReady.body, StoryText.Finale.title, StoryText.Finale.body,
			StoryText.PauseNote });
		return texts.ToArray();
	}

	// Spec §7.2: full sentences, the agreed words, no jargon or cut text.
	[Test]
	public void NoShortJargonInPlayerTexts() {
		foreach (string text in AllTexts()) {
			Assert.IsNotEmpty(text);
			foreach (string bad in new[] { "spawn", "cap", "trigger", "map", "lõi", "…", "...", "Thu 1" })
				Assert.IsFalse(text.ToLowerInvariant().Contains(bad.ToLowerInvariant()), "'" + bad + "' in: " + text);
		}
		Assert.AreEqual("Bạn đã thua", StoryText.Lost);
	}

	[Test]
	public void HudLabelReadsNangLuong() {
		Assert.AreEqual("Năng lượng: 3/10", StoryText.EnergyLabel(3, 10));
	}

	static void AssertFits(Text text, string value) {
		Vector2 size = text.rectTransform.rect.size;
		TextGenerationSettings settings = text.GetGenerationSettings(size);
		settings.horizontalOverflow = HorizontalWrapMode.Wrap;
		settings.verticalOverflow = VerticalWrapMode.Overflow;
		float height = new TextGenerator().GetPreferredHeight(value, settings) / text.pixelsPerUnit;
		Assert.LessOrEqual(height, size.y, text.name + ": " + value);
	}

	// Spec §7.4: no cut text. The dialog canvas scales by height (1080 units on every phone) and the box fits the
	// narrowest accepted width (16:9 = 1920 units); 18:9 and 20:9 only add width.
	[Test]
	public void EveryPageFitsAt16_9And20_9() {
		var pages = StoryText.Tutorial(LevelCatalog.Get("Level1")).Concat(StoryText.Guide()).ToList();
		foreach (LevelConfig level in LevelCatalog.All)
			pages.Add(StoryText.LevelCard(level));
		foreach (string scene in new[] { "Level1", "Level2", "Level3", "Level4", "MainMenu" }) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity", OpenSceneMode.Single);
			DialogPanel dialog = Object.FindObjectOfType<DialogPanel>(true);
			Assert.IsNotNull(dialog, scene);
			CanvasScaler scaler = dialog.GetComponentsInParent<CanvasScaler>(true)[0];
			Assert.AreEqual(1f, scaler.matchWidthOrHeight, scene + " scales by height");
			Assert.AreEqual(new Vector2(1920f, 1080f), scaler.referenceResolution);
			RectTransform box = (RectTransform)dialog.box.transform;
			Assert.LessOrEqual(box.sizeDelta.x, 1920f - 2 * 80f, scene + " box width with safe-area margins");
			Assert.LessOrEqual(box.sizeDelta.y, 1080f - 2 * 60f, scene + " box height");
			Assert.GreaterOrEqual(dialog.body.fontSize, 32, scene + " readable body");
			foreach (StoryPage page in pages) {
				AssertFits(dialog.title, page.title);
				AssertFits(dialog.body, page.body);
			}
		}
	}
}
