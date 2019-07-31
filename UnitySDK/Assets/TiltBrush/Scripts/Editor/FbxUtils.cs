// Copyright 2017 Google Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TiltBrushToolkit {

public class FbxError : Exception {
  public FbxError(string message) : base(message) {}
}

/// Simple and limited fbx reading code - we only need enough to read the 'Tilt Brush' property.
/// FBX format ref found here. Note that it's _not_ correct.
///   https://code.blender.org/2013/08/fbx-binary-file-format-specification/
/// This reference is better:
///   https://github.com/ideasman42/pyfbx_i42/blob/master/pyfbx/parse_bin.py
/// The correct definition is:
///
/// FBX : FILE_HEADER NODE_LIST
/// FILE_HEADER : "Kaydara FBX Binary  " 0x00 0x1a 0x00 int32<version>
/// NODE_LIST : NODE+ NULLNODE
/// NODE : NODE_HEADER NODE_PROPERTIES NODE_LIST?
///   presence of NODE_LIST is detected by examining header.end
/// NODE_HEADER :
///   uint32 end           absolute offset of node end
///   uint32 props_count   number of properties in property list
///   uint32 props_bytes   number of bytes in property list
///   uint8-length-prefixed string
/// NODE_PROPERTIES :
///   props_bytes bytes of properties (format not documented here)
/// NULLNODE : 0x00 * 13
public static class FbxUtils {
  const int kNodeHeaderSize = 13;
  static readonly string[] kFbxUserPropertiesPath =
      new[] {"FBXHeaderExtension", "SceneInfo", "Properties70"};
  const string kFbxUserPropertiesPath2 =
      "FBXHeaderExtension/CreationTimeStamp/SceneInfo/MetaData/Properties70";

  public struct FbxInfo {
    public bool isFbx;
    /// null if not created by Tilt Brush, or version not found
    public Version? tiltBrushVersion;
    /// null if "RequiredToolkitVersion" not found
    public Version? requiredToolkitVersion;
  }

  static Version? FromDict(Dictionary<string, string> dict, string key) {
    string value;
    if (! dict.TryGetValue(key, out value)) { return null; }
    try {
      return Version.Parse(value);
    } catch (ArgumentException) {
      return null;
    }
  }

  /// Given a path to an arbitrary file, return some info about that file.
  public static FbxInfo GetTiltBrushFbxInfo(string path, bool force=false) {
    FbxInfo info = new FbxInfo();
    if (force || path.ToLowerInvariant().EndsWith(".fbx")) {
      IEnumerable<KeyValuePair<string, string>> propsIter = null;
      if (IsBinaryFbx(path)) {
        propsIter = IterUserPropertiesBinary(path);
      } else if (IsAsciiFbx(path)) {
        propsIter = IterUserPropertiesAscii(path);
      }

      if (propsIter != null) {
        info.isFbx = true;
        var props = new Dictionary<string, string>();
        try {
          foreach (var pair in propsIter) {
            props[pair.Key] = pair.Value;
          }
        } catch (FbxError) {
          // Can't find any properties
        }

        string name;
        if (props.TryGetValue("Original|ApplicationName", out name) && name == "Tilt Brush") {
          info.tiltBrushVersion = FromDict(
              props, "Original|ApplicationVersion");
        }
        info.requiredToolkitVersion = FromDict(
            props, "Original|RequiredToolkitVersion");
      }
    }
    return info;
  }

  //
  // Binary FBX support
  //

  /// Returns true if the file might be a binary-format FBX
  static bool IsBinaryFbx(string path) {
    try {
      using (var file = new FileStream(path, FileMode.Open, FileAccess.Read))
      using (var reader = new BinaryReader(file)) {
        return ReadHeader(reader);
      }
    } catch (Exception) {
      return false;
    }
  }

  /// Returns true if the header was read properly and looks like a binary fbx
  static bool ReadHeader(BinaryReader reader) {
    string firstTwenty = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(20));
    if ((firstTwenty != "Kaydara FBX Binary  ")
        || (reader.ReadByte() != 0x00)
        || (reader.ReadByte() != 0x1a)
        || (reader.ReadByte() != 0x00)) {
      return false;
    }
    reader.ReadUInt32(); // Version - unneeded
    return true;
  }

  struct NodeHeader {
    public uint endOffset;       // absolute
    public uint propertyCount;   // number of properties
    public uint propertyBytes;   // number of bytes in property list
    public string name;
    public bool IsNull { get { return endOffset == 0; } }
  }

  static string ReadUint8String(BinaryReader reader) {
    int len = reader.ReadByte(); // read the name length
    return System.Text.Encoding.ASCII.GetString(reader.ReadBytes(len));
  }

  static NodeHeader ReadNodeHeader(BinaryReader reader) {
    return new NodeHeader {
      endOffset = reader.ReadUInt32(),
      propertyCount = reader.ReadUInt32(),
      propertyBytes = reader.ReadUInt32(),
      name = ReadUint8String(reader)
    };
  }

  // Returns header of child node of the given name and positions reader at the end
  // of the child node's header.
  // On error, throws FbxError and reader is undefined.
  static NodeHeader FindChildNodeBinary(BinaryReader reader, string name) {
    while (true) {
      NodeHeader child = ReadNodeHeader(reader);
      if (child.name == name) {
        return child;
      } else if (child.IsNull) {
        throw new FbxError(name);
      }
      reader.BaseStream.Seek(child.endOffset, SeekOrigin.Begin);
    }
  }

  // Returns all properties that can be parsed.
  // Leaves reader in undefined position (because maybe not all could be parsed).
  // Most of the time, it should be positioned at the beginning of the NODE_LIST.
  static List<string> ReadAllProperties(BinaryReader reader, NodeHeader header) {
    var props = new List<string>();
    for (int i = 0; i < header.propertyCount; ++i) {
      char type = reader.ReadChar();
      // Don't understand anything but strings
      if (type != 'S') { break; }
      int length = reader.ReadInt32();
      if (length < 0) { break; }
      props.Add(System.Text.Encoding.ASCII.GetString(reader.ReadBytes(length)));
    }
    return props;
  }

  /// Throws FbxError if user properties can't be found
  static IEnumerable<KeyValuePair<string, string>>
      IterUserPropertiesBinary(string path) {
    using (var file = new FileStream(path, FileMode.Open, FileAccess.Read))
    using (var reader = new BinaryReader(file)) {
      if (! ReadHeader(reader)) {
        yield break;
      }

      NodeHeader header = new NodeHeader();
      foreach (string name in kFbxUserPropertiesPath) {
        header = FindChildNodeBinary(reader, name);
        // Skip over the properties to get to the NODE_LIST
        reader.BaseStream.Seek(header.propertyBytes, SeekOrigin.Current);
      }

      // A user-property is actually a child node with N properties.
      // The 1st property is the user-property name
      // The Nth property is the user-property value
      while (true) {
        NodeHeader propNode = ReadNodeHeader(reader);
        if (propNode.IsNull) {
          // NODE_LIST is null-terminated
          break;
        }
        // This indicates a user-property node
        UnityEngine.Debug.Assert(propNode.name == "P");
        List<string> props = ReadAllProperties(reader, propNode);
        // Skip any child nodes or properties that ReadAllProperties wasn't able to parse
        reader.BaseStream.Seek(propNode.endOffset, SeekOrigin.Begin);
        yield return new KeyValuePair<string, string>(props[0], props[props.Count-1]);
      }
    }
  }

  //
  // Ascii FBX support
  //

  /// Returns true if the file might be an ASCII-format FBX
  static bool IsAsciiFbx(string path) {
    using (var file = new FileStream(path, FileMode.Open, FileAccess.Read))
    using (var reader = new StreamReader(file)) {
      return reader.ReadLine().StartsWith("; FBX");
    }
  }

  // Positions reader after the opening line of the requested child node.
  // On error, throws FbxError and position of reader is undefined.
  static void FindChildNodeAscii(StreamReader reader, string name) {
    // Open-curly with a prefixed name
    Regex startRgx = new Regex(@"^\s*([^:]+):.*{");
    // Close-curly
    Regex endRgx = new Regex(@"^\s*}");
    int depth = 0;
    while (true) {
      string line = reader.ReadLine();
      if (line == null) {
        throw new FbxError(name);
      }

      if (startRgx.Match(line).Success) {
        depth += 1;
        if (depth == 1) {
          string matched = startRgx.Match(line).Groups[1].Value;
          if (matched == name) {
            return;
          }
        }
      } else if (endRgx.Match(line).Success) {
        depth -= 1;
        if (depth < 0) {
          throw new FbxError(name);
        }
      }
    }
  }

  /// Throws FbxError if user properties can't be found
  static public IEnumerable<KeyValuePair<string, string>>
      IterUserPropertiesAscii(string path) {
    using (var file = new FileStream(path, FileMode.Open, FileAccess.Read))
    using (var reader = new StreamReader(file)) {
      if (!reader.ReadLine().StartsWith(";")) {
        yield break;
      }

      foreach (string name in kFbxUserPropertiesPath) {
        FindChildNodeAscii(reader, name);
      }

      // Now at the P: section.
      // We only care about pure-string properties, so we can assume everything is in ""
      // Fbx uses &quot; to escape double-quotes, eg
      //   P: "Original|ApplicationVendor", "KString", "", "", "&quot;Google'"
      // We use this rather complicated regex to parse because the strings may contain ","
      Regex propertyRgx = new Regex(@"^\s*P: ""(?<first>[^""]*)""(, ""(?<rest>[^""]*)"")+");
      Regex endRgx = new Regex(@"^\s*}");
      while (true) {
        string line = reader.ReadLine();
        if (line == null) {
          yield break;
        }
        Match match = endRgx.Match(line);
        if (match.Success) {
          yield break;
        }
        match = propertyRgx.Match(line);
        if (match.Success) {
          string key = match.Groups["first"].Value;
          IEnumerable<Capture> captures = match.Groups["rest"].Captures.Cast<Capture>();
          string value = captures.Select(c => c.Value).Last();
          yield return new KeyValuePair<string, string>(key, value);
        } else {
          // Probably some non-string property -- ignore
        }
      }
    }
  }
}

}
