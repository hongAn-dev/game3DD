using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// App identity: name "Robo Lạc Lối" and the icon/logo drawn by Tools/ui/make_app_icon.py (default icon for every
/// platform; Android adaptive background + foreground layers, round and legacy icons). Re-runnable.
/// </summary>
public static class AppIconSetup {

	public const string ProductName = "Robo Lạc Lối";
	const string Folder = "Assets/ThirdParty/RoboLacLoi/Icons/";

	[MenuItem("Tools/Robo Lac Loi/Apply App Icon")]
	public static void Apply() {
		Texture2D full = Icon("app_icon"), background = Icon("icon_background"), foreground = Icon("icon_foreground");
		PlayerSettings.productName = ProductName;
		PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { full });
		foreach (PlatformIconKind kind in new[] { AndroidPlatformIconKind.Adaptive, AndroidPlatformIconKind.Round, AndroidPlatformIconKind.Legacy }) {
			PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(BuildTargetGroup.Android, kind);
			foreach (PlatformIcon icon in icons) {
				if (kind == AndroidPlatformIconKind.Adaptive) {
					icon.SetTexture(background, 0);
					icon.SetTexture(foreground, 1);
				} else {
					icon.SetTexture(full);
				}
			}
			PlayerSettings.SetPlatformIcons(BuildTargetGroup.Android, kind, icons);
		}
		AssetDatabase.SaveAssets();
		Debug.Log("AppIconSetup: done");
	}

	static Texture2D Icon(string name) {
		string path = Folder + name + ".png";
		TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
		importer.textureType = TextureImporterType.Default;
		importer.alphaIsTransparency = true;
		importer.mipmapEnabled = false;
		importer.textureCompression = TextureImporterCompression.Uncompressed;
		importer.npotScale = TextureImporterNPOTScale.None;
		importer.SaveAndReimport();
		return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
	}
}
