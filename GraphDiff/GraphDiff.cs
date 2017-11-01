using Microsoft.VisualStudio.GraphModel;
using Microsoft.VisualStudio.GraphModel.GraphDiff;
using Microsoft.VisualStudio.GraphModel.Schemas;
using Microsoft.VisualStudio.Progression;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LovettSoftware.DgmlPowerTools
{
    public class DiffResultInfo
    {
        public GraphObject GraphObject { get; set; }
        public string Mesage { get; set; }
        public string Details { get; set; }
    }

    /// <summary>
    /// This class wraps the underlying diff engine and provides a more easy to consume API.
    /// </summary>
    public class GraphDiff
    {
        // List of properties we ignore for comparison purposes.
        HashSet<string> _whitelist = new HashSet<string>();

        public GraphDiff()
        {
            _whitelist.Add("Bounds");
            _whitelist.Add("LabelBounds");
            _whitelist.Add("UseManualLocation");
        }

        
        /// <summary>
        /// Copy all serializable, non-pseudo, sharable, nodes and links from the given graph into a new clean copy
        /// </summary>
        /// <param name="other">The graph to use as a source for copying</param>
        /// <returns>true if the graph was copied</returns>
        public Graph Copy(Graph original)
        {
            Graph graph = new Graph();
            graph.CopyProperties(original);
            graph.SetValue<string>(DgmlProperties.BaseUri, original.GetValue<string>(DgmlProperties.BaseUri));
            graph.Merge(original);
            return graph;
        }

        private List<DiffResultInfo> diffResults;

        public List<DiffResultInfo> Differences
        {
            get { return this.diffResults; }
        }

        public string Compare(Graph first, Graph second, Graph result)
        {
            first = Copy(first); // make a copy!

            diffResults = new List<DiffResultInfo>();

            ExecutionEngine diffengine = new ExecutionEngine();
            diffengine.IgnoreProperties = _whitelist;
            diffengine.FoundDifferentNodeEvent += new EventHandler<FoundDiffEventArgs>(OnFoundDifferentNodeEvent);
            diffengine.FoundDifferentPropertyEvent += new EventHandler<FoundDiffPropertyEventArgs>(OnFoundDifferentPropertyEvent);
            diffengine.FoundDifferentCategoryEvent += new EventHandler<FoundDiffCategoryEventArgs>(OnFoundDifferentCategoryEvent);
            
            result.CopySchemas(first);
            result.CopySchemas(second);
            diffengine.Execute(first, second, result);

            // Save the diff in the same place so that it can pick up and referenced icons via relative paths.
            Uri baseUri = new Uri(first.GetValue<string>(DgmlProperties.BaseUri));
            string outputPath = Path.Combine(Path.GetDirectoryName(baseUri.LocalPath), Path.GetFileNameWithoutExtension(baseUri.LocalPath) + ".diff.dgml");
            result.SetValue<string>(DgmlProperties.BaseUri, outputPath); 
            result.Save(outputPath);

            return outputPath;
        }
            

        void OnFoundDifferentNodeEvent(object sender, FoundDiffEventArgs e)
        {
            string format = null;
            switch (e.DiffType)
            {
                case DiffType.Add:
                    if (e.GraphObject is GraphNode)
                    {
                        format = "Node '{0}' was added";
                    }
                    else if (e.GraphObject is GraphLink)
                    {
                        format = "Link '{0}' was added";
                    }
                    break;
                case DiffType.Remove:
                    if (e.GraphObject is GraphNode)
                    {
                        format = "Node '{0}' was removed";
                    }
                    else if (e.GraphObject is GraphLink)
                    {
                        format = "Link '{0}' was removed";
                    }
                    break;
            } 
            if (format != null)
            {
                ReportError(e.GraphObject, string.Format(format, GetNodeShortName(e.GraphObject)));
            }
        }

        private void ReportError(GraphObject graphObject, string message)
        {
            diffResults.Add(new DiffResultInfo() { GraphObject = graphObject, Mesage = message });
        }

        void OnFoundDifferentCategoryEvent(object sender, FoundDiffCategoryEventArgs e)
        {
            if (e.GraphObject != null)
            {
                string format = null;
                switch (e.DiffType)
                {
                    case DiffType.Add:
                        if (e.GraphObject is GraphNode)
                        {
                            format = "Category '{0}' was added to Node '{1}'";
                        }
                        else if (e.GraphObject is GraphLink)
                        {
                            format = "Category '{0}' was added to Link '{1}'";
                        }
                        break;
                    case DiffType.Remove:
                        if (e.GraphObject is GraphNode)
                        {
                            format = "Category '{0}' was removed from Node '{1}'";
                        }
                        else if (e.GraphObject is GraphLink)
                        {
                            format = "Category '{0}' was removed from Link '{1}'";
                        }
                        break;
                }
                if (format != null)
                {
                    ReportError(e.GraphObject, string.Format(format, e.GraphCat.Id, GetNodeShortName(e.GraphObject)));
                }
                return;
            }

            GraphLink link = e.GraphObject as GraphLink;
            if (link != null)
            {
                string format = null;
                switch (e.DiffType)
                {
                    case DiffType.Add:
                        format = "Category '{0}' was added to Link '{1}'->'{2}'";
                        break;
                    case DiffType.Remove:
                        format = "Category '{0}' was removed from Link '{1}'->'{2}'";
                        break;
                }
                if (format != null)
                {
                    ReportError(link, string.Format(format, e.GraphCat.Id, GetNodeShortName(link.Source), GetNodeShortName(link.Target)));
                }
                return;
            }
        }

        void OnFoundDifferentPropertyEvent(object sender, FoundDiffPropertyEventArgs e)
        {
            GraphObject graphObject = e.GraphObject2; // object 2 is in the result graph.
            if (e.GraphObject != null)
            {
                string format = null;
                switch (e.DiffType)
                {
                    case DiffType.Add:
                        if (graphObject is GraphNode)
                        {
                            format = "Property '{0}' was added to Node '{1}'";
                        }
                        else if (graphObject is GraphLink)
                        {
                            format = "Property '{0}' was added to Link '{1}'";
                        }
                        break;
                    case DiffType.Remove:
                        if (graphObject is GraphNode)
                        {
                            format = "Property '{0}' was removed from Node '{1}'";
                        }
                        else if (graphObject is GraphLink)
                        {
                            format = "Property '{0}' was removed from Link '{1}'";
                        }
                        break;
                    case DiffType.Modify:
                        if (graphObject is GraphNode)
                        {
                            format = "Property value '{0}' was changed on Node '{1}'";
                        }
                        else if (graphObject is GraphLink)
                        {
                            format = "Property value '{0}' was changed on Link '{1}'";
                        }
                        break;
                }
                if (format != null)
                {
                    ReportError(graphObject, string.Format(format, e.GraphProp.Id, GetNodeShortName(graphObject)));
                }
                return;
            }
        }

        string GetNodeShortName(GraphObject go)
        {
            GraphNode node = go as GraphNode;
            if (node != null)
            {
                string label = node.Label;
                if (!string.IsNullOrEmpty(label)) return label;
                return node.Id.ToString();
            }
            GraphLink link = go as GraphLink;
            if (link != null)
            {
                string label = link.Label;
                if (!string.IsNullOrEmpty(label)) return label;
                return GetNodeShortName(link.Source) + " -> " + GetNodeShortName(link.Target);
            }
            if (go == null)
            {
                return "";
            }
            return go.GetType().Name;
        }

    }
}
