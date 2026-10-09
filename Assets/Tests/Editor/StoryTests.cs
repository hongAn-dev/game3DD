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

	static RectTransform Rect(Component c) {
		return (RectTransform)c.transform;
	}

	// Spec §7.6: sound toggle left of pause, top right inside the safe area; icon ≈ 24–28 dp, touch ≥ 48 dp, ≥ 8 dp
	// apart (at the 1080-unit height-scaled canvas 1 dp ≈ 2.25 units on a 1080p phone: 48 dp ≈ 108, 8 dp ≈ 18 → use
	// 110 and 18).
	[Test]
	public void TopRightButtonsLayout() {
		foreach (string scene in new[] { "Level1", "Level2", "Level3", "Level4", "MainMenu", "Ending" }) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity", OpenSceneMode.Single);
			SoundToggle sound = Object.FindObjectsOfType<SoundToggle>(true).Single();
			Assert.IsNotNull(sound.GetComponentsInParent<SafeAreaFitter>(true).FirstOrDefault(), scene + " sound inside the safe area");
			Assert.AreEqual(Vector2.one, Rect(sound).anchorMin, scene);
			Assert.GreaterOrEqual(Mathf.Min(Rect(sound).sizeDelta.x, Rect(sound).sizeDelta.y), 110f, scene + " touch area");
			Assert.That(Rect(sound.icon).sizeDelta.x, Is.InRange(54f, 64f), scene + " icon");
			Assert.AreEqual(1f, sound.GetComponentsInParent<CanvasScaler>(true)[0].matchWidthOrHeight, scene);
			Assert.Greater(sound.GetComponentsInParent<Canvas>(true).Last().sortingOrder, 50, scene + " above the pause panel");
			PauseController pause = Object.FindObjectOfType<PauseController>(true);
			if (scene.StartsWith("Level")) {
				RectTransform p = Rect(pause.pauseButton.transform), q = Rect(sound);
				Assert.AreEqual(q.parent, p.parent, scene);
				Assert.AreEqual(Vector2.one, p.anchorMin, scene);
				Assert.GreaterOrEqual(Mathf.Min(p.sizeDelta.x, p.sizeDelta.y), 110f, scene + " pause touch area");
				float gap = (p.anchoredPosition.x - p.sizeDelta.x) - q.anchoredPosition.x;
				Assert.GreaterOrEqual(gap, 18f, scene + " sound sits left of pause with a gap");
				Assert.AreEqual(new Vector2(0.5f, 1f), Rect(GameObject.Find("Score Canvas").transform).anchorMin, scene + " energy panel moved to the top centre");
				Assert.IsNull(GameObject.Find("Mobile Canvas").transform.Find("Safe Area/Pause Button"), scene + " old pause button gone");
			} else {
				Assert.IsNull(pause, scene + " has no gameplay pause");
			}
		}
	}

	[Test]
	public void PausePanelHasTwoButtons() {
		foreach (LevelConfig level in LevelCatalog.All) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			PauseController pause = Object.FindObjectOfType<PauseController>(true);
			CollectionAssert.AreEqual(new[] { "Tiếp tục", "Thoát về menu" },
				pause.overlay.GetComponentsInChildren<Button>(true).Select(b => b.GetComponentInChildren<Text>(true).text).ToArray(), level.levelId);
			var texts = pause.overlay.GetComponentsInChildren<Text>(true).Where(t => t.GetComponentsInParent<Button>(true).Length == 0).Select(t => t.text).ToArray();
			CollectionAssert.AreEquivalent(new[] { "Tạm dừng", StoryText.PauseNote }, texts, level.levelId);
			Assert.IsEmpty(pause.overlay.GetComponentsInChildren<Slider>(true), level.levelId + " settings live in the main menu");
		}
	}

	// Spec §7.3/§7.6/§8.2: the main menu has Hướng dẫn chơi and Cài đặt (camera sensitivity, music/SFX/ambience).
	[Test]
	public void MainMenuHasSettingsAndGuide() {
		EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
		Transform play = Object.FindObjectsOfType<Button>(true).Single(b => b.name == "Play Button").transform;
		foreach (string label in new[] { "Hướng dẫn chơi", "Cài đặt" })
			Assert.IsTrue(play.parent.GetComponentsInChildren<Button>(true).Any(b => b.GetComponentInChildren<Text>(true).text == label), label);
		MainMenuPanels panels = Object.FindObjectOfType<MainMenuPanels>(true);
		Assert.IsNotNull(panels.dialog);
		Assert.IsFalse(panels.settings.activeSelf, "closed at start");
		Assert.AreEqual(0.5f, panels.sensitivity.minValue);
		Assert.AreEqual(2f, panels.sensitivity.maxValue);
		foreach (Slider slider in new[] { panels.sensitivity, panels.music, panels.sfx, panels.ambience }) {
			Assert.GreaterOrEqual(((RectTransform)slider.transform).sizeDelta.y, 70f, slider.name + " touch height");
			Assert.GreaterOrEqual(slider.handleRect.sizeDelta.x, 60f, slider.name + " handle");
			Assert.AreNotEqual(Color.white, slider.fillRect.GetComponent<Image>().color, slider.name);
		}
		foreach (Slider slider in new[] { panels.music, panels.sfx, panels.ambience })
			Assert.AreEqual(new Vector2(0f, 1f), new Vector2(slider.minValue, slider.maxValue), slider.name);
		var labels = panels.settings.GetComponentsInChildren<Text>(true).Select(t => t.text).ToArray();
		CollectionAssert.IsSubsetOf(new[] { "Cài đặt", "Độ nhạy camera", "Nhạc nền", "Hiệu ứng âm thanh", "Âm thanh môi trường", "Đóng" }, labels);
	}
}
