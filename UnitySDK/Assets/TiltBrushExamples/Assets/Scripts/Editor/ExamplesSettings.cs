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
using System.Collections;
using System.Reflection;

namespace TiltBrushToolkit {

  [InitializeOnLoad]
  public class ExamplesSettings : EditorWindow {

    const string DEFINE_STEAM = "TILTBRUSH_STEAMVRPRESENT";
    const string DEFINE_CINEMADIRECTOR = "TILTBRUSH_CINEMADIRECTORPRESENT";

    static bool m_SteamPresent = false;
    static bool m_CinemaDirectorPresent = false;

    static ExamplesSettings() {
      EditorApplication.projectWindowChanged += OnProjectWindowChanged;

      EnsureSymbols();
    }

    static void OnProjectWindowChanged() {
      ClearSymbols ();
    }
    static void ClearSymbols() {
      EnsureSymbol(DEFINE_STEAM, false);
      EnsureSymbol(DEFINE_CINEMADIRECTOR, false);
    }
    static void EnsureSymbols() {
      m_SteamPresent = NamespaceExists("Valve.VR");
      EnsureSymbol(DEFINE_STEAM, m_SteamPresent);
      m_CinemaDirectorPresent = NamespaceExists("CinemaDirector");
      EnsureSymbol(DEFINE_CINEMADIRECTOR, m_CinemaDirectorPresent);
    }

    static bool NamespaceExists(string Namespace) {
      foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies()) {
        foreach(var t in assembly.GetTypes()) {
          if (t.Namespace == Namespace)
            return true;
        }
      }
      return false;
    }

    static bool HasSymbol(string Symbol) {
      var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
      return !string.IsNullOrEmpty(symbols) && symbols.Contains(Symbol);
    }
    static void EnsureSymbol(string Symbol, bool Active = false) {
      var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
      bool present = HasSymbol(Symbol);
      if (present && !Active) {
        symbols = symbols.Remove(symbols.IndexOf(Symbol), Symbol.Length); // TODO Remove ; too
      } else if (!present && Active) {
        if (symbols.Length > 0)
          symbols += ";";
        symbols += Symbol + ";";
      }
      PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, symbols);
      
    }

  }
}