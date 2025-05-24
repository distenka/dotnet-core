using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Newtonsoft.Json.Converters;

namespace Distenka.Client
{
    public enum RunLogType { stdout, stderr }
    public class RunLog
    {
        public long Id { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public RunLogType Type { get; set; }
        public int Index { get; set; }
        public string Value { get; set; }
    }
    public class RunLogInfo
    {
        public int Size { get; set; }
        public string DownloadUrl { get; set; }
    }
}
