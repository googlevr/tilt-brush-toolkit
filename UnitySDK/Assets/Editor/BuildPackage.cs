using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

static class BuildPackage {
  static string kVersionNormalLocation = "Assets/Editor/DummyVERSION.txt";
  static string kVersionBuildLocation = "Assets/TiltBrush/VERSION.txt";

  [System.Serializable()]
  public class BuildFailedException : System.Exception {
    public BuildFailedException(string fmt, params object[] args)
      : base(string.Format(fmt, args)) { }
  }

  /// Temporarily create a VERSION.txt build stamp so we can embed it
  /// in the unitypackage. Cleans up afterwards.
  /// Ensures that the VERSION.txt has a consistent GUID.
  class TempBuildStamp : System.IDisposable {
    byte[] m_previousContents;
    public TempBuildStamp(string contents) {
      string err = AssetDatabase.MoveAsset(
          kVersionNormalLocation, kVersionBuildLocation);
      if (err != "") {
        throw new BuildFailedException(
            "Couldn't move {0} to {1}: {2}",
            kVersionNormalLocation, kVersionBuildLocation, err);
      }
      m_previousContents = File.ReadAllBytes(kVersionBuildLocation);
      File.WriteAllText(kVersionBuildLocation, contents);
    }

    public void Dispose() {
      string err = AssetDatabase.MoveAsset(kVersionBuildLocation, kVersionNormalLocation);
      if (err == "" && m_previousContents != null) {
        File.WriteAllBytes(kVersionNormalLocation, m_previousContents);
      }
    }
  }

  static string GetGitVersion() {
    // Start the child process.
    var p = new System.Diagnostics.Process();
    // Redirect the output stream of the child process.
    p.StartInfo.UseShellExecute = false;
    p.StartInfo.RedirectStandardOutput = true;
    p.StartInfo.FileName = "git.exe";
    p.StartInfo.Arguments = "describe";
    p.Start();
    // Do not wait for the child process to exit before
    // reading to the end of its redirected stream.
    // p.WaitForExit();
    // Read the output stream first and then wait.
    var version = p.StandardOutput.ReadToEnd().Trim();
    p.WaitForExit();
    if (p.ExitCode != 0) {
      throw new BuildFailedException("git describe exited with code {0}", p.ExitCode);
    }
    return version;
  }

  [MenuItem("Tilt Brush/Build Package")]
  static void DoBuild() {
    string version = GetGitVersion();
    string name = string.Format("../../tiltbrushtoolkit-UnitySDK-{0}.unitypackage", version);

    using (var tmp = new TempBuildStamp(version)) {
      AssetDatabase.ExportPackage(
          new string[] {
            "Assets/ThirdParty",
            "Assets/TiltBrush",
            "Assets/TiltBrushExamples"
          },
          name,
          ExportPackageOptions.Recurse);
      Debug.LogFormat("Done building {0}", name);
    }
  }
}
