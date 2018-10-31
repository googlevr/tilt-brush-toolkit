using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using TiltbrushToolkit.Exceptions;

namespace TiltbrushToolkit
{
    public class TiltBrushFile
    {
        public Metadata MetadataInformation { get; set; }
        public Sketch SketchInformation { get; set; }
        public TiltBrushFile()
        {
            MetadataInformation = new Metadata();
            SketchInformation = new Sketch();
        }
        private static void ValidateMetadata(Metadata data)
        {
            if(data.BrushIndex.Any(b=>b == Guid.Empty))
            {
                throw new BadMetadataException("Invalid brush index passed");
            }
            if(data.EnvironmentPreset == Guid.Empty)
            {
                throw new BadMetadataException("Invalid environment preset");
            }
            if(data.Authors.Any(a=>String.IsNullOrEmpty(a)))
            {
                throw new BadMetadataException("Invalid author names");
            }
        }
        /// <summary>
        /// Creates a TiltBrushFile instance from passed in file
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static TiltBrushFile ParseFromFile(string filename)
        {
            if (filename.IndexOf(".tilt") == -1)
            {
                throw new ArgumentException("Process can only convert .tilt files");
            }
            ConvertBetweenDirectoryAndZip(filename);
           
            try
            {
                //fill out meta data from json file
                string metaDataJson = File.ReadAllText(filename + @"\metadata.json");
                Metadata data = Metadata.FromJson(metaDataJson);
                //ensure that minimum details have been passed
                ValidateMetadata(data);
                //fill out sketch data from .sketch file
                TiltBrushFile file = new TiltBrushFile();
                file.MetadataInformation = data;
                using (FileStream fs = new FileStream(filename + @"\data.sketch", FileMode.Open))
                using (BinaryReader br = new BinaryReader(fs, Encoding.UTF8))
                {
                    file.SketchInformation.HeaderInfo.Sentinal = br.ReadUInt32();
                    file.SketchInformation.HeaderInfo.Version = br.ReadInt32();
                    file.SketchInformation.HeaderInfo.Unused = br.ReadInt32();

                    //additional header length
                    int length = br.ReadInt32();
                    //will probably need to skip this length but for now assume it is empty
                    var additionalHeader = br.ReadBytes(length);
                    var totalStrokes = br.ReadInt32();
                    if (totalStrokes < 300000)
                    {
                        file.SketchInformation.Strokes = new Stroke[totalStrokes];
                        for (int i = 0; i < totalStrokes; i++)
                        {
                            file.SketchInformation.Strokes[i] = new Stroke();
                            file.SketchInformation.Strokes[i].BrushIndex = br.ReadInt32();
                            file.SketchInformation.Strokes[i].BrushColor[0] = br.ReadSingle();
                            file.SketchInformation.Strokes[i].BrushColor[1] = br.ReadSingle();
                            file.SketchInformation.Strokes[i].BrushColor[2] = br.ReadSingle();
                            file.SketchInformation.Strokes[i].BrushColor[3] = br.ReadSingle();
                            file.SketchInformation.Strokes[i].BrushSize = br.ReadSingle();
                            file.SketchInformation.Strokes[i].StrokeMask = br.ReadInt32();
                            file.SketchInformation.Strokes[i].CPMask = br.ReadInt32();
                            file.SketchInformation.Strokes[i].Flags = br.ReadInt32();
                            if (file.SketchInformation.Strokes[i].Flags != 0)
                            {
                                file.SketchInformation.Strokes[i].Scale = br.ReadSingle();
                            }
                            var totalPoints = br.ReadInt32();
                            if (totalPoints < 10000)
                            {
                                file.SketchInformation.Strokes[i].ControlPoints = new ControlPoint[totalPoints];
                                for (var j = 0; j < totalPoints; j++)
                                {
                                    file.SketchInformation.Strokes[i].ControlPoints[j] = new ControlPoint();
                                    file.SketchInformation.Strokes[i].ControlPoints[j].Position[0] = br.ReadSingle();
                                    file.SketchInformation.Strokes[i].ControlPoints[j].Position[1] = br.ReadSingle();
                                    file.SketchInformation.Strokes[i].ControlPoints[j].Position[2] = br.ReadSingle();
                                    file.SketchInformation.Strokes[i].ControlPoints[j].Orientation[0] = br.ReadSingle();
                                    file.SketchInformation.Strokes[i].ControlPoints[j].Orientation[1] = br.ReadSingle();
                                    file.SketchInformation.Strokes[i].ControlPoints[j].Orientation[2] = br.ReadSingle();
                                    file.SketchInformation.Strokes[i].ControlPoints[j].Orientation[3] = br.ReadSingle();
                                    file.SketchInformation.Strokes[i].ControlPoints[j].Extension.TriggerPressure = br.ReadSingle();
                                    file.SketchInformation.Strokes[i].ControlPoints[j].Extension.Timestamp = br.ReadInt32();
                                }
                            }
                            else
                            {
                                throw new BadTiltException("Unable to parse: Too many control points present");
                            }
                        }
                    }
                    else
                    {
                        throw new BadTiltException("Unable to parse: Too many strokes present");
                    }
                }
                return file;
            }
            finally
            {
                //always ensure the file gets converted back from directory
                ConvertBetweenDirectoryAndZip(filename);
            }
        }
        /// <summary>
        /// Writes current TiltBrush file details to disk at specified path
        /// </summary>
        /// <param name="destinationpath">Path to write file to</param>
        /// <param name="thumbnailpath">Optional path to thumbnail to use</param>
        public void ConvertToTiltFile(string destinationpath, string thumbnailpath = null)
        {
            if (Directory.Exists(destinationpath))
            {
                Directory.Delete(destinationpath);
            }
            var info = Directory.CreateDirectory(destinationpath);
            //write the metadata.json file
            File.WriteAllText(destinationpath + @"\metadata.json", MetadataInformation.ToJSON());
            if (String.IsNullOrEmpty(thumbnailpath))
            {
                //use a solid black image as the thumbnail if none provided
                using (Bitmap b = new Bitmap(50, 50))
                {
                    using (Graphics g = Graphics.FromImage(b))
                    {
                        g.Clear(Color.Black);
                    }
                    b.Save(destinationpath + @"\thumbnail.png", ImageFormat.Png);
                }
            }
            else
            {
                File.Copy(thumbnailpath, destinationpath + @"\thumbnail.png");
            }

            using (FileStream fs = new FileStream(destinationpath + @"\data.sketch", FileMode.OpenOrCreate))
            using (BinaryWriter bw = new BinaryWriter(fs, Encoding.UTF8))
            {
                bw.Write(SketchInformation.HeaderInfo.Sentinal);
                bw.Write(SketchInformation.HeaderInfo.Version);
                bw.Write(SketchInformation.HeaderInfo.Unused);
                //write the additional header
                bw.Write(0);
                bw.Write(SketchInformation.Strokes.Length);
                foreach (var item in SketchInformation.Strokes)
                {
                    if (item != null)
                    {
                        bw.Write(item.BrushIndex);
                        bw.Write(item.BrushColor[0]);
                        bw.Write(item.BrushColor[1]);
                        bw.Write(item.BrushColor[2]);
                        bw.Write(item.BrushColor[3]);
                        bw.Write(item.BrushSize);
                        bw.Write(item.StrokeMask);
                        bw.Write(item.CPMask);
                        bw.Write(item.Flags);
                        if (item.Flags != 0)
                        {
                            bw.Write(item.Scale);
                        }
                        bw.Write(item.ControlPoints.Length);
                        foreach (var point in item.ControlPoints)
                        {
                            if (point != null)
                            {
                                bw.Write(point.Position[0]);
                                bw.Write(point.Position[1]);
                                bw.Write(point.Position[2]);
                                bw.Write(point.Orientation[0]);
                                bw.Write(point.Orientation[1]);
                                bw.Write(point.Orientation[2]);
                                bw.Write(point.Orientation[3]);
                                bw.Write(point.Extension.TriggerPressure);
                                bw.Write(point.Extension.Timestamp);
                            }
                        }
                    }
                }
            }

            ConvertBetweenDirectoryAndZip(destinationpath);
        }
        public static void ConvertBetweenDirectoryAndZip(string filename)
        {
            FileAttributes attr = File.GetAttributes(filename);

            if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
            {
                UnpackTilt.ConvertDirectoryToZip(filename);
            }
            else
            {
                UnpackTilt.ConvertZipToDirectory(filename);
            }
        }
    }
}
