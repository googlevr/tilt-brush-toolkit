using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace TiltbrushToolkit
{
    internal static class Export
    {
        private class JsonHolder
        {
            public Stroke[] strokes { get; set; }
            public Brush[] brushes { get; set; }
        }
        private class Stroke
        {
            public string v { get; set; }
            public string n { get; set; }
            public string uv0 { get; set; }
            public string uv1 { get; set; }
            public string c { get; set; }
            public string t { get; set; }
            public string tri { get; set; }
            public int brush { get; set; }
        }
        private class Brush
        {
            public string name { get; set; }
            public Guid guid { get; set; }
        }
        private static float[] GetFlatFloatArray(string propName, string propValue, ref int numVerts)
        {
            float[] flatData = null;
            var data = Convert.FromBase64String(propValue);
            if (data.Length > 0)
            {
                int totalValues = data.Length / 4;
                using (MemoryStream ms = new MemoryStream(data))
                using (BinaryReader br = new BinaryReader(ms))
                {
                    flatData = new float[totalValues];
                    for (int i = 0; i < totalValues; i++)
                    {

                        flatData[i] = br.ReadSingle();
                    }
                }
                if (propName == "v")
                {
                    numVerts = flatData.Length / 3;
                }
                if (flatData.Length % numVerts != 0)
                {
                    throw new Exception();
                }
            }
            return flatData;
        }
        private static uint[] GetFlatUIntArray(string propName, string propValue, ref int numVerts)
        {
            uint[] flatData = null;
            var data = Convert.FromBase64String(propValue);
            if (data.Length > 0)
            {
                int totalValues = data.Length / 4;
                using (MemoryStream ms = new MemoryStream(data))
                using (BinaryReader br = new BinaryReader(ms))
                {
                    flatData = new uint[totalValues];
                    for (int i = 0; i < totalValues; i++)
                    {
                        flatData[i] = br.ReadUInt32();
                    }
                }
                if (propName == "v")
                {
                    numVerts = flatData.Length / 3;
                }
                if (flatData.Length % numVerts != 0)
                {
                    throw new Exception();
                }
            }
            else
            {
                throw new ArgumentNullException($"No value passed for {propName}");
            }
            return flatData;
        }
        private static int[] GetFlatIntArray(string propName, string propValue, ref int numVerts)
        {
            int[] flatData = null;
            var data = Convert.FromBase64String(propValue);
            if (data.Length > 0)
            {
                int totalValues = data.Length / 4;
                using (MemoryStream ms = new MemoryStream(data))
                using (BinaryReader br = new BinaryReader(ms))
                {
                    flatData = new int[totalValues];
                    for (int i = 0; i < totalValues; i++)
                    {
                        flatData[i] = br.ReadInt32();
                    }
                }
                numVerts = 3;
                if (flatData.Length % numVerts != 0)
                {
                    throw new Exception();
                }
            }
            else
            {
                throw new ArgumentNullException($"No value passed for {propName}");
            }
            return flatData;
        }
        private static Tuple<float, float, float>[] GetInfo(string propName, string propValue, ref int numVerts, int? expectedStride = null)
        {
            Tuple<float, float, float>[] value = null;
            if (!String.IsNullOrEmpty(propValue))
            {
                var flatData = GetFlatFloatArray(propName, propValue, ref numVerts);
                int strideWords = flatData.Length / numVerts;
                if (expectedStride.HasValue && expectedStride.Value != strideWords)
                {
                    throw new Exception();
                }
                value = new Tuple<float, float, float>[flatData.Length / strideWords];
                if (strideWords > 1)
                {
                    int count = 0;
                    for (int j = 0; j < flatData.Length; j += strideWords, count++)
                    {
                        Tuple<float, float, float> val = new Tuple<float, float, float>(flatData[j], flatData[j + 1], flatData[j + 2]);
                        value[count] = val;
                    }
                }
            }
            return value;
        }
        private static uint[] GetIntInfo(string propName, string propValue, ref int numVerts, int? expectedStride = null)
        {
            uint[] flatData = null;
            if (!String.IsNullOrEmpty(propValue))
            {
                flatData = GetFlatUIntArray(propName, propValue, ref numVerts);
                int strideWords = flatData.Length / numVerts;
                if (expectedStride.HasValue && expectedStride.Value != strideWords)
                {
                    throw new Exception();
                }
            }
            return flatData;
        }
        private static Tuple<float, float, float?, float?>[] Get4TupleInfo(string propName, string propValue, ref int numVerts, int? expectedStride = null)
        {
            Tuple<float, float, float?, float?>[] value = null;
            if (!String.IsNullOrEmpty(propValue))
            {
                float[] flatData = GetFlatFloatArray(propName, propValue, ref numVerts);

                int strideWords = flatData.Length / numVerts;
                if (expectedStride.HasValue && expectedStride.Value != strideWords)
                {
                    throw new Exception();
                }
                value = new Tuple<float, float, float?, float?>[flatData.Length / strideWords];
                if (strideWords > 1)
                {
                    int count = 0;
                    for (int j = 0; j < flatData.Length; j += strideWords, count++)
                    {
                        Tuple<float, float, float?, float?> val = new Tuple<float, float, float?, float?>(flatData[j], flatData[j + 1], strideWords > 2 ? flatData[j + 2] : (float?)null, strideWords > 3 ? flatData[j + 3] : (float?)null);
                        value[count] = val;
                    }
                }
            }
            return value;
        }
        private static Tuple<int, int, int>[] GetIntTupleInfo(string propName, string propValue, ref int numVerts, int? expectedStride = null)
        {
            Tuple<int, int, int>[] value = null;
            if (!String.IsNullOrEmpty(propValue))
            {
                int[] flatData = GetFlatIntArray(propName, propValue, ref numVerts);

                int strideWords = 3;
                if (expectedStride.HasValue && expectedStride.Value != strideWords)
                {
                    throw new Exception();
                }
                value = new Tuple<int, int, int>[flatData.Length / strideWords];
                if (strideWords > 1)
                {
                    int count = 0;
                    for (int j = 0; j < flatData.Length; j += strideWords, count++)
                    {
                        Tuple<int, int, int> val = new Tuple<int, int, int>(flatData[j], flatData[j + 1], flatData[j + 2]);
                        value[count] = val;
                    }
                }
            }
            return value;
        }
        /// <summary>
        /// returns an array of TiltBrushMesh instances, parsed from the .json export from Tilt Brush
        /// </summary>
        /// <param name="filePath">Path to .json file</param>
        /// <returns></returns>
        public static TiltBrushMesh[] FromFile(string filePath)
        {
            TiltBrushMesh[] meshes = null;
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                JavaScriptSerializer jsonSerializer = new JavaScriptSerializer();
                jsonSerializer.MaxJsonLength = Int32.MaxValue;
                JsonHolder items = jsonSerializer.Deserialize<JsonHolder>(json);
                meshes = new TiltBrushMesh[items.strokes.Length];
                int count = 0;
                foreach (var stroke in items.strokes)
                {
                    TiltBrushMesh mesh = new TiltBrushMesh();
                    var brush = items.brushes[stroke.brush];
                    mesh.BrushName = brush.name;
                    mesh.BrushGuid = brush.guid;
                    //num verts gets set when parsing the vertices information and is then used to parse the remainder of the information
                    int numVerts = 0;
                    mesh.v = GetInfo("v", stroke.v, ref numVerts);
                    mesh.n = GetInfo("n", stroke.n, ref numVerts);
                    mesh.uv0 = Get4TupleInfo("uv0", stroke.uv0, ref numVerts);
                    mesh.uv1 = Get4TupleInfo("uv1", stroke.uv1, ref numVerts);
                    mesh.c = GetIntInfo("c", stroke.c, ref numVerts);
                    mesh.t = Get4TupleInfo("t", stroke.t, ref numVerts);
                    mesh.tri = GetIntTupleInfo("tri", stroke.tri, ref numVerts);
                    meshes[count] = mesh;
                    count++;
                }
            }
            else
            {
                throw new FileNotFoundException("No file found at " + filePath);
            }
            return meshes;
        }
        /// <summary>
        /// Returns a single TiltBrushMesh instance created from all passed in
        /// </summary>
        /// <param name="meshes">Instances to merge</param>
        /// <returns></returns>
        public static TiltBrushMesh FromMeshes(TiltBrushMesh[] meshes)
        {
            TiltBrushMesh destinationMesh = new TiltBrushMesh();
            if (meshes.Length > 0)
            {
                destinationMesh.Name = meshes[0].Name;
                destinationMesh.BrushName = meshes[0].BrushName;
                destinationMesh.BrushGuid = meshes[0].BrushGuid;
                for(int i = 0; i < meshes.Length; i++)
                {
                    var mesh = meshes[i];
                    if(i == 0)
                    {
                        //if the first item in the list, instantiate the destination object properties with mesh properties
                        destinationMesh.v = mesh.v;
                        destinationMesh.n = mesh.n;
                        destinationMesh.uv0 = mesh.uv0;
                        destinationMesh.uv1 = mesh.uv1;
                        destinationMesh.c = mesh.c;
                        destinationMesh.t = mesh.t;
                        destinationMesh.tri = mesh.tri; 
                    }
                    else
                    {
                        int offset = destinationMesh.v.Length;
                        destinationMesh.v = destinationMesh.v.ExtendArray(mesh.v);
                        destinationMesh.n = destinationMesh.n.ExtendArray(mesh.n);
                        destinationMesh.uv0 = destinationMesh.uv0.ExtendArray(mesh.uv0);
                        if (destinationMesh.uv1 != null)
                        {
                            destinationMesh.uv1 = destinationMesh.uv1.ExtendArray(mesh.uv1);
                        }
                        destinationMesh.c = destinationMesh.c.ExtendArray(mesh.c);
                        destinationMesh.t = destinationMesh.t.ExtendArray(mesh.t);
                        destinationMesh.tri = destinationMesh.tri.ExtendArray(mesh.tri.Select(t=> new Tuple<int,int,int>(t.Item1 + offset,t.Item2 + offset, t.Item3 + offset)).ToArray());
                    }
                }
            }
            else
            {
                throw new ArgumentException("Empty array passed");
            }
            return destinationMesh;
        }

    }
}
