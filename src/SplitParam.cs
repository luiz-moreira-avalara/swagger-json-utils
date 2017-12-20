using System;
using Newtonsoft.Json.Linq;

namespace Swagger.Json.Utils
{
    public class SplitParam
    {
        public SplitParam()
        {
            Remove = new string[0];
            RemoveAllBut = new string[0];
        }

        public string Name { get; set; }
        public string[] Remove { get; internal set; }
        public string[] RemoveAllBut { get; internal set; }
        public Action<JToken> Resolver { get; set; }
    }
}