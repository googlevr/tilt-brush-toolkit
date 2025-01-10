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

using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor.PackageManager;
using Object = UnityEngine.Object;
using System.IO;

namespace TiltBrushToolkit {

public class EditorUtils {

  #region Tilt Menu

  [MenuItem("Tilt Brush/Labs/Split mesh by stroke")]
  public static void MeshToStrokes()
  {
    GameObject[] selected = Selection.gameObjects;

    if (selected == null || selected.Length == 0)
    {
      return;
    }

    Undo.IncrementCurrentGroup();
    Undo.SetCurrentGroupName("Split mesh by stroke");

    AssetDatabase.StartAssetEditing();

    if (!AssetDatabase.IsValidFolder($"Assets/{m_SplitMeshByStrokeDirectoryName}"))
    {
      AssetDatabase.CreateFolder("Assets",$"{m_SplitMeshByStrokeDirectoryName}");
    }

    bool cancel = false;
    try
    {
      foreach (var obj in selected)
      {
        try
        {
          MeshFilter mf = obj.GetComponent<MeshFilter>();
          if (mf == null) throw new MissingComponentException($"Missing MeshFilter on {obj.name}");

          MeshRenderer mr = mf.GetComponent<MeshRenderer>();
          if (mr == null) throw new MissingComponentException($"Missing MeshRenderer on {obj.name}");

          cancel = MeshToStroke(mf);
          if (cancel)
          {
            Undo.RevertAllInCurrentGroup();
            break;
          }

          Undo.DestroyObjectImmediate(mf);
          Undo.DestroyObjectImmediate(mr);
        }
        catch (MissingComponentException ex)
        {
          Debug.LogError($"Error processing object {obj.name}: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
          Debug.LogError($"Error processing object {obj.name}: {ex.Message}");
        }
      }
    }
    finally // since we call AssetDatabase.StartAssetEditing(), we must also call AssetDatabase.StopAssetEditing()
    {
      EditorUtility.ClearProgressBar();
      AssetDatabase.StopAssetEditing();
    }
  }

  // separate a mesh into strokes by timestamp in UV2
  private static bool MeshToStroke(MeshFilter meshFilter)
  {
      GameObject go = meshFilter.gameObject;
      Mesh mesh = meshFilter.sharedMesh;
      var uv3 = new List<Vector3>();
      mesh.GetUVs(2,uv3);

      bool cancel = false;

      if (uv3.Count == 0)
      {
        throw new InvalidOperationException($"Mesh ({mesh.name}) has no timestamps. Make sure the sketch was exported from Open Brush with ExportStrokeTimestamp = true");
      }
      else
      {

        List<float> strokeIDs = new List<float>();

        // is using float safe as a key?
        // for our use case, yes.
        // if we were calculating the key by e.g adding two floats together, that'd be a problem
        Dictionary<float, int> vertexCounts = new Dictionary<float, int>();

        // we create the strokeGameObjects already here, because later (when we create the meshes) we parent them to 'go'
        // but if we call SetParent for each one here right after it has been instantiated
        // then in the next iteration, GameObject.Instantiate(go,..) will also clone the children that were parented
        List<GameObject> strokeGameObjects = new List<GameObject>();

        string baseNameForStrokeGameObject = go.name;
        Undo.RecordObject(go,"Split mesh by stroke");
        go.name += " (separated)";

        // explanation of the following 6 lines:
        // - the new meshes that get created are parented to 'go', which holds the mesh originally
        // - we reset go's local transform
        // - and we copy go's transform to the new mesh gameobjects (this is done later in the code, when GetMeshSubset is used)
        // This ensures that nothing changes visually when the tool is used
        // The other approach would be to apply the transform to the mesh data, but I think it's better to not modify the mesh data
        // as it's saved as an asset
        Vector3 originalScale = go.transform.localScale;
        Vector3 originalPosition = go.transform.localPosition;
        Quaternion originalRotation = go.transform.localRotation;

        go.transform.localScale = new Vector3(1, 1, 1);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        for (int i = 0; i < mesh.vertexCount; i++)
        {

          if (!vertexCounts.ContainsKey(uv3[i].x))
          {
            vertexCounts.Add(uv3[i].x,1);
            strokeIDs.Add(uv3[i].x);
            GameObject strokeGameObject = GameObject.Instantiate(go);
            Undo.RegisterCreatedObjectUndo(strokeGameObject, "Split mesh by stroke");
            strokeGameObjects.Add(strokeGameObject);
          }
          else
          {
            vertexCounts[uv3[i].x]++;
          }

        }



        /*
         * We use 3 jobs to set up the data needed for creating the new meshes.
         *
         * - Jobs 1 and 2 create data that Job 3 needs
         *
         * - All jobs are IParallelFor, and Execute(index) runs for the index-th stroke
         *
         * Jobs:
         * 1. The first job will calculate how many triangles each new mesh will have.
         *
         * 2. The second job will create a 1d array that maps each vertex in OriginalMesh.vertices
         *    to that vertex's index in the new mesh's vertex array.
         *
         * 3. The third job populates a 1d array that holds all the new triangle arrays in continuous memory. (There are no Native 2d arrays)
         *  - This job depends on (2), because when creating the new triangles arrays, the .triangles array must reference indices in the new vertex array
         *  - This job depends on (1), because it needs to know the length of each strokes triangle array, in order
         *    to calculate the starting index for a stroke's triangle array.
         */


        NativeArray<int> triangleCounts = new NativeArray<int>(strokeIDs.Count, Allocator.Persistent);
        NativeArray<int> originalTriangles = new NativeArray<int>(mesh.triangles, Allocator.Persistent);
        NativeArray<float> nativeStrokeIDs = new NativeArray<float>(strokeIDs.ToArray(), Allocator.Persistent);
        NativeArray<Vector3> nativeUV3 = new NativeArray<Vector3>(uv3.ToArray(), Allocator.Persistent);

        var job1 = new TriangleCountJob()
        {
          triangles = originalTriangles,
          triangleCounts = triangleCounts,
          uv3 = nativeUV3,
          strokeIDs = nativeStrokeIDs
        };

        var job1Handle = job1.Schedule(strokeIDs.Count, 1);

        NativeArray<int> vertexMap = new NativeArray<int>(mesh.vertexCount, Allocator.Persistent);

        var job2 = new VertexMapJob()
        {
          vertexMap = vertexMap,
          uv3 = nativeUV3,
          strokeIDs = nativeStrokeIDs
        };

        var job2Handle = job2.Schedule(strokeIDs.Count, 1);

        NativeArray<int> allNewTriangles = new NativeArray<int>(mesh.triangles.Length, Allocator.Persistent);

        var job3 = new TriangleArrayCopyJob()
        {
          uv3 = nativeUV3,
          strokeIDs = nativeStrokeIDs,
          originalTriangles = originalTriangles,
          triangleCounts = triangleCounts,
          vertexMap = vertexMap,
          triangles = allNewTriangles,
        }.Schedule(strokeIDs.Count, 1, JobHandle.CombineDependencies(job1Handle,job2Handle));

        JobHandle.CompleteAll(ref job1Handle, ref job2Handle);
        job3.Complete();

        // allNewTriangles and triangleCounts will be disposed after mesh generation
        nativeUV3.Dispose();
        nativeStrokeIDs.Dispose();
        originalTriangles.Dispose();
        vertexMap.Dispose();

        const int PROGRESS_FREQUENCY = 1;
        int count = 0;

        int strokeIndex = 0;
        foreach (float strokeId in strokeIDs)
        {
          if (++count > PROGRESS_FREQUENCY) {
            count = 0;
            cancel = EditorUtility.DisplayCancelableProgressBar("Creating strokes", "Processing " + mesh.name, (float)strokeIndex / (float)strokeIDs.Count);
            if (cancel) break;
          }

          GameObject strokeGameObject = strokeGameObjects[strokeIndex];

          // These 3 lines ensure that nothing changes visually when the tool is used
          strokeGameObject.transform.localPosition = originalPosition;
          strokeGameObject.transform.localScale = originalScale;
          strokeGameObject.transform.localRotation = originalRotation;

          strokeGameObject.transform.SetParent(go.transform,false);
          strokeGameObject.name =  $"{baseNameForStrokeGameObject} ({strokeIndex})";

          int startingIndexInTrianglesArray = 0;
          int startingIndexInVerticesArray = 0;
          int triangleCount = triangleCounts[strokeIndex];
          int vertexCount = vertexCounts[strokeId];

          for (int i = 0; i < strokeIndex; i++)
          {
            float strokeId_ = strokeIDs[i];
            startingIndexInTrianglesArray += triangleCounts[i];
            startingIndexInVerticesArray += vertexCounts[strokeId_];
          }

          int[] trianglesForStroke = new int[triangleCount];
          Vector3[] vertices = new Vector3[vertexCount];
          Vector2[] uv = new Vector2[vertexCount];
          Vector3[] normals = new Vector3[vertexCount];
          Color[] colors = new Color[vertexCount];
          Vector3[] uv2 = new Vector3[vertexCount];
          Vector3[] uv3_subset = new Vector3[vertexCount];

          List<Vector3> uv2_full = new List<Vector3>();
          mesh.GetUVs(1,uv2_full);

          NativeArray<int>.Copy(allNewTriangles, startingIndexInTrianglesArray, trianglesForStroke, 0, triangleCount);

          CopyMeshAttribute(mesh.vertices,vertices,startingIndexInVerticesArray,vertexCount);

          CopyMeshAttribute(mesh.uv, uv, startingIndexInVerticesArray, vertexCount);

          CopyMeshAttribute(mesh.normals, normals, startingIndexInVerticesArray, vertexCount);

          CopyMeshAttribute(mesh.colors, colors, startingIndexInVerticesArray, vertexCount);

          CopyMeshAttribute(uv2_full.ToArray(), uv2, startingIndexInVerticesArray, vertexCount);

          CopyMeshAttribute(uv3.ToArray(), uv3_subset, startingIndexInVerticesArray, vertexCount);

          var newMesh_ = GetMeshSubset(mesh,trianglesForStroke,vertices, uv, uv2,uv3_subset,normals, colors, strokeIndex);

          strokeGameObject.GetComponent<MeshFilter>().mesh = newMesh_;
          strokeIndex++;
        }

        allNewTriangles.Dispose();
        triangleCounts.Dispose();
      }

      return cancel;
  }

  // copy elements from sourceArray to destinationArray starting at startingIndexInSource,
  // making sure that bounds aren't exceeded
  // The purpose of this function is because e.g Dots stroke has uv2, but OilPaint stroke doesn't.
  private static void CopyMeshAttribute<T>(
    T[] sourceArray,
    T[] destinationArray,
    int startingIndexInSource,
    int vertexCount)
  {
    if (sourceArray != null && sourceArray.Length >= startingIndexInSource + vertexCount)
    {
      Array.Copy(sourceArray, startingIndexInSource, destinationArray, 0, vertexCount);
    }
  }

  [MenuItem("Tilt Brush/Labs/Split mesh by color")]
  public static void ExplodeSketchByColor() {
    if (!EditorUtility.DisplayDialog ("Different Strokes", "Separate brush strokes of different colors into separate objects? \n* Note: This is an experimental feature!", "OK", "Cancel"))
      return;
    Undo.IncrementCurrentGroup();
    Undo.SetCurrentGroupName("Split mesh by color");
    List<GameObject> newSelection = new List<GameObject>();
    bool cancel = false;

    var tri = new int[3] { 0, 0, 0 };

    foreach (var o in Selection.gameObjects) {
      if (cancel) break;
      var obj = GameObject.Instantiate(o, o.transform.position, o.transform.rotation) as GameObject;
      obj.name = o.name + " (Separated)";
      Undo.RegisterCreatedObjectUndo(obj, "Split mesh by color");
      newSelection.Add(obj);
      int count = 0;

      foreach (var m in obj.GetComponentsInChildren<MeshFilter>()) {
        if (cancel) break;
        var mesh = m.sharedMesh;
        var meshColors = mesh.colors;

        // Keep a list of triangles for each color (as a vector) we find
        Dictionary<Vector3, List<int>> colors = new Dictionary<Vector3, List<int>>();

        var triangles = mesh.triangles;
        const int PROGRESS_FREQUENCY = 600;

        for (int i = 0; i < triangles.Length; i += 3) {
          if (++count > PROGRESS_FREQUENCY) {
            count = 0;
            cancel = EditorUtility.DisplayCancelableProgressBar("Separating sketch", "Processing " + mesh.name, (float)i / (float)triangles.Length);
            if (cancel) break;
          }

          tri[0] = triangles[i];
          tri[1] = triangles[i + 1];
          tri[2] = triangles[i + 2];

          // Get the triangle's average color
          var color = GetTriangleColorVec(meshColors, tri);

          // Add the triangle to the triangle-by-color list
          List<int> trianglesForColor;
          if (!colors.TryGetValue(color, out trianglesForColor))
            trianglesForColor = colors[color] = new List<int>();
          trianglesForColor.AddRange(tri);
        }

        if (cancel)
          break;

        // make a new mesh for each color
        int colorIndex = 0;
        count = 0;
        foreach (var color in colors.Keys) {
          if (++count > PROGRESS_FREQUENCY) {
            count = 0;
            cancel = EditorUtility.DisplayCancelableProgressBar("Separating sketch", "Processing " + mesh.name, (float)colorIndex / (float)colors.Keys.Count);
            if (cancel) break;
          }
          // Clone the gameobject with the mesh
          var newObj = GameObject.Instantiate(m.gameObject, m.transform.position, m.transform.rotation) as GameObject;
          newObj.name = string.Format("{0} {1}", m.name, colorIndex);
          newObj.transform.SetParent(m.transform.parent, true);
          Undo.RegisterCreatedObjectUndo(newObj, "Split mesh by color");

          // get the subset of triangles for this color and make a new mesh out of it. TODO: only use the vertices used by the triangles
          var newMesh = GetMeshSubset(mesh, colors[color].ToArray());
          newObj.GetComponent<MeshFilter>().mesh = newMesh;
          colorIndex++;
        }
        Undo.DestroyObjectImmediate(m.gameObject);
      }
      // Delete the original object?
      Undo.DestroyObjectImmediate(o);
    }
    if (!cancel) {
      // Select the newly created objects
      Selection.objects = newSelection.ToArray();
    } else {
      Undo.RevertAllInCurrentGroup();
    }
    EditorUtility.ClearProgressBar();
  }

  [MenuItem("Tilt Brush/Labs/Split mesh by color", true)]
  public static bool ExplodeSketchByColorValidate() {
    // TODO: validate that selection is a model
    foreach (var o in Selection.gameObjects) {
      if (o.GetComponent<MeshFilter>() != null)
        return true;
      if (o.GetComponentsInChildren<MeshFilter>().Length > 0)
        return true;
    }
    return false;
  }

  /// <summary>
  /// Gets the average color of a triangle and returns it as a vector for easier comparison
  /// </summary>
  public static Vector3 GetTriangleColorVec(Color[] meshColors, int[] Triangle) {
    Vector3 v = new Vector3();
    for (int i = 0; i < Triangle.Length; i++) {
      var c = meshColors[Triangle[i]];
      v.x += c.r;
      v.y += c.g;
      v.z += c.b;
    }
    v /= Triangle.Length;
    return v;
  }

  public static Mesh GetMeshSubset(Mesh OriginalMesh, int[] Triangles, Vector3[] vertices = null, Vector2[] uv = null, Vector3[] uv2 =  null,
    Vector3[] uv3 = null,
    Vector3[] normals = null,
    Color[] colors = null, int index = 0) {
    Mesh newMesh = new Mesh();
    newMesh.name = OriginalMesh.name;
    newMesh.vertices = vertices ?? OriginalMesh.vertices;
    newMesh.triangles = Triangles;
    newMesh.uv = uv ?? OriginalMesh.uv;
    if (uv3 != null)
    {
      newMesh.SetUVs(2,uv3);
    }
    if (uv2 != null)
    {
      newMesh.SetUVs(1,uv2);
    }
    else
    {
      newMesh.uv2 = OriginalMesh.uv2;
    }
    newMesh.colors = colors ?? OriginalMesh.colors;
    newMesh.subMeshCount = OriginalMesh.subMeshCount;
    newMesh.normals = normals ?? OriginalMesh.normals;
    string assetName = AssetDatabase.GenerateUniqueAssetPath($"Assets/{m_SplitMeshByStrokeDirectoryName}/{OriginalMesh.name}_submesh[{index}].asset");
    AssetDatabase.CreateAsset(newMesh, assetName);
    return newMesh;
  }



  #endregion

  public static string m_SplitMeshByStrokeDirectoryName = "SplitMeshByStroke";
  public static string m_TiltBrushDirectoryName = "TiltBrush";
  static string m_TiltBrushDirectory = "";
  public static string TiltBrushDirectory {
    get {
      if (string.IsNullOrEmpty (m_TiltBrushDirectory)) {
        foreach (var s in AssetDatabase.FindAssets ("EditorUtils")) {
          var path = AssetDatabase.GUIDToAssetPath (s);
          if (!path.Contains (m_TiltBrushDirectoryName))
            continue;
          m_TiltBrushDirectory = path.Substring (0, path.IndexOf (m_TiltBrushDirectoryName) + m_TiltBrushDirectoryName.Length);
        }
      }
      if (string.IsNullOrEmpty (m_TiltBrushDirectory))
        Debug.LogErrorFormat ("Could not find the TiltBrush directory. Reimport the Tilt Brush Unity SDK to ensure it's unmodified.");
      return m_TiltBrushDirectory;
    }
  }

  /// <summary>
  ///  Takes a texture (usually with height 1) and stretches it into single line height for debugging
  /// </summary>
  public static void LayoutCustomLabel(string Label, int FontSize = 11, FontStyle Style = FontStyle.Normal, TextAnchor Anchor = TextAnchor.MiddleLeft) {
    var gs = new GUIStyle(GUI.skin.label);
    gs.fontStyle = Style;
    gs.fontSize = FontSize;
    gs.alignment = Anchor;
    gs.richText = true;
    EditorGUILayout.LabelField(Label, gs);
  }

  /// <summary>
  ///  Takes a texture (usually with height 1) and stretches it into single line height for debugging
  /// </summary>
  public static void LayoutTexture(string Label, Texture2D Texture) {
    EditorGUILayout.LabelField(Label);
    var r = EditorGUILayout.BeginVertical(GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 2));
    EditorGUILayout.Space();
    GUI.DrawTexture(r, Texture, ScaleMode.StretchToFill, true);
    //EditorGUI.DrawTextureTransparent(r,t.WaveFormTexture, ScaleMode.StretchToFill);
    EditorGUILayout.EndVertical();
  }

  public static void LayoutBar(string Label, float Value, Color Color) {
    Value = Mathf.Clamp01(Value);
    EditorGUILayout.Space();
    var r = EditorGUILayout.BeginHorizontal(GUILayout.MinHeight(EditorGUIUtility.singleLineHeight));
    EditorGUILayout.Space();
    EditorGUI.LabelField(new Rect(r.x, r.y, r.width * 0.5f, r.height), Label);
    DrawBar(new Rect(r.x + r.width * .5f, r.y, r.width * .5f, r.height), Value, Color);
    EditorGUILayout.EndHorizontal();
  }

  public static void LayoutBarVec4(string Label, Vector4 Value, Color Color, bool ClampTo01 = true) {
    Value.x = ClampTo01 ? Mathf.Clamp01(Value.x) : Value.x % 1f;
    Value.y = ClampTo01 ? Mathf.Clamp01(Value.y) : Value.y % 1f;
    Value.z = ClampTo01 ? Mathf.Clamp01(Value.z) : Value.z % 1f;
    Value.w = ClampTo01 ? Mathf.Clamp01(Value.w) : Value.w % 1f;
    EditorGUILayout.Space();
    var r = EditorGUILayout.BeginHorizontal(GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 2f));
    EditorGUILayout.Space();
    var gs = new GUIStyle(GUI.skin.label);
    gs.alignment = TextAnchor.UpperRight;
    EditorGUI.LabelField(new Rect(r.x, r.y, r.width * 0.5f, r.height), Label + " ", gs);
    DrawBar(new Rect(r.x + r.width * .5f, r.y + (r.height / 4f) * 0, r.width * .5f, r.height / 4f - 2), Value.x, Color);
    DrawBar(new Rect(r.x + r.width * .5f, r.y + (r.height / 4f) * 1, r.width * .5f, r.height / 4f - 2), Value.y, Color);
    DrawBar(new Rect(r.x + r.width * .5f, r.y + (r.height / 4f) * 2, r.width * .5f, r.height / 4f - 2), Value.z, Color);
    DrawBar(new Rect(r.x + r.width * .5f, r.y + (r.height / 4f) * 3, r.width * .5f, r.height / 4f - 2), Value.w, Color);
    EditorGUILayout.EndHorizontal();
  }

  static void DrawBar(Rect r, float Value, Color Color) {
    EditorGUI.DrawRect(r, new Color(0.7f, 0.7f, 0.7f));
    EditorGUI.DrawRect(new Rect(r.x, r.y, r.width * Value, r.height), Color * new Color(0.8f, 0.8f, 0.8f));
  }

  public static bool IconButton(Rect Rect, Texture2D Texture, Color Color, string Tooltip = "") {
    var c = GUI.color;
    GUI.color = Color;
    var gs = new GUIStyle(GUI.skin.label);
    gs.alignment = TextAnchor.MiddleCenter;
    bool result = GUI.Button(Rect, new GUIContent(Texture, Tooltip), gs);
    GUI.color = c;
    return result;
  }

  public static GameObject[] GetFramesFromFolder(Object FolderAsset) {

    if (!(FolderAsset is UnityEditor.DefaultAsset))
      return null;

    var frames = new List<GameObject> ();

    string sAssetFolderPath = AssetDatabase.GetAssetPath(FolderAsset);
    string sDataPath  = Application.dataPath;
    string sFolderPath = sDataPath.Substring(0 ,sDataPath.Length-6)+sAssetFolderPath;

    GrabObjectsAtDirectory (sFolderPath, ref frames); // get files in top directory
    RecursiveSearch (sFolderPath, ref frames); // go through subdirectories
    GameObject[] array = frames.ToArray ();
    System.Array.Sort (array, new AlphanumericComparer ());
    return array;

  }

  public static void GrabObjectsAtDirectory(string sDir, ref List<GameObject> List) {
    foreach (string f in Directory.GetFiles(sDir))
    {
      var frame =  AssetDatabase.LoadAssetAtPath<GameObject>(f.Substring(Application.dataPath.Length-6));
      if (frame != null)
        List.Add (frame);
    }
  }
  public static void RecursiveSearch(string sDir, ref List<GameObject> List)
  {
    try
    {
      foreach (string d in Directory.GetDirectories(sDir))
      {
        GrabObjectsAtDirectory(d, ref List);
        RecursiveSearch(d, ref List);
      }
    }
    catch (System.Exception excpt)
    {
      Debug.LogError(excpt.Message);
    }
  }

  private static string RemoveWhitespace(string String) {
    return string.Join("", String.Split(default(string[]), System.StringSplitOptions.RemoveEmptyEntries));
  }

}

public class AlphanumericComparer : IComparer<Object> {
  public int Compare(Object x, Object y) {
    return string.Compare (Pad (x.name), Pad (y.name));
  }

  string Pad(string Input) {
    // turn ABC10 into ABC000000000010 so it gets sorted as ABC1,ABC2,ABC10 instead of ABC1,ABC10,ABC2
    return System.Text.RegularExpressions.Regex.Replace (Input, "[0-9]+", match => match.Value.PadLeft (10, '0'));
  }
}

}  // namespace TiltBrushToolkit