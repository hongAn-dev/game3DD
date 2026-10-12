using System.IO;
using UnityEditor.Android;

/// <summary>Allows Gradle to package this Windows workspace whose parent folder contains Vietnamese characters.</summary>
public sealed class AndroidGradlePathSetup : IPostGenerateGradleAndroidProject {
    public int callbackOrder { get { return 0; } }

    public void OnPostGenerateGradleAndroidProject(string path) {
        // Unity passes the unityLibrary module; the properties belong to its root Gradle project.
        string project = File.Exists(Path.Combine(path, "settings.gradle")) ? path : Directory.GetParent(path).FullName;
        string properties = Path.Combine(project, "gradle.properties");
        string contents = File.Exists(properties) ? File.ReadAllText(properties) : "";
        const string flag = "android.overridePathCheck=true";
        if (!contents.Contains(flag)) File.WriteAllText(properties, contents.TrimEnd() + "\n" + flag + "\n");
    }
}
