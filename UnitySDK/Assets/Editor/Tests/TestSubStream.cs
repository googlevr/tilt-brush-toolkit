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

using System.Linq;
using System.IO;

using NUnit.Framework;
using Range = TiltBrushToolkit.GlbParser.Range;

namespace TiltBrushToolkit {

internal class TestSubStream {
  static Stream MakeStream(int n=25) {
    return new MemoryStream(Enumerable.Range(0, n).Select(i => (byte) i).ToArray());
  }

  static byte[] GetRange(Stream stream, Range range) {
    return GetRange(stream, range.start, range.length);
  }

  // If the stream only has n < length bytes left, returns a byte[n] instead of a byte[length].
  static byte[] GetRange(Stream stream, long start, long length) {
    int numDesired = (int)length;
    // Lazy way of handling a short reads.
    while (true) {
      stream.Position = start;
      var ret = new byte[numDesired];
      int numRead = stream.Read(ret, 0, ret.Length);
      if (numRead == ret.Length) {
        return ret;
      } else {
        numDesired = numRead;
      }
    }
  }

  [Test]
  public void TestSubStreamContents() {
    Stream baseStream = MakeStream();
    Range range = new Range { start = 10, length = 5 };
    Stream subStream = new SubStream(baseStream, range.start, range.length);
    var expected = new byte[] { 10, 11, 12, 13, 14 };
    Assert.AreEqual(expected, GetRange(baseStream, range));
    Assert.AreEqual(expected, GetRange(subStream, 0, range.length));
    Assert.AreEqual(expected, GetRange(subStream, 0, range.length + 1));
  }

  [Test]
  public void TestSubStreamSeekBeforeBeginning() {
    Stream baseStream = MakeStream();
    Stream subStream = new SubStream(baseStream, 10, 5);
    Assert.Catch(() => { subStream.Position = -1; });
  }

  [Test]
  public void TestSubStreamSeekAfterEnd() {
    Stream baseStream = MakeStream();
    Stream subStream = new SubStream(baseStream, 10, 5);
    Assert.AreEqual(new byte[0], GetRange(subStream, 5, 1));
    subStream.Position = 6;
    Assert.AreEqual(new byte[0], GetRange(subStream, 6, 1));
  }

  [Test]
  public void TestSubStreamOutOfRange() {
    Stream baseStream = MakeStream(25);
    Assert.Catch(() => new SubStream(baseStream, 23, 5));
    Assert.Catch(() => new SubStream(baseStream, -1, 5));
    Assert.Catch(() => new SubStream(baseStream, 23, -1));
  }
}

}
