using Microsoft.VisualStudio.GraphModel;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Reflection;
using NodeCategories = Microsoft.VisualStudio.Progression.NodeCategories;
using Microsoft.VisualStudio.GraphModel.Schemas;
using System;

namespace Microsoft.VisualStudio.GraphProviders
{
    /// <summary>
    /// Expand dependencies of a managed assembly.
    /// </summary>
    [Export(typeof(IGraphDependencyProvider))]
    class AssemblyProvider : IGraphDependencyProvider
    {
        public bool ExpandDependencies(IEnumerable<GraphNode> context, int depth)
        {
            bool rc = false;
            foreach (var node in context)
            {
                rc |= ExpandAssemblyDependencies(node, depth);
            }
            return rc;
        }

        string GetQualifiedFileName(GraphNodeId id)
        {
            var part = id.GetNestedIdByName(CodeGraphNodeIdName.Assembly);
            if (part != null)
            {
                return part.ToString();
            }
            part = id.GetNestedIdByName(CodeGraphNodeIdName.File);
            if (part != null)
            {
                return part.ToString();
            }

            GraphNodeIdName filePathName = GraphNodeIdName.Get("FilePath", "FilePath", typeof(string));
            part = id.GetNestedIdByName(filePathName);
            if (part != null)
            {
                return part.ToString();
            }
            return id.ToString();
        }


        private bool ExpandAssemblyDependencies(GraphNode node, int levels)
        {
            if (node.HasCategory(NodeCategories.File)  && node.HasValue(Microsoft.VisualStudio.Progression.DgmlProperties.FilePath))
            {
                var path = node.GetValue<string>(Microsoft.VisualStudio.Progression.DgmlProperties.FilePath);
                return AddDependencies(node, path, new HashSet<string>(), levels);
            }
            else if (node.HasCategory(CodeNodeCategories.Assembly)) { 
                return AddDependencies(node, GetQualifiedFileName(node.Id), new HashSet<string>(), levels);
            }
            return false;
        }

        private static bool AddDependencies(GraphNode node, string filename, HashSet<string> loaded, int depth)
        {
            string localFile = null;
            try
            {
                var ext = System.IO.Path.GetExtension(filename);
                if (ext != ".exe" && ext != ".dll")
                {
                    return false;
                }
                Uri uri = new Uri(filename);
                localFile = uri.LocalPath;
                if (loaded.Contains(localFile) || !File.Exists(localFile))
                {
                    return false;
                }
            } 
            catch
            {
                // ignore badly formed file names
                return false;
            }

            bool rc = false;
            var graph = node.Owner;
            try
            {
                var dir = Path.GetDirectoryName(localFile);
                Assembly a = Assembly.ReflectionOnlyLoadFrom(localFile);
                foreach (var ar in a.GetReferencedAssemblies())
                {
                    var label = ar.Name;
                    var found = FindAssembly(dir, ar);
                    if (found != null)
                    {
                        loaded.Add(localFile);
                        var refNode = graph.Nodes.GetOrCreate(found, label, Microsoft.VisualStudio.GraphModel.Schemas.CodeNodeCategories.Assembly);
                        graph.Links.GetOrCreate(node, refNode);
                        rc = true;
                        if (depth > 1)
                        {
                            AddDependencies(refNode, found, loaded, depth - 1);
                        }
                    }
                    else
                    {
                        // unresolvable (todo: give it a different icon?).
                        var refNode = graph.Nodes.GetOrCreate(label, label, Microsoft.VisualStudio.GraphModel.Schemas.CodeNodeCategories.Assembly); 
                        graph.Links.GetOrCreate(node, refNode);
                    }
                }
            } 
            catch
            {
                // perhaps it is not managed?
                return AddNativeDependencies(node, filename, loaded, depth);
            }
            return rc;
        }

        private static string FindAssembly(string possibleDirectory, AssemblyName name)
        {
            try
            {
                Assembly a = Assembly.ReflectionOnlyLoad(name.FullName);
                return new Uri(a.Location).LocalPath;
            } 
            catch (Exception)
            {
                // try and resolve dependency using parent assembly folder.
                string fullPath = Path.Combine(possibleDirectory, name.Name + ".dll");
                if (File.Exists(fullPath))
                {
                    try
                    {
                        var a = Assembly.LoadFrom(fullPath);
                        return new Uri(a.Location).LocalPath;
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                }
                return null;
            }
        }

        private static bool AddNativeDependencies(GraphNode node, string filename, HashSet<string> loaded, int depth)
        {
            if (loaded.Contains(filename))
            {
                // break circular references.
                return false;
            }

            bool rc = false;
            var graph = node.Owner;
            byte[] coff = File.ReadAllBytes(filename);
            using (var cp = new CoffProvider(coff))
            {
                var imports = cp.GetImports();
                if (imports != null)
                {
                    foreach (var name in imports)
                    {
                        var label = name;
                        var found = FindNativeBinary(filename, name);
                        if (found != null)
                        {
                            loaded.Add(found);
                            var refNode = graph.Nodes.GetOrCreate(found, label, Microsoft.VisualStudio.GraphModel.Schemas.CodeNodeCategories.Assembly);
                            graph.Links.GetOrCreate(node, refNode);
                            rc = true;
                            if (depth > 1)
                            {
                                AddNativeDependencies(refNode, found, loaded, depth - 1);
                            }
                        }
                        else
                        {
                            // unresolvable (todo: give it a different icon?).
                            var refNode = graph.Nodes.GetOrCreate(label, label, Microsoft.VisualStudio.GraphModel.Schemas.CodeNodeCategories.Assembly);
                            graph.Links.GetOrCreate(node, refNode);
                        }
                    }
                }
            }
            return rc;
        }

        private static string FindNativeBinary(string basePath, string name)
        {
            var local = Path.Combine(Path.GetDirectoryName(basePath), name);
            if (File.Exists(local))
            {
                return local;
            }

            foreach (var path in Environment.GetEnvironmentVariable("PATH").Split(';'))
            {
                string fullPath = Path.Combine(path, name);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
            return null;
        }
    }
}
