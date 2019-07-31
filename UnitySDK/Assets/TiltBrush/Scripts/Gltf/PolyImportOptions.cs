// Copyright 2017 Google Inc. All rights reserved.
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

using UnityEngine;

namespace TiltBrushToolkit {
/// <summary>
/// Options that indicate how to import a given asset.
/// </summary>
[Serializable]
[AutoStringifiable]
public struct PolyImportOptions {
  public enum RescalingMode {
    // Convert the object's units to scene units and optionally apply a scale as well
    // (given by scaleFactor).
    CONVERT,
    // Scale the object such that it fits a box of a particular size (desiredSize).
    FIT,
  }

  /// <summary>
  /// What type of rescaling to perform.
  /// </summary>
  public RescalingMode rescalingMode;

  /// <summary>
  /// Scale factor to apply (in addition to unit conversion).
  /// Only relevant if rescalingMode==CONVERT.
  /// </summary>
  public float scaleFactor;

  /// <summary>
  /// The desired size of the bounding cube, if scaleMode==FIT.
  /// </summary>
  public float desiredSize;

  /// <summary>
  /// If true, recenters this object such that the center of its bounding box
  /// coincides with the center of the resulting GameObject (recommended).
  /// </summary>
  public bool recenter;

  /// <summary>
  /// If true, do not immediately perform heavy main thread operations (mesh import, texture creation,
  /// etc) on import. Rather, an enumerator will be returned (mainThreadThrottler) in PolyImportResult
  /// which you can enumerate to gradually create meshes and perform other heavy UI thread operations.
  /// This option is useful for performance-sensitive applications that want to be in full control of
  /// when Unity objects are created on the main thread.
  /// </summary>
  [HideInInspector]
  public bool clientThrottledMainThread;

  /// <summary>
  /// Returns a default set of import options.
  /// </summary>
  public static PolyImportOptions Default() {
    PolyImportOptions options = new PolyImportOptions();
    options.recenter = true;
    options.rescalingMode = RescalingMode.CONVERT;
    options.scaleFactor = 1.0f;
    options.desiredSize = 1.0f;
    return options;
  }

  public override string ToString() {
    return AutoStringify.Stringify(this);
  }
}
}
