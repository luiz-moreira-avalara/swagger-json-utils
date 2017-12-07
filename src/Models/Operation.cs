using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Swagger.Json.Utils.Models
{
    public class Operation
    {
        public IList<string> tags;

        private string _summary;

        public string summary
        {
            get { return $"{operationId} - {_summary}"; }
            set { _summary = value; }
        }

        public string description;

        public JObject externalDocs;

        public string operationId;

        public IList<string> consumes;

        public IList<string> produces;

        public IList<JObject> parameters;

        public IDictionary<string, JObject> responses;

        public IList<string> schemes;

        public bool? deprecated;

        public IList<IDictionary<string, IEnumerable<string>>> security;

        public Dictionary<string, object> vendorExtensions = new Dictionary<string, object>();
    }
}