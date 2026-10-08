using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildScript {

	[MenuItem("Tools/Robo Lac Loi/Build Linux")]
	public static void BuildLinux() {
		Build(BuildTarget.StandaloneLinux64, "Builds/Linux/RoboLacLoi.x86_64", BuildOptions.None);
	}

	// Development APK for profiling (Profiler connection, script debugging).
	[MenuItem("Tools/Robo Lac Loi/Build Android (development)")]
	public static void BuildAndroidDevelopment() {
		ConfigureAndroid();
		Build(BuildTarget.Android, "Builds/Android/RoboLacLoi-dev.apk",
			BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.ConnectWithProfiler);
	}

	// APK for play testing.
	[MenuItem("Tools/Robo Lac Loi/Build Android (test)")]
	public static void BuildAndroidTest() {
		ConfigureAndroid();
		Build(BuildTarget.Android, "Builds/Android/RoboLacLoi.apk", BuildOptions.None);
	}

	// Spec §7/§12: landscape, ARM64 + IL2CPP, Android 9+ (API 28).
	static void ConfigureAndroid() {
		PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
		PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
		PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel28;
		PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
		PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
		PlayerSettings.allowedAutorotateToPortrait = false;
		PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
		PlayerSettings.allowedAutorotateToLandscapeLeft = true;
		PlayerSettings.allowedAutorotateToLandscapeRight = true;
		// Signing comes from the environment so no keystore path or password is committed (spec §14.3):
		// ANDROID_KEYSTORE, ANDROID_KEYSTORE_PASS, ANDROID_KEY_ALIAS, ANDROID_KEY_PASS. Without them Unity signs with
		// ~/.android/debug.keystore. (The project used to point at the original author's private keystore.)
		string keystore = System.Environment.GetEnvironmentVariable("ANDROID_KEYSTORE");
		PlayerSettings.Android.useCustomKeystore = !string.IsNullOrEmpty(keystore);
		PlayerSettings.Android.keystoreName = keystore ?? "";
		PlayerSettings.Android.keystorePass = System.Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS") ?? "";
		PlayerSettings.Android.keyaliasName = System.Environment.GetEnvironmentVariable("ANDROID_KEY_ALIAS") ?? "";
		PlayerSettings.Android.keyaliasPass = System.Environment.GetEnvironmentVariable("ANDROID_KEY_PASS") ?? "";
	}

	// Keeps local keystore paths out of ProjectSettings.asset (they are set from the environment per build).
	static void ClearSigning() {
		PlayerSettings.Android.useCustomKeystore = false;
		PlayerSettings.Android.keystoreName = "";
		PlayerSettings.Android.keystorePass = "";
		PlayerSettings.Android.keyaliasName = "";
		PlayerSettings.Android.keyaliasPass = "";
		AssetDatabase.SaveAssets();
	}

	static void Build(BuildTarget target, string path, BuildOptions options) {
		// Without a graphics device (-nographics) shaders are not compiled and the player renders all magenta.
		if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) {
			Debug.LogError("BuildScript: run the build without -nographics, shaders cannot be compiled");
			EditorApplication.Exit(1);
			return;
		}
		string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
		BuildReport report = BuildPipeline.BuildPlayer(scenes, path, target, options);
		if (target == BuildTarget.Android)
			ClearSigning();
		Debug.Log("BuildScript: " + report.summary.result + " " + report.summary.totalSize + " bytes -> " + path);
		if (report.summary.result != BuildResult.Succeeded)
			EditorApplication.Exit(1);
	}
}
