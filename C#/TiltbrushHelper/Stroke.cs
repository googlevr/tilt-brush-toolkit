using System;

namespace TiltbrushToolkit
{
    public class Stroke
    {
        public int BrushIndex { get; set; }
        public float[] BrushColor { get; set; }
        public float BrushSize { get; set; }
        public ControlPoint[] ControlPoints { get; set; }
        public int StrokeMask { get; set; }
        public int CPMask { get; set; }
        public int Flags { get; set; }
        public float Scale { get; set; }
        public Stroke()
        {
            BrushColor = new float[4];
        }
    }
}
