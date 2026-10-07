using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildScript {

	[MenuItem("Tools/Robo Lac Loi/Build Linux")]
	public static void BuildLinux() {
		// Without a graphics device (-nographics) shaders are not compiled and the player renders all magenta.
		if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) {
			Debug.LogError("BuildScript: run the build without -nographics, shaders cannot be compiled");
			EditorApplication.Exit(1);
			return;
		}
		string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
		BuildReport report = BuildPipeline.BuildPlayer(scenes, "Builds/Linux/RoboLacLoi.x86_64",
			BuildTarget.StandaloneLinux64, BuildOptions.None);
		Debug.Log("BuildScript: " + report.summary.result + " " + report.summary.totalSize + " bytes");
		if (report.summary.result != BuildResult.Succeeded)
			EditorApplication.Exit(1);
	}
}
