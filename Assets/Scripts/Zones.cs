/// <summary>
/// Zone names of the levels, shown on the HUD and the intro card.
/// </summary>
public static class Zones {

	public static string Title(string sceneName) {
		switch (sceneName) {
			case "Level1": return "Bãi phế liệu";
			case "Level2": return "Khu công nghiệp bỏ hoang";
			case "Level3": return "Vùng hoang hóa";
			case "Level4": return "Trạm căn cứ";
			default: return "";
		}
	}
}
