using System;

namespace TiltbrushToolkit
{
    public class Sketch
    {
        public string FileName { get; set; }
        public Header HeaderInfo { get; set; }
        public Stroke[] Strokes { get; set; }
        public Sketch()
        {
            HeaderInfo = new Header();
        }
    }
}
