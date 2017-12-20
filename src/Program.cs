using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Swagger.Json.Utils
{
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
            var circularReferenceJson = args.ElementAtOrDefault(1);
            var command = $"swagger-cli bundle {GetChangedFilePath(filepath)} -r";
            SetCircularReferenceLookup(circularReferenceJson);
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            string success;
            do
            {
                SwaggerParser(filepath);

                ExecuteCommandSync(command, out var error, out success);

                if (string.IsNullOrWhiteSpace(error))
                    break;

                var outputSplitedBySharp = error.Replace("Circular $ref pointer found at", string.Empty).Split("#");
                var key = outputSplitedBySharp.First().Split("/").Last();
                var value = GetCircularReferenceOutputValue(outputSplitedBySharp);

                Console.WriteLine($"Avoid {key}:{value} circular reference.");

                if (_circularReferenceLookup.ContainsKey(key))
                {
                    _circularReferenceLookup[key].Add(value);
                    continue;
                }

                _circularReferenceLookup.Add(key, new List<string>
                {
                    value
                });
            } while (true);
            stopwatch.Stop();

            SaveFile(circularReferenceJson, _circularReferenceLookup);
            SaveFile("swagger-bundle.json", success);

            Console.WriteLine($"Finish \n elapsed time:{stopwatch.ElapsedMilliseconds}ms");
            Console.Read();
        }

        private static void SaveFile(string circularReferenceJson, object obj)
        {
            using (var stream = new StreamWriter(circularReferenceJson))
            using (var jsonTextWriter = new JsonTextWriter(stream))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(jsonTextWriter, obj);
            }
        }

        private static string GetCircularReferenceOutputValue(string[] outputSplitedBySharp)
        {
            var valueOutputSplited = outputSplitedBySharp[1].Split("/").ToArray();
            var valueIndex = valueOutputSplited.Where(x => x.Equals("properties"))
                                 .Select(x => Array.IndexOf(valueOutputSplited, x)).Single() + 1;
            var value = valueOutputSplited[valueIndex];
            return value.Replace("\n", string.Empty);
        }

        public static void ExecuteCommandSync(object command, out string error, out string success)
        {
            error = success = string.Empty;
            var procStartInfo =
                new ProcessStartInfo("cmd", "/c " + command)
                {

                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            var process = new Process();
            process.StartInfo = procStartInfo;
            process.Start();

            while (process.StandardOutput.Peek() > -1)
                success = process.StandardOutput.ReadLine();

            while (process.StandardError.Peek() > -1)
                error = process.StandardError.ReadLine();

            process.WaitForExit();
        }

        private static void SwaggerParser(string filepath)
        {
            foreach (var splitParam in _splitParams)
                Directory.GetParent(filepath).CreateSubdirectory(splitParam.Name);

            TransformJson(filepath, Directory.GetParent(filepath));
        }

        private static void SetCircularReferenceLookup(string circularReferenceJson)
        {
            if (circularReferenceJson == null || !File.Exists(circularReferenceJson))
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

            if (_circularReferenceLookup == null || !_circularReferenceLookup.ContainsKey(property.Name))
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
                var path = GetChangedFilePath(filepath);
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

        private static string GetChangedFilePath(string filepath)
        {
            return filepath.Replace(".json", $"-changed.json");
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