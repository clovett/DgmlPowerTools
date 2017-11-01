using Microsoft.VisualStudio.Diagrams.View;
using Microsoft.VisualStudio.GraphModel;
using Microsoft.VisualStudio.GraphModel.Schemas;
using Microsoft.VisualStudio.Progression;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LovettSoftware.DgmlPowerTools
{
    /// <summary>
    /// This class encapsulates drag/drop operation from windows shell.
    /// </summary>
    [Export(typeof(IGraphDropHandler))]
    public sealed class FileDragDropHandler : IGraphDropHandler
    {
        string[] formats = new string[] { "FileDrop", "FileNameW" };

        public FileDragDropHandler()
        {
        }

        bool CanReceive(IDataObject data)
        {
            foreach (string f in formats)
            {
                if (data.GetDataPresent(f))
                {
                    return true;
                }
            }
            return false;
        }

        GraphNodeIdName FileNameId = GraphNodeIdName.Get("FileName", "FileName", typeof(Uri));


        #region IGraphDropHandler

        public bool CanDrop(object target, DragEventArgs args)
        {
            if (CanReceive(args.Data))
            {
                args.Effects |= DragDropEffects.Link;
                return true;
            }
            return false;
        }

        void IGraphDropHandler.OnDrop(object sender, DragEventArgs args)
        {
            if (CanReceive(args.Data))
            {
                GraphControl control = (GraphControl)sender;
                Point pos = args.GetPosition(control.Diagram);
                GraphObject hit = control.GetChildAt(pos, false);
                GraphNode target = (hit == null) ? null : hit.AsNode();

                DoFileDrop(control, target, pos, args.Data);
                args.Handled = true;
            }
        }

        public double Priority
        {
            get { return 10; }
        }

        void DoFileDrop(GraphControl control, GraphNode target, Point pos, IDataObject data)
        {
            foreach (string format in formats)
            {
                if (data.GetDataPresent(format))
                {
                    string[] list = data.GetData(format) as string[];
                    if (list != null)
                    {
                        OnFilesDropped(control, target, pos, list);
                        return;
                    }
                }
            }
        }

        private void OnFilesDropped(GraphControl control, GraphNode target, Point pos, string[] files)
        {
            if (control == null)
            {
                return;
            }

            List<GraphNode> newNodes = new List<GraphNode>();
            Graph graph = control.Graph;

            Graph dropped = new Graph();
            dropped.CopySchemas(graph);

            using (var scope = new UndoableGraphTransactionScope(Resources.Drop))
            {
                foreach (string file in files)
                {
                    string ext = System.IO.Path.GetExtension(file);
                    string extension = System.IO.Path.GetExtension(file).ToLowerInvariant();

                    string href = file;
                    Uri uri = null;
                    try
                    {
                        uri = new Uri(file);
                        Uri baseUri = control.BaseUri;
                        Uri relative = baseUri.MakeRelativeUri(uri);
                        href = relative.ToString();
                    }
                    catch { 
                    }

                    GraphCategory cat = null;
                    GraphNodeId id = null;
                    if (uri != null && (string.Compare(extension, ".exe", StringComparison.OrdinalIgnoreCase) == 0 ||
                        string.Compare(extension, ".dll", StringComparison.OrdinalIgnoreCase) == 0))
                    {
                        cat = CodeNodeCategories.Assembly;
                        id = GraphNodeId.GetNested(GraphNodeId.GetPartial(CodeGraphNodeIdName.Assembly, uri));                        
                    }
                    else
                    {
                        cat = NodeCategories.File;
                        id = GraphNodeId.GetNested(GraphNodeId.GetPartial(FileNameId, new Uri(file)));
                    }

                    var node = dropped.Nodes.GetOrCreate(id, System.IO.Path.GetFileName(file), cat);

                    if (string.Compare(extension, ".gif", StringComparison.OrdinalIgnoreCase) == 0 ||
                        string.Compare(extension, ".png", StringComparison.OrdinalIgnoreCase) == 0 ||
                        string.Compare(extension, ".jpg", StringComparison.OrdinalIgnoreCase) == 0 ||
                        string.Compare(extension, ".bmp", StringComparison.OrdinalIgnoreCase) == 0 ||
                        string.Compare(extension, ".ico", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        // then it looks like it's an image, so try load the image as an icon.
                        node.SetIcon(href);
                        node.SetShape("None");
                        node.Label = "";
                    }
                    else if (string.Compare(extension, ".exe", StringComparison.OrdinalIgnoreCase) == 0 ||
                        string.Compare(extension, ".dll", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        node.SetValue<string>(DgmlProperties.FilePath, file);
                    }
                    else
                    {
                        node.SetValue<string>(DgmlProperties.Reference, file);
                    }

                    newNodes.Add(node);
                }

                if (target == null || target.IsGroup)
                {
                    SeedGridLayoutAtMousePosition(control, pos, newNodes);
                }

                scope.Complete();
            }

            // Ok, now merge this new graph in with the existing one reparenting the nodes into
            // the target group.
            if (newNodes != null && newNodes.Any<GraphNode>())
            {
                using (control.Diagram.SuspendLayoutWorker())
                {
                    object causality = new CausalAction(Resources.Drop);
                    UndoOption option = UndoOption.Add;

                    if (target != null && !target.IsGroup)
                    {
                        // has to be in separate change
                        using (var scope = new UndoableGraphTransactionScope(causality, UndoOption.Add))
                        {
                            option = UndoOption.Merge;
                            target.IsGroup = true;
                            GraphGroup group = graph.FindGroup(target);
                            scope.Complete();
                        }
                    }

                    // Ok, now do the merger...
                    using (var scope = new UndoableGraphTransactionScope(causality, option))
                    {
                        GraphGroup group = graph.FindGroup(target);
                        if (group != null)
                        {
                            group.IsExpanded = true;
                        }
                        control.MergeGraphs(dropped, group, Resources.Drop, pos, true);
                        scope.Complete();
                    }

                    List<GraphNode> merged = new List<GraphNode>();
                    foreach (GraphNode node in dropped.Nodes)
                    {
                        GraphNode m = control.Graph.Nodes.Get(node.Id);
                        if (m != null)
                        {
                            merged.Add(m);
                        }
                    }
                    control.SelectAll(merged);

                }
            }
        }

        /// <summary>
        /// Seed some initial bounds on the nodes so they don't all just stack up 
        /// in one huge vertical line.  This also works around a problem in LayoutGraph where it
        /// thinks it needs to do a Kind=Full layout if there is any root level node with no Bounds property.
        /// </summary>
        /// <param name="nodes">The nodes to layout</param>
        private void SeedGridLayoutAtMousePosition(GraphControl control, Point pos, List<GraphNode> nodes)
        {
            DiagramControl diagram = control.Diagram;
            pos = diagram.TransformToCanvas().Transform(pos);
            int columns = (int)Math.Sqrt(nodes.Count);
            if (columns == 0) columns = 1;
            int col = 0;
            double x = pos.X;
            double y = pos.Y;
            foreach (GraphNode node in nodes)
            {
                node.SetBounds(new Rect(x, y, 0, 0));
                col++;
                if (col == columns)
                {
                    col = 0;
                    x = pos.X;
                    y += 30;
                }
                else
                {
                    x += 110;
                }
            }
        }
        #endregion

    }
}
