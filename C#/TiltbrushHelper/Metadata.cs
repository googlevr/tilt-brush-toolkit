using System;
using System.Web.Script.Serialization;
using TiltbrushToolkit.Exceptions;

namespace TiltbrushToolkit
{
    public class Metadata
    {
        //not everything contained in this class due to limitations of parsing JSON through standard .NET JavaScript Serializer class
        public Guid EnvironmentPreset { get; set; }
        public string[] Authors { get; set; }

        public int SchemaVersion = 1;
        public Guid[] BrushIndex { get; set; }
        public string ToJSON()
        {
            var obj = new
            {
                EnvironmentPreset = EnvironmentPreset,
                Authors = Authors,
                BrushIndex = BrushIndex,
                SchemaVersion = SchemaVersion
            };
            JavaScriptSerializer serialize = new JavaScriptSerializer();
            return serialize.Serialize(obj);
        }
        public static Metadata FromJson(string json)
        {
            try
            {
                JavaScriptSerializer serialize = new JavaScriptSerializer();
                var metaData = serialize.Deserialize<Metadata>(json);
                return metaData;
            }
            catch(Exception ex)
            {
                throw new BadMetadataException(ex.Message);
            }
        }
    }
}
