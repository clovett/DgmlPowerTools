using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.GraphModel;
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
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            Graph result = new Graph();
            using (var scope = result.BeginUpdate(Guid.NewGuid(), "initial", UndoOption.Disable))
            {
                result.AddSchema(DgmlCommonSchema.Schema);
                result.AddSchema(CodeGraphSchema.Schema);

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
                        GraphNode src = result.Nodes.GetOrCreate(id, label, CodeNodeCategories.Project);

                        object[] req = sourceItem.RequiredProjects as object[];
                        if (req != null)
                        {
                            foreach (Project targetProject in req)
                            {
                                string targetLabel = targetProject.Name;
                                string targetId = targetProject.UniqueName;
                                if (targetId != id)
                                {
                                    GraphNode target = result.Nodes.GetOrCreate(targetId, targetLabel, CodeNodeCategories.Project);
                                    result.Links.GetOrCreate(src, target);
                                }
                            }
                        }
                    }
                    scope.Complete();
                }
            }
            return result;
        }
    }
}
