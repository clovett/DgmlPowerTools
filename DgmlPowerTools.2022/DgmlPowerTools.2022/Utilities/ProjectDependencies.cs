using System;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.GraphModel;
using Microsoft.VisualStudio.GraphModel.Styles;
using Microsoft.VisualStudio.Progression;
using Microsoft.VisualStudio.Progression.CodeSchema;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using CodeNodeCategories = Microsoft.VisualStudio.Progression.CodeSchema.NodeCategories;
using CodeNodeProperties = Microsoft.VisualStudio.Progression.CodeSchema.Properties;
using System.Diagnostics;
using System.Windows.Media;
using Microsoft.VisualStudio.Shell.Flavor;

namespace LovettSoftware.DgmlPowerTools
{
    public interface SProjectDependencies
    {
    }

    public interface IProjectDependencies
    {
        Graph GetProjectDependencies(IVsSolution solution);
    }

    class ProjectDependencies : SProjectDependencies, IProjectDependencies
    {
        private const string PropNameNugetVersion = "Nuget-Version";

        private Dictionary<string, GraphCategory> _categories = new Dictionary<string, GraphCategory>();
        private Dictionary<string, GraphConditionalStyle> _projectStyles;        

        public Graph GetProjectDependencies(IVsSolution solution)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var graph = new Graph();
            _projectStyles = new Dictionary<string, GraphConditionalStyle>();

            using (var scope = graph.BeginUpdate(Guid.NewGuid(), "initial", UndoOption.Disable))
            {
                graph.AddSchema(DgmlCommonSchema.Schema);
                graph.AddSchema(CodeGraphSchema.Schema);
                GraphSchema custom = new GraphSchema("DgmlPowerTool");
                graph.AddSchema(custom);

                AddCategoryStyle(graph, CodeNodeCategories.Assembly, "#FF094167", "CodeSchema_Assembly");

                GraphProperty versionProperty = CodeGraphSchema.Schema.Properties.AddNewProperty(PropNameNugetVersion, typeof(string),
                    () => {
                        var meta = new GraphMetadata("Version", "Nuget package version", null, GraphMetadataOptions.Serializable | GraphMetadataOptions.Browsable | GraphMetadataOptions.Sharable);
                        meta.SetValue(CodeNodeProperties.IsBrowsable, "True");
                        return meta;
                    });


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
                        string kind = sourceProject.Kind;
                        IVsHierarchy hierarchy;
                        solution.GetProjectOfUniqueName(id, out hierarchy);
                        IVsAggregatableProjectCorrected ap = hierarchy as IVsAggregatableProjectCorrected;
                        if (ap != null)
                        {
                            if (ap.GetAggregateProjectTypeGuids(out string guids) == 0)
                            {
                                kind = guids;
                            }
                        }

                        Debug.WriteLine($"{label} = {kind}");
                        GraphCategory projectCategory = GetOrCreateProjectCategoryStyle(graph, custom, kind);
                        GraphNode sourceProjectNode = graph.Nodes.GetOrCreate(id, label, projectCategory);

                        CreateNugetReferences(sourceProject.FullName, graph, sourceProjectNode, versionProperty);

                        object[] req = sourceItem.RequiredProjects as object[];
                        if (req != null)
                        {
                            foreach (Project targetProject in req)
                            {
                                string targetLabel = targetProject.Name;
                                string targetId = targetProject.UniqueName;
                                string targetKind = sourceProject.Kind;
                                if (targetId != id)
                                {
                                    GraphCategory targetProjectCategory = GetOrCreateProjectCategoryStyle(graph, custom, targetKind);
                                    GraphNode target = graph.Nodes.GetOrCreate(targetId, targetLabel, targetProjectCategory);
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

        static string GetKnownProjectCategory(string kind)
        {
            switch (kind)
            {
                case "{930c7802-8a8c-48f9-8165-68863bccd9dd}":
                    return "CodeMap_WixPackage";
                case "{C7167F0D-BC9F-4E6E-AFE1-012C56B48DB5}":
                    return "CodeMap_WindowsPackage";
                case "{A5A43C5B-DE2A-4C0C-9213-0A381AF9435A}":
                    return "CodeMap_WindowsStoreProject";
                case "{349C5851-65DF-11DA-9384-00065B846F21}":
                case "{E24C65DC-7377-472B-9ABA-BC803B73C61A}":
                    return "CodeMap_WebProject";
                case "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}":
                    return "CodeMap_WindowsProject";
                case "{60DC8134-EBA5-43B8-BCC9-BB4BC16C2548}":
                    return "CodeMap_WpfProject";
                case "{3AC096D0-A1C2-E12C-1390-A8335801FDAB}":
                    return "CodeMap_TestProject";
                case "{82B43B9B-A64C-4715-B499-D71E9CA2BD60}":
                    return "CodeMap_VsixProject";
                default:
                    return "CodeSchema_Project";
            }
        }

        static string GetKnownProjectIcon(string category)
        {
            switch (category)
            {
                case "CodeMap_WindowsPackage":
                case "CodeMap_WindowsProject":
                    return "CodeMap_WpfProject";
                case "CodeMap_WixPackage":
                    return "CodeSchema_Project";
                default:
                    return category;
            }
        }

        static string GetKnownProjectColors(string category)
        {
            switch (category)
            {
                default:
                    return "#FF307A69";
            }
        }

        GraphCategory GetOrCreateProjectCategoryStyle(Graph graph, GraphSchema schema, string kind)
        {
            string categoryName = "CodeSchema_Project";
            foreach (var guid in kind.Split(';'))
            {
                categoryName = GetKnownProjectCategory(guid.ToUpperInvariant());
                if (categoryName != "CodeMap_Project")
                {
                    break;
                }
            }

            _categories.TryGetValue(categoryName, out GraphCategory category);
            if (category == null)
            {
                string label = categoryName.Split('_')[1];
                category = schema.RegisterNodeCategory(categoryName, label, label + " Category", false, null);
                _categories[categoryName] = category;
            }

            // make sure this graph has the style
            if (!_projectStyles.TryGetValue(categoryName, out GraphConditionalStyle style))
            {
                var color = GetKnownProjectColors(categoryName);
                var icon = GetKnownProjectIcon(categoryName);
                style = AddCategoryStyle(graph, category, color, icon);
                _projectStyles[categoryName] = style;
            }

            return category;
        }

        private static GraphConditionalStyle AddCategoryStyle(Graph graph, GraphCategory category, string color, string icon)
        {
            var style = new GraphConditionalStyle(graph)
            {
                 TargetType = typeof(GraphNode),
                 GroupLabel = category.GetLabelOrId(graph),
                 ValueLabel = "Has category",
            };
            style.Conditions.Add(new GraphCondition(style)
            {
                Expression = $"HasCategory('{category.Id}')"
            });
            style.Setters.Add(new GraphSetter(style, "Background")
            {
                Value = color
            });
            style.Setters.Add(new GraphSetter(style, "Icon")
            {
                Value = icon
            });
            graph.Styles.Add(style);
            return style;
        }

        private static void CreateNugetReferences(string sourceProjectFullName, Graph graph, GraphNode sourceProjectNode, GraphProperty versionProp)
        {
            try
            {
                var csproj = XDocument.Load(sourceProjectFullName);
                XNamespace ns = csproj.Root.Name.Namespace;
                foreach (var e in csproj.Descendants(ns + "PackageReference"))
                {
                    string name = (string)e.Attribute("Include");
                    if (!string.IsNullOrEmpty(name))
                    {
                        string nugetVersion = (string)e.Attribute(ns + "Version");
                        if (string.IsNullOrEmpty(nugetVersion))
                        {
                            nugetVersion = (string)e.Element(ns + "Version");
                        }
                        string id = name;
                        string label = name;
                        if (!string.IsNullOrEmpty(nugetVersion))
                        {
                            id += "-" + nugetVersion;
                        }

                        GraphNode nugetNode = graph.Nodes.GetOrCreate(id, label, CodeNodeCategories.Assembly);
                        if (!string.IsNullOrEmpty(nugetVersion)) {
                            nugetNode.SetValue(versionProp, nugetVersion);
                        }
                        graph.Links.GetOrCreate(sourceProjectNode, nugetNode);

                        // check for inconsistent version and mark them with a red border
                        foreach (var existingNugetNode in graph.Nodes.Where(n => n.Categories.Contains(CodeNodeCategories.Assembly)))
                        {
                            if (nugetNode.Label == existingNugetNode.Label && nugetNode.Id != existingNugetNode.Id)
                            {
                                nugetNode.SetStroke(Brushes.Red);
                                existingNugetNode.SetStroke(Brushes.Red);
                            }
                        }
                    }
                }
            } 
            catch (Exception)
            {
                // ignore bad files..
            }
        }
    }
}
