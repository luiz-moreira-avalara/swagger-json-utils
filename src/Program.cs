using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
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

    internal class Program
    {
        private static readonly SplitParam[] _splitParams =
        {
            new SplitParam
            {
                Name = "paths",
                RemoveAllBut = new[] {"/compliance/documento-fiscal"}
            },
            new SplitParam
            {
                Name = "parameters"
            },
            new SplitParam
            {
                Name = "definitions",
                Remove = new[] {"definitions"},
                Resolver = CircularReferenceResolver
            },
            new SplitParam
            {
                Name = "responses"
            }
        };

        private static Dictionary<string, IList<string>> _circularReferenceLookup;

        private static void Main(string[] args)
        {
            if (!args.Any())
            {
                Console.WriteLine("Couldn't find a file. Ensure a file exists");
                Console.Read();
                return;
            }

            var filepath = Path.Combine(Environment.CurrentDirectory, args.First());
            SetCircularReferenceLookup(args);
            
            foreach (var splitParam in _splitParams)
                Directory.GetParent(filepath).CreateSubdirectory(splitParam.Name);

            TransformJson(filepath, Directory.GetParent(filepath));

            Console.WriteLine("Complete");
        }

        private static void SetCircularReferenceLookup(string[] args)
        {
            var circularReferenceJson = args.ElementAtOrDefault(1);
            if (circularReferenceJson == null)
            {
                _circularReferenceLookup = new Dictionary<string, IList<string>>();
                return;
            }

            using (var fileStream = File.OpenText(circularReferenceJson))
            {
                var jsonText = fileStream.ReadToEndAsync().Result;
                _circularReferenceLookup = JsonConvert.DeserializeObject<Dictionary<string, IList<string>>>(jsonText);
            }
        }

        private static void CircularReferenceResolver(JToken token)
        {
            void RemoveFrom(JProperty jProperty, params string[] tokens)
            {
                var remove = _circularReferenceLookup[jProperty.Name];

                foreach (var t in tokens)
                {
                    var result = jProperty.Value.SelectToken(t)?.Values<JProperty>();

                    if (result == null)
                        continue;
                    try
                    {
                        var x = result.Where(p => remove.Contains(p.Name)).ToList();

                        for (var i = 0; i < x.Count; i++)
                            x[i].Remove();
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }

            if (token.Type != JTokenType.Property)
                return;

            var property = token.Value<JProperty>();

            if (!_circularReferenceLookup.ContainsKey(property.Name))
                return;

            RemoveFrom(property, "required", "properties");
        }


        private static void Split(JObject node, SplitParam param, string directory)
        {
            var jToken = node[param.Name];

            if (jToken == null)
                return;

            var baseFilePath = $@"{directory}\{param.Name}";

            var childs = jToken.Children<JProperty>();

            if (param.RemoveAllBut != null && param.RemoveAllBut.Any())
                foreach (var jProperty in childs.Where(x => !param.RemoveAllBut.Contains(x.Name)).ToList())
                    jProperty.Remove();

            foreach (var child in childs)
            {
                param.Resolver?.Invoke(child);
                var filename = $"{child.Name.Split("/").Last()}";
                var content = child.Value.ToString(Formatting.None);
                File.WriteAllText(Path.Combine(baseFilePath, filename), content, Encoding.UTF8);
            }

            foreach (var property in param.Remove)
                if (node[property] != null)
                    node.Remove(property);
        }

        private static void TransformJson(string filepath, DirectoryInfo directory)
        {
            using (var fileStream = File.OpenText(filepath))
            {
                var path = filepath.Replace(".json", $"-changed.json");
                var changedFilename = path.Split(@"\").Last();

                var jsonText = fileStream.ReadToEndAsync().Result.Replace("#", $"/{GetSwaggerRefPath(path)}");
                var jsonObject = JObject.Parse(jsonText);

                WalkNode(jsonObject, node =>
                {
                    foreach (var splitParam in _splitParams)
                        Split(node, splitParam, directory.FullName);
                });

                using (var outputFile = new StreamWriter(path))
                {
                    outputFile.WriteLine(JsonConvert.SerializeObject(jsonObject, new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        MissingMemberHandling = MissingMemberHandling.Ignore,
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    }));
                }
            }
        }

        private static string GetSwaggerRefPath(string path)
        {
            var splitedPath = path.Split(@"\").Skip(1).ToList();
            splitedPath.RemoveAt(splitedPath.Count - 1);
            var swaggerRefPath = Path.Combine(splitedPath.ToArray()).Replace(@"\", "/");
            return swaggerRefPath;
        }

        private static void WalkNode(JToken node, Action<JObject> action)
        {
            if (node.Type == JTokenType.Object)
            {
                action((JObject)node);

                foreach (var child in node.Children<JProperty>())
                    WalkNode(child.Value, action);
            }
            else if (node.Type == JTokenType.Array)
            {
                foreach (var child in node.Children())
                    WalkNode(child, action);
            }
        }
    }
}