using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace jsonparser
{
    public class SwaggerDocument
    {
        public readonly string swagger = "2.0";

        public JObject info;

        public string host;

        public string basePath;

        public IList<string> schemes;

        public IList<string> consumes;

        public IList<string> produces;

        public JObject paths;

        public IDictionary<string, JObject> definitions;

        public IDictionary<string, JObject> parameters;

        public IDictionary<string, JObject> responses;

        public IDictionary<string, JObject> securityDefinitions;

        public IList<IDictionary<string, IEnumerable<string>>> security;

        public IList<JObject> tags;

        public JObject externalDocs;

        public Dictionary<string, object> vendorExtensions = new Dictionary<string, object>();
    }
}