using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Swagger.Json.Utils.Models
{
    public class PathItem
    {
        [JsonProperty("$ref")]
        public string @ref;

        public Operation get;

        public Operation put;

        public Operation post;

        public Operation delete;

        public Operation options;

        public Operation head;

        public Operation patch;

        public IList<JObject> parameters;

        public Dictionary<string, object> vendorExtensions = new Dictionary<string, object>();
    }
}