using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Audeu.EplanAi.AddIn.Serialization
{
    internal static class CompactJson
    {
        public static string Serialize<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
