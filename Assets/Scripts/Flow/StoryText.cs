using System.Collections.Generic;

/// <summary>One dialog page: a title and its full text.</summary>
public struct StoryPage {
	public string title;
	public string body;

	public StoryPage(string title, string body) {
		this.title = title;
		this.body = body;
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
		new StoryPage("Thu thập năng lượng", "Hãy nhặt những viên năng lượng phát sáng trên đường đi. Thu thập đủ số viên được yêu cầu để qua màn. Hoàn thành cả bốn màn chơi sẽ giúp Robo có đủ năng lượng khởi động phi thuyền và rời khỏi hành tinh."),
		new StoryPage("Bảo vệ Robo", "Robo bắt đầu mỗi màn chơi với 100 HP. Mỗi lần bị quái vật đánh trúng, Robo sẽ mất một phần máu; khi máu về 0, bạn sẽ thua. Bạn cũng sẽ thua ngay nếu Robo rơi khỏi đất liền, xuống axit hoặc dung nham, dù vẫn còn máu."),
		new StoryPage("Vật phẩm hỗ trợ", "Nhặt vật phẩm hồi máu để hồi 10 hoặc 20 HP, tùy loại. Nhặt khiên sẽ giúp Robo chặn đòn từ quái vật trong 5 giây. Tuy nhiên, khiên không thể bảo vệ Robo nếu rơi khỏi đất liền, xuống axit hoặc dung nham."),
	};

	// Extra pages for the main menu's "Hướng dẫn chơi" review.
	static readonly StoryPage[] GuideExtras = {
		new StoryPage("Vật phẩm tăng tốc", "Từ màn thứ hai, bạn có thể gặp vật phẩm tăng tốc. Khi nhặt được, Robo sẽ di chuyển nhanh hơn trong 5 giây, giúp bạn thoát khỏi quái vật hoặc đến kịp những viên năng lượng ở xa."),
		new StoryPage("Âm thanh và tạm dừng", "Nút loa ở góc trên bên phải giúp bật hoặc tắt hiệu ứng âm thanh. Nút tạm dừng ngay bên cạnh sẽ dừng trò chơi; khi đó bạn có thể chọn Tiếp tục hoặc Thoát về menu. Âm lượng nhạc nền, hiệu ứng và âm thanh môi trường có thể chỉnh trong phần Cài đặt ở menu chính."),
	};

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
		return goal + " " + Get(Advice, level);
	}

	/// <summary>The level's intro card: its name, the lead, then the goal.</summary>
	public static StoryPage LevelCard(LevelConfig level) {
		return new StoryPage(level.displayName, Get(Leads, level) + "\n\n" + Goal(level));
	}

	/// <summary>New Game tutorial: five pages, then the first level's card as page 6.</summary>
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
		return "Năng lượng: " + collected + "/" + target;
	}
}
