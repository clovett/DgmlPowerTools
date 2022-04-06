using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Xml.Linq;
using System.Xml.XPath;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.GraphModel;
using Microsoft.VisualStudio.GraphModel.Styles;
using Microsoft.VisualStudio.Progression;
using Microsoft.VisualStudio.Progression.CodeSchema;
using Microsoft.VisualStudio.Shell;

using CodeNodeCategories = Microsoft.VisualStudio.Progression.CodeSchema.NodeCategories;

namespace LovettSoftware.DgmlPowerTools
{
    public interface SProjectDependencies
    {
    }

    public interface IProjectDependencies
    {
        Graph GetProjectDependencies();
    }

    class ProjectDependencies : SProjectDependencies, IProjectDependencies
    {
        public Graph GetProjectDependencies()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var graph = new Graph();

            using (var scope = graph.BeginUpdate(Guid.NewGuid(), "initial", UndoOption.Disable))
            {
                graph.AddSchema(DgmlCommonSchema.Schema);
                graph.AddSchema(CodeGraphSchema.Schema);

                AddCategory(graph, CodeNodeCategories.Project, "Project", Brushes.Green);
                AddCategory(graph, CodeNodeCategories.Assembly, "Nuget Package", Brushes.Blue);

                DTE2 dte2 = Package.GetGlobalService(typeof(DTE)) as DTE2;
                int count = dte2.Solution.SolutionBuild.BuildDependencies.Count;
                for (int i = 1; i <= count; i++)
                {
                    BuildDependency sourceItem = dte2.Solution.SolutionBuild.BuildDependencies.Item(i);
                    var sourceProject = sourceItem.Project;
                    if (sourceProject != null)
                    {
                        string label = sourceProject.Name;
                        string id = sourceProject.UniqueName;
                        GraphNode sourceProjectNode = graph.Nodes.GetOrCreate(id, label, CodeNodeCategories.Project);

                        CreateNugetReferences(sourceProject.FullName, graph, sourceProjectNode);

                        object[] req = sourceItem.RequiredProjects as object[];
                        if (req != null)
                        {
                            foreach (Project targetProject in req)
                            {
                                string targetLabel = targetProject.Name;
                                string targetId = targetProject.UniqueName;
                                if (targetId != id)
                                {
                                    GraphNode target = graph.Nodes.GetOrCreate(targetId, targetLabel, CodeNodeCategories.Project);
                                    graph.Links.GetOrCreate(sourceProjectNode, target);
                                }
                            }
                        }
                    }
                    scope.Complete();
                }
            }
            return graph;
        }

        private static void AddCategory(Graph graph, GraphCategory graphCategory, string legendLabel, Brush brush)
        {
            // add category
            graph.AddCategory(graphCategory);
            graph.Categories.Single(c => c == graphCategory).GetMetadata(graph).SetBackground(brush);

            // add style for category
            var style = new GraphConditionalStyle(graph) { GroupLabel = legendLabel, ValueLabel = "True", TargetType = typeof(GraphNode) };
            style.Conditions.Add(new GraphCondition(style) { Expression = $"HasCategory('{graphCategory}')" });
            style.Setters.Add(new GraphSetter(style, "Background") { Value = brush.ToString() });
            graph.Styles.Add(style);
        }

        private static void CreateNugetReferences(string sourceProjectFullName, Graph graph, GraphNode sourceProjectNode)
        {
            var csproj = XDocument.Parse(File.ReadAllText(sourceProjectFullName));
            var nugets = csproj.XPathSelectElements("//PackageReference")
                                                .Select(packageReference => new Nuget
                                                {
                                                    Name = packageReference.Attribute("Include").Value,
                                                    Version = new Version(packageReference.Attribute("Version").Value)
                                                });

            foreach (var nuget in nugets)
            {
                GraphNode nugetNode = graph.Nodes.GetOrCreate($"{nuget.Name}-{nuget.Version}", $"{nuget.Name}\n{nuget.Version}", CodeNodeCategories.Assembly);
                graph.Links.GetOrCreate(sourceProjectNode, nugetNode);
            }
        }
        internal class Nuget
        {
            public string Name { get; set; }
            public Version Version { get; set; }
        }
    }
}
