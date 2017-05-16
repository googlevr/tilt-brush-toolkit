using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace TiltBrushToolkit {

internal class TestFbx {
  [Test]
  public void TestVersionLess() {
    Assert.IsTrue(Version.Parse("10.0") < Version.Parse("10.1"));
    Assert.IsTrue(Version.Parse("10.1") < Version.Parse("11.0"));
    Assert.IsFalse(Version.Parse("10.1") < Version.Parse("10.0"));
    Assert.IsFalse(Version.Parse("11.0") < Version.Parse("10.1"));
    Assert.IsFalse(Version.Parse("11.0b") < Version.Parse("11.0"));
    Assert.IsFalse(Version.Parse("11.0") < Version.Parse("11.0b"));
  }

  [Test]
  public void TestFbxUserPropertiesAscii() {
    var file = string.Format(
        "{0}/Editor/Tests/{1}-fbx.bytes", UnityEngine.Application.dataPath, "v10-text");
    var dict = new Dictionary<string, string>();
    foreach (var pair in FbxUtils.IterUserPropertiesAscii(file)) {
      dict[pair.Key] = pair.Value;
    }
    Assert.IsTrue(dict.ContainsKey("SrcDocumentUrl"));
    Assert.AreEqual("v10comma,text.fbx", dict["SrcDocumentUrl"]);
  }

  [TestCase("v10-bin", "10.0", "10.0")]
  [TestCase("v10-text", "10.0", "10.0")]
  public void TestFbxHeaderParse(string sketch, string tbVersion, string toolkitVersion) {

    var file = string.Format(
        "{0}/Editor/Tests/{1}-fbx.bytes", UnityEngine.Application.dataPath, sketch);

    var info = FbxUtils.GetTiltBrushFbxInfo(file, force: true);

    Assert.AreEqual(tbVersion,                info.tiltBrushVersion.ToString());
    Assert.AreEqual(Version.Parse(tbVersion), info.tiltBrushVersion);

    if (toolkitVersion == null) {
      Assert.AreEqual(null, info.requiredToolkitVersion);
    } else {
      Assert.AreEqual(toolkitVersion,                info.requiredToolkitVersion.ToString());
      Assert.AreEqual(Version.Parse(toolkitVersion), info.requiredToolkitVersion);
    }
  }
}

}
