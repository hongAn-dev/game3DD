using System.Collections.Generic;
using UnityEngine;

/// <summary>One dialog page: a title, full text, and optional visual from Resources/GuideSprites.</summary>
public struct StoryPage {
	public string title;
	public string body;
	public string illustrationKey;

	public StoryPage(string title, string body) {
		this.title = title;
		this.body = body;
		this.illustrationKey = "";
	}

	public StoryPage(string title, string body, string illustrationKey) {
		this.title = title;
		this.body = body;
		this.illustrationKey = illustrationKey ?? "";
	}
}

/// <summary>
/// Every story line the player reads (spec §7.2–§7.5), in one place. Level names and energy targets come from
/// LevelConfig so the texts follow tuning; the rest is verbatim from the spec.
/// </summary>
public static class StoryText {

	public const string EndingName = "Hành trình trở về";
	public const string Lost = "Bạn đã thua";
	public const string PauseNote = "Nếu thoát về menu, bạn sẽ phải bắt đầu lại hành trình.";

	public static readonly StoryPage ShipReady = new StoryPage("Phi thuyền đã sẵn sàng",
		"Năng lượng đã được nạp đầy. Robo đã có thể khởi động phi thuyền và rời khỏi hành tinh xa lạ.");
	public static readonly StoryPage Finale = new StoryPage("Robo đã tìm được đường về",
		"Nhờ sự giúp đỡ của bạn, Robo đã vượt qua mọi hiểm nguy và rời khỏi hành tinh an toàn. Cảm ơn bạn đã đồng hành cùng Robo trong chuyến phiêu lưu này!");

	static readonly StoryPage[] Basics = {
		new StoryPage("Robo lạc lối", "Sau một sự cố, Robo bị mắc kẹt trên một hành tinh xa lạ. Hãy cùng Robo vượt qua bốn khu vực, thu thập năng lượng để khởi động lại phi thuyền và tìm đường trở về."),
		new StoryPage("Di chuyển và quan sát", "Kéo cần điều khiển ở góc dưới bên trái để di chuyển Robo. Vuốt vùng trống bên phải màn hình để nhìn xung quanh. Bạn có thể dùng hai tay cùng lúc để vừa di chuyển, vừa quan sát đường đi."),
		new StoryPage("Thu thập năng lượng", "Hãy nhặt những viên năng lượng phát sáng trên đường đi. Thu thập đủ số viên được yêu cầu để qua màn. Hoàn thành cả bốn màn chơi sẽ giúp Robo có đủ năng lượng khởi động phi thuyền và rời khỏi hành tinh.", "core_icon"),
		new StoryPage("Bảo vệ Robo", "Robo bắt đầu mỗi màn chơi với 100 HP. Mỗi lần bị quái vật đánh trúng, Robo sẽ mất một phần máu; khi máu về 0, bạn sẽ thua. Bạn cũng sẽ thua ngay nếu Robo rơi khỏi đất liền, xuống axit hoặc dung nham, dù vẫn còn máu.", "robot_icon"),
		new StoryPage("Vật phẩm hỗ trợ", "Nhặt vật phẩm hồi máu để hồi 10 hoặc 20 HP, tùy loại. Nhặt khiên sẽ giúp Robo chặn đòn từ quái vật trong 5 giây. Tuy nhiên, khiên không thể bảo vệ Robo nếu rơi khỏi đất liền, xuống axit hoặc dung nham.", "support_items"),
	};

	// Extra pages for the main menu's "Hướng dẫn chơi" review.
	static readonly StoryPage[] GuideExtras = {
		new StoryPage("Chạy đua với thời gian", "Mỗi màn có một bộ đếm ngược ở góc trên bên trái. Hãy hoàn thành mục tiêu trước khi đồng hồ về 0; thời gian dài hơn ở các bản đồ rộng và khó hơn."),
		new StoryPage("Quái vật và boss", "Creep là quái nhỏ, di chuyển nhanh và đuổi Robo khi xuất hiện. Vệ binh là boss lớn, có vùng cảnh báo trước đòn đánh và tìm Robo dù ở xa. Ở màn cuối, chỉ một lần chạm vào boss cũng khiến Robo chết ngay.", "monsters"),
		new StoryPage("Hồi HP và khiên", "Dấu cộng màu xanh hồi HP cho Robo; khiên giúp chặn sát thương từ quái vật trong thời gian ngắn. Dấu trừ màu đỏ là cảnh báo mất HP, còn tia sét tím báo hiệu Robo đang bị giảm tốc.", "hp_plus"),
		new StoryPage("Vật phẩm nguy hiểm", "Hãy tránh ba loại vật phẩm gây hại trên đường đi. Chúng có hình giống viên năng lượng nhưng khác màu. Hóa chất độc hại màu đỏ sẽ làm Robo mất máu. Bẫy hút năng lượng màu tím hồng sẽ lấy đi 1 viên năng lượng bạn đã thu trong màn hiện tại. Bẫy giảm tốc màu cam sẽ làm Robo chậm lại 50% trong 5 giây. Khoảng 2/3 vật phẩm trên bản đồ là vật phẩm gây hại; hãy quan sát toàn bộ khu vực. Khiên chỉ chặn sát thương của hóa chất, không ngăn được hai hiệu ứng còn lại.", "harmful_items"),
		new StoryPage("Vật phẩm tăng tốc", "Từ màn thứ hai, bạn có thể gặp vật phẩm tăng tốc. Khi nhặt được, Robo sẽ di chuyển nhanh hơn trong 5 giây, giúp bạn thoát khỏi quái vật hoặc đến kịp những viên năng lượng ở xa.", "overdrive_icon"),
		new StoryPage("Âm thanh và tạm dừng", "Nút SFX hình loa ở góc trên bên phải bật hoặc tắt hiệu ứng âm thanh và âm thanh môi trường. Nút BGM hình nốt nhạc bật hoặc tắt riêng nhạc nền. Nút tạm dừng ngay bên cạnh sẽ dừng trò chơi; khi đó bạn có thể chọn Tiếp tục hoặc Thoát về menu. Âm lượng nhạc nền, hiệu ứng và âm thanh môi trường có thể chỉnh trong phần Cài đặt ở menu chính."),
	};

	public static readonly StoryPage BossIntro = new StoryPage("Vệ binh cuối cùng",
		"Đây là Vệ binh cuối cùng của bãi phóng. Boss tự tìm Robo trên toàn bản đồ, gây sát thương rất lớn và Robo sẽ chết ngay khi chạm vào nó. Hãy quan sát vùng cảnh báo màu đỏ, giữ khoảng cách và chỉ tiếp tục khi đã sẵn sàng.", "boss_icon");

	static readonly Dictionary<string, string> Leads = new Dictionary<string, string> {
		{ "Level1", "Robo tỉnh dậy trên một hòn đảo xa lạ, xung quanh là vùng axit nguy hiểm. Những viên năng lượng rải rác trên đảo là hy vọng đầu tiên để Robo tìm đường trở về." },
		{ "Level2", "Rời hòn đảo, Robo tìm đến một trạm khai thác đã bị bỏ hoang. Năng lượng vẫn còn nằm giữa những công trình cũ, nhưng quái vật cũng xuất hiện nhiều hơn trên đường đi." },
		{ "Level3", "Con đường tiếp theo đưa Robo vào một thung lũng phủ đầy dung nham. Một quái vật hộ vệ đang canh giữ nơi này và sẽ tấn công khi Robo đến gần." },
		{ "Level4", "Cuối cùng, Robo cũng đến được bãi phóng, nơi một chiếc phi thuyền đang chờ được khởi động. Chỉ còn thiếu một phần năng lượng nữa, nhưng quái vật hộ vệ cuối cùng vẫn đang chặn đường." },
	};

	static readonly Dictionary<string, string> Advice = new Dictionary<string, string> {
		{ "Level1", "Hãy cố gắng né quái vật và đừng để Robo rơi khỏi đất liền. Nếu cần nghỉ, hãy chạm nút tạm dừng ở góc trên bên phải màn hình." },
		{ "Level2", "Hãy quan sát xung quanh và tránh để quái vật bao vây. Nhặt vật phẩm tăng tốc sẽ giúp Robo di chuyển nhanh hơn trong 5 giây để thoát khỏi nguy hiểm." },
		{ "Level3", "Khi vùng đỏ xuất hiện dưới chân quái vật hộ vệ, hãy nhanh chóng di chuyển ra ngoài trước khi nó ra đòn. Bạn không cần đánh bại nó; hãy tập trung nhặt năng lượng và tránh rơi xuống dung nham." },
		{ "Level4", "Hãy tránh vùng đập màu đỏ; khi thấy một dải đỏ xuất hiện phía trước quái vật hộ vệ, hãy né sang bên để tránh cú lao của nó. Đừng để Robo rơi khỏi đất liền khi đã gần tới đích." },
	};

	static readonly Dictionary<string, string> Completions = new Dictionary<string, string> {
		{ "Level1", "Robo đã thu thập đủ năng lượng để tiếp tục hành trình. Hãy đến trạm khai thác bỏ hoang để tìm thêm những viên năng lượng còn thiếu." },
		{ "Level2", "Robo đã tìm được thêm năng lượng giữa những công trình cũ. Phía trước là thung lũng dung nham đầy nguy hiểm; hãy sẵn sàng cho chặng đường tiếp theo." },
		{ "Level3", "Robo đã vượt qua vùng dung nham và tiến gần hơn tới phi thuyền. Hãy đến bãi phóng, thu thập phần năng lượng cuối cùng và giúp Robo trở về." },
	};

	static string Get(Dictionary<string, string> table, LevelConfig level) {
		string text;
		return table.TryGetValue(level.levelId, out text) ? text : "";
	}

	/// <summary>"Thu thập đủ {N} viên năng lượng …" plus the level's advice (spec §7.4 templates).</summary>
	public static string Goal(LevelConfig level) {
		string goal = level.IsFinal
			? "Thu thập đủ " + level.energyTarget + " viên năng lượng để khởi động phi thuyền và hoàn thành hành trình."
			: "Thu thập đủ " + level.energyTarget + " viên năng lượng để qua màn.";
		int seconds = Mathf.CeilToInt(level.timeLimitSeconds > 0f ? level.timeLimitSeconds : 30f);
		return goal + " Bạn có " + FormatTime(seconds) + ". " + Get(Advice, level);
	}

	public static string FormatTime(int seconds) {
		int minutes = seconds / 60;
		int remainder = seconds % 60;
		return minutes > 0 ? minutes + " phút " + remainder.ToString("00") + " giây" : seconds + " giây";
	}

	/// <summary>The level's intro card: its name, the lead, then the goal.</summary>
	public static StoryPage LevelCard(LevelConfig level) {
		return new StoryPage(level.displayName, Get(Leads, level) + "\n\n" + Goal(level));
	}

	/// <summary>New Game tutorial: five pages, then the first level's card as page 6.</summary>
	public static StoryPage[] LevelCards(LevelConfig level) {
		if (level.IsFinal)
			return new[] { LevelCard(level), BossIntro };
		if (level.levelId != "Level2")
			return new[] { LevelCard(level) };
		return new[] { LevelCard(level), new StoryPage("Cẩn thận với vật phẩm nguy hiểm", "Từ khu vực này, hãy cẩn thận với Hóa chất độc hại, Bẫy hút năng lượng và Bẫy giảm tốc. Chúng có thể làm bạn mất máu, mất năng lượng đã thu hoặc di chuyển chậm lại. Những cụm năng lượng gần quái vật hoặc mép đất có thể giúp bạn thu thập nhanh hơn, nhưng hãy cân nhắc trước khi tiến tới.") };
	}

	public static StoryPage[] Tutorial(LevelConfig first) {
		var pages = new List<StoryPage>(Basics);
		pages.Add(LevelCard(first));
		return pages.ToArray();
	}

	/// <summary>Main menu "Hướng dẫn chơi": the basics plus speed boost, sound and pause buttons.</summary>
	public static StoryPage[] Guide() {
		var pages = new List<StoryPage>(Basics);
		pages.AddRange(GuideExtras);
		return pages.ToArray();
	}

	public static StoryPage LevelComplete(LevelConfig level) {
		return new StoryPage("Đã vượt qua " + level.displayName, Get(Completions, level));
	}

	public static string EnergyLabel(int collected, int target) {
		return "Năng lượng: " + System.Math.Max(0, System.Math.Min(collected, target)) + "/" + target;
	}
}
