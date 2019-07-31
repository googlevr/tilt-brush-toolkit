// Copyright 2019 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

// Use of deprecated "WWW"
#pragma warning disable 0618

using System.IO;
using UnityEngine;

using NUnit.Framework;

namespace TiltBrushToolkit {

internal class TestGlbParser {
  // Files in glTF-Sample-Models are too vague about their licenses for us to distribute them,
  // so pull them as-needed.
  static string FetchToTempFile(string uri, string uniqueName) {
    string fullDest = Path.Combine(Application.temporaryCachePath, uniqueName);
    if (!File.Exists(fullDest)) {
      using (var www = new WWW(uri)) {
        while (!www.isDone) {
          System.Threading.Thread.Sleep(50);
        }
        File.WriteAllBytes(fullDest, www.bytes);
      }
    }
    return fullDest;
  }

  static string GetGlb1File() {
    return FetchToTempFile(
        "http://github.com/KhronosGroup/glTF-Sample-Models/blob/master/1.0/Box/glTF-Binary/Box.glb?raw=true",
        "Box.glb1");
  }

  static string GetGlb2File() {
    return FetchToTempFile(
        "http://github.com/KhronosGroup/glTF-Sample-Models/blob/master/2.0/Box/glTF-Binary/Box.glb?raw=true",
        "Box.glb2");
  }

  [Test]
  public void TestV1Json() {
    var file = GetGlb1File();
    GlbParser.GetJsonChunkAsString(file);
  }

  [Test]
  public void TestV1Bin() {
    var file = GetGlb1File();
    GlbParser.GetBinChunk(file);
  }

  [Test]
  public void TestV2Json() {
    var file = GetGlb2File();
    GlbParser.GetJsonChunkAsString(file);
  }

  [Test]
  public void TestV2Bin() {
    var file = GetGlb2File();
    GlbParser.GetBinChunk(file);
  }
}

}
