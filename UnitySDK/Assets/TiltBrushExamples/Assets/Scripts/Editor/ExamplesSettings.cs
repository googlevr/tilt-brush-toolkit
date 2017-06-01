// Copyright 2016 Google Inc.
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
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TiltBrushToolkit {

  [InitializeOnLoad]
  public class ExamplesSettings : EditorWindow {
    struct Define {
      public string symbol;
      public string ns;
    }
    static readonly Define[] kDefines = new[] {
      new Define { symbol = "TILTBRUSH_STEAMVRPRESENT", ns = "Valve.VR" },
      new Define { symbol = "TILTBRUSH_CINEMADIRECTORPRESENT", ns = "CinemaDirector" },
    };

    static ExamplesSettings() {
      EditorApplication.projectWindowChanged += OnProjectWindowChanged;
      OnProjectWindowChanged();
    }

    static void OnProjectWindowChanged() {
      foreach (var define in kDefines) {
        DefineSymbol(define.symbol, NamespaceExists(define.ns));
      }
    }

    static bool NamespaceExists(string Namespace) {
      foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies()) {
        foreach(var t in assembly.GetTypes()) {
          if (t.Namespace == Namespace) {
            return true;
          }
        }
      }
      return false;
    }

    static void DefineSymbol(string symbol, bool active = false) {
      List<string> symbols = new List<string>();
      string tmp = PlayerSettings.GetScriptingDefineSymbolsForGroup(
          BuildTargetGroup.Standalone);
      if (! string.IsNullOrEmpty(tmp)) {
        symbols.AddRange(tmp.Split(';'));
      }

      bool present = symbols.Contains(symbol);
      if (present != active) {
        symbols = symbols.Where(s => s != "" && s != symbol).ToList();
        if (active) {
          symbols.Add(symbol);
        }
        Debug.LogFormat("{0} scripting define {1}", active ? "Adding" : "Removing", symbol);
        PlayerSettings.SetScriptingDefineSymbolsForGroup(
            BuildTargetGroup.Standalone,
            string.Join(";", symbols.ToArray()));
      }
    }
  }
}
