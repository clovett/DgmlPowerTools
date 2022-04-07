using Microsoft.VisualStudio.GraphModel;
using Microsoft.VisualStudio.Progression;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.Build.Construction;
using System.IO;
using System.Xml.Linq;
using Microsoft.VisualStudio.GraphModel.Schemas;

namespace Microsoft.VisualStudio.GraphProviders
{
    [Export(typeof(IGraphDependencyProvider))]
    class MsbuildProvider : IGraphDependencyProvider
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
            if (node.HasCategory(NodeCategories.File) && node.HasValue(Microsoft.VisualStudio.Progression.DgmlProperties.FilePath))
            {
                var path = node.GetValue<string>(Microsoft.VisualStudio.Progression.DgmlProperties.FilePath);
                return AddDependencies(node, path, new HashSet<string>(), levels);
            }
            else if (node.HasCategory(Microsoft.VisualStudio.GraphModel.Schemas.CodeNodeCategories.Assembly))
            {
                return AddDependencies(node, GetQualifiedFileName(node.Id), new HashSet<string>(), levels);
            }
            return false;
        }

        private bool AddDependencies(GraphNode node, string filename, HashSet<string> loaded, int depth)
        {
            string localFile = null;
            try
            {
                Uri uri = new Uri(filename);
                localFile = uri.LocalPath;
                if (localFile.EndsWith(".sln"))
                {
                    return AddSolutionDependencies(node, localFile, loaded, depth);
                }
                else if (localFile.EndsWith(".csproj") || localFile.EndsWith(".vcxsproj") || localFile.EndsWith(".vbproj"))
                {
                    return AddProjectDependencies(node, localFile, loaded, depth);
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                // ignore badly formed file names
            }
            return false;
        }

        private bool AddSolutionDependencies(GraphNode node, string filename, HashSet<string> loaded, int depth)
        {
            var solutionFile = SolutionFile.Parse(filename);
            foreach (var project in solutionFile.ProjectsInOrder)
            {
                var path = project.AbsolutePath;
                var name = project.ProjectName;
                AddProjectNode(node, loaded, depth, path, name);
            }
            return true;
        }

        private void AddProjectNode(GraphNode node, HashSet<string> loaded, int depth, string path, string name)
        {
            Graph graph = node.Owner;
            if (File.Exists(path) && !loaded.Contains(path))
            {
                loaded.Add(path);
                var refNode = graph.Nodes.GetOrCreate(path, name, Microsoft.VisualStudio.GraphModel.Schemas.CodeNodeCategories.Project);
                if (!refNode.HasCategory(Microsoft.VisualStudio.GraphModel.Schemas.CodeNodeCategories.File))
                {
                    refNode.AddCategory(Microsoft.VisualStudio.GraphModel.Schemas.CodeNodeCategories.File);
                }
                graph.Links.GetOrCreate(node, refNode);
                if (depth > 1)
                {
                    AddProjectDependencies(refNode, path, loaded, depth - 1);
                }
            }
            else
            {
                // unresolvable (todo: give it a different icon?).
                var refNode = graph.Nodes.GetOrCreate(path, name, Microsoft.VisualStudio.GraphModel.Schemas.CodeNodeCategories.Project);
                if (!refNode.HasCategory(Microsoft.VisualStudio.GraphModel.Schemas.CodeNodeCategories.File))
                {
                    refNode.AddCategory(Microsoft.VisualStudio.GraphModel.Schemas.CodeNodeCategories.File);
                }
                graph.Links.GetOrCreate(node, refNode);
            }
        }

        private bool AddProjectDependencies(GraphNode node, string localFile, HashSet<string> loaded, int depth)
        {
            Uri baseUri = new Uri(localFile);
            XDocument doc = XDocument.Load(localFile);
            foreach (var e in doc.Root.Elements())
            {
                if (e.Name.LocalName == "ItemGroup")
                {
                    foreach (var c in e.Elements())
                    {
                        if (c.Name.LocalName == "ProjectReference")
                        {
                            var ne = c.Element(c.Name.Namespace + "Name");
                            if (ne != null) {
                                string name = ne.Value;
                                string include = (string)c.Attribute("Include");
                                if (!string.IsNullOrEmpty(include))
                                {
                                    Uri resolved = new Uri(baseUri, include);
                                    var path = resolved.LocalPath;
                                    AddProjectNode(node, loaded, depth, path, name);
                                }
                            }
                        }
                    }
                }
            }

            return true;
        }

    }
}