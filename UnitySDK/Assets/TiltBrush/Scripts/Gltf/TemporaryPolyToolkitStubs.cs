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

using System;
using System.Collections.Generic;

namespace TiltBrushToolkit {

public class PolyFormat {
  public List<PolyFile> resources { get { return null; } }
}

public class PolyFile {
  public string relativePath { get { return null; } }
  public byte[] contents { get { return null; } }
}

public static class PtDebug {
  public static void LogVerboseFormat(params object[] args) {}
}

public class AutoStringifiable : Attribute {}

public class AutoStringify : Attribute {
  public static string Stringify(object o) { return null; }
}

public struct LengthWithUnit {
  public float ToMeters() { return 0; }
}

}
