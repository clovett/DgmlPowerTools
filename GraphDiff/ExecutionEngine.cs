using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.VisualStudio.GraphModel;
using Microsoft.VisualStudio.GraphModel.Styles;
using System.IO;

namespace Microsoft.VisualStudio.GraphModel.GraphDiff
{
    public enum DiffType
    {
        Add,
        Remove,
        Modify,
        Equal
    }

    static class GraphDiffSchema {

        static GraphSchema instance;

        public static GraphSchema Instance 
        {
            get
            {
                if (instance == null)
                {
                    instance = new GraphSchema("Graph Diff Schema");
                    GraphCommonSchema.Schema.AddSchema(instance);
                }
                return instance;
            }
        }
    }

    public class ExecutionEngine
    {
        #region DGML Categories & Properties

        GraphCategory addedCategory = GraphDiffSchema.Instance.Categories.AddNewCategory("DGMLDiff.Add");
        GraphCategory modifiedCategory = GraphDiffSchema.Instance.Categories.AddNewCategory("DGMLDiff.Modify");
        GraphCategory removedCategory = GraphDiffSchema.Instance.Categories.AddNewCategory("DGMLDiff.Remove");
        GraphCategory partialCategory = GraphDiffSchema.Instance.Categories.AddNewCategory("DGMLDiff.Partial");

        GraphProperty childrenAdded = GraphDiffSchema.Instance.Properties.AddNewProperty("ChildrenAdded", typeof(int));
        GraphProperty childrenRemoved = GraphDiffSchema.Instance.Properties.AddNewProperty("ChildrenRemoved", typeof(int));

        #endregion 

        public ExecutionEngine()
        {
        }

        /// <summary>
        /// Provides a set of property id's that will be excluded from the diff process.
        /// </summary>
        public HashSet<string> IgnoreProperties { get; set; }

        public void Execute(Graph firstGraph, Graph secondGraph, Graph result)
        {
            if (IgnoreProperties == null)
            {
                IgnoreProperties = new HashSet<string>();
            }
            result.AddSchema(GraphDiffSchema.Instance);
            result.Merge(secondGraph);

            // Register the categories in the second graph
            result.AddCategory(addedCategory);
            result.AddCategory(modifiedCategory);
            result.AddCategory(removedCategory);

            DiffNodes(firstGraph, result);
            DiffLinks(firstGraph, result);

            FindGroups(result);
            PropagateGroupDiff();
        }

        #region Diff Nodes

        private void DiffNodes(Graph firstGraph, Graph secondGraph)
        {

            foreach (GraphNode firstNode in firstGraph.Nodes)
            {

                GraphNode secondNode = secondGraph.Nodes.Get(firstNode.Id);

                // second graph doesnt have this node.
                if (secondNode == null)
                {
                    // If the second graph DOES NOT have the 
                    // node that is present in the first graph
                    // then it is a REMOVE (but we will need to put that in the graph first)
                    secondNode = secondGraph.ImportNode(firstNode);
                    secondNode.AddCategory(removedCategory);

                    FoundDiffEventArgs diffNode = new FoundDiffEventArgs(secondNode, DiffType.Remove);
                    FireDiffEvent(diffNode);
                }
                // If the second graph does have the node
                // then it might be a MODIFY
                // else it is an EQUAL
                else
                {
                    bool catsAreEqual = DiffCategoriesOnGraphObject(firstNode, secondNode);
                    bool propsAreEqual = DiffPropertiesOnNode(firstNode, secondNode);

                    // either the properties or the categories were not equal 
                    // then the nodes are not equal
                    if (!catsAreEqual || !propsAreEqual)
                    {
                        FoundDiffEventArgs diffNode = new FoundDiffEventArgs(secondNode, DiffType.Modify);
                        FireDiffEvent(diffNode);
                    }
                }
            }

            // if the first graph does NOT have a node the second graph does
            // ==> "ADD"
            foreach (GraphNode secondGraphNode in secondGraph.Nodes)
            {
                if (firstGraph.Nodes.Get(secondGraphNode.Id) == null)
                {
                    secondGraphNode.AddCategory(addedCategory);

                    FoundDiffEventArgs diffNode = new FoundDiffEventArgs(secondGraphNode, DiffType.Add);
                    FireDiffEvent(diffNode);
                }
            }

        }

        #endregion

        #region Diff Links

        private void DiffLinks(Graph firstGraph, Graph secondGraph)
        {
            foreach (GraphLink firstLink in firstGraph.Links)
            {
                GraphLink secondLink = secondGraph.Links.Get(firstLink.Source.Id, firstLink.Target.Id);

                if (secondLink != null)
                {
                    bool areCatsSame = DiffCategoriesOnGraphObject(firstLink, secondLink);

                    if (!areCatsSame)
                    {
                        FoundDiffEventArgs diffLink = new FoundDiffEventArgs(secondLink, DiffType.Modify);
                        FireDiffEvent(diffLink);
                    }
                }
                // could not find the link. Assume delete (even though it may have been "retargeted)"
                else
                {
                    secondLink = secondGraph.ImportLink(firstLink);
                    secondGraph.Links.Add(secondLink);
                    secondLink.AddCategory(removedCategory);

                    FoundDiffEventArgs diffLink = new FoundDiffEventArgs(secondLink, DiffType.Remove);
                    FireDiffEvent(diffLink);
                }
            }

            // Find the links that have been added to the second graph
            // ie. cannot find them in the first graph
            foreach (GraphLink secondLink in secondGraph.Links)
            {
                if (firstGraph.Links.Get(secondLink.Source.Id, secondLink.Target.Id) == null)
                {
                    secondLink.AddCategory(addedCategory);
                    FoundDiffEventArgs diffLink = new FoundDiffEventArgs(secondLink, DiffType.Add);
                    FireDiffEvent(diffLink);
                }
            }
        }

        #endregion 

        #region Diff Groups

        Dictionary<GraphNode, Group> groups = new Dictionary<GraphNode, Group>();

        private void FindGroups(Graph graph)
        {
            foreach (GraphLink link in graph.Links)
            {
                if (link.IsContainment && link.Source.Visibility == System.Windows.Visibility.Visible && link.Target.Visibility == System.Windows.Visibility.Visible && link.Source.IsGroup)
                {
                    GraphNode n = link.Source;
                    Group g = null;
                    if (!groups.TryGetValue(n, out g))
                    {
                        g = new Group() { Node = n };
                        groups[n] = g;
                    }
                    g.AddChild(link.Target);
                }
            }

            // fix up parents
            foreach (Group g in groups.Values)
            {
                foreach (GraphNode n in g.Children)
                {
                    Group child = null;
                    if (groups.TryGetValue(n, out child))
                    {
                        child.Parent = g;
                    }
                }
            }
        }

        private void PropagateGroupDiff()
        {
            HashSet<Group> visited = new HashSet<Group>();
            foreach (Group g in groups.Values)
            {
                int added = 0;
                int removed = 0;
                int nodes = 0;
                bool groupAdded = g.Node.HasCategory(addedCategory);
                bool groupRemoved = g.Node.HasCategory(removedCategory);
                ComputeGroupDiff(g, groupAdded, groupRemoved, visited, ref added, ref removed, ref nodes);                
            }
        }

        private void ComputeGroupDiff(Group g, bool groupAdded, bool groupRemoved, HashSet<Group> visited, ref int added, ref int removed, ref int nodes)
        {
            if (visited.Contains(g))
            {
                return;
            }
            visited.Add(g);
            groupAdded = groupAdded | g.Node.HasCategory(addedCategory);
            groupRemoved = groupRemoved | g.Node.HasCategory(removedCategory);

            foreach (GraphNode c in g.Children)
            {
                nodes++;
                if (groupAdded || c.HasCategory(addedCategory))
                {
                    added++;
                }
                else if (groupRemoved || c.HasCategory(removedCategory))
                {
                    removed++;
                }

                Group child = null;
                if (groups.TryGetValue(c, out child))
                {
                    int tempadded = 0;
                    int tempremoved = 0;
                    int tempnodes = 0;
                    ComputeGroupDiff(child, groupAdded, groupRemoved, visited, ref tempadded, ref tempremoved, ref tempnodes);
                    added += tempadded;
                    removed += tempremoved;
                    nodes += tempnodes;
                }
            }
            if (added != 0 || removed != 0)
            {
                g.Node.AddCategory(partialCategory);
                g.Node.SetValue<int>(childrenAdded, (added * 100) / nodes);
                g.Node.SetValue<int>(childrenRemoved, (removed * 100) / nodes);
            }
        }


        class Group
        {
            public Group Parent { get; set; }
            public GraphNode Node { get; set; }
            public List<GraphNode> Children = new List<GraphNode>();
            public void AddChild(GraphNode child)
            {
                Children.Add(child);
            }
        }

        #endregion

        #region Diff Categories

        private bool DiffCategoriesOnGraphObject(GraphObject firstNode, GraphObject secondNOde)
        {
            bool isEqual = true;

            // Find the deleted categories
            foreach (GraphCategory catOnFirstNode in firstNode.Categories)
            {
                if (secondNOde.HasCategory(catOnFirstNode))
                {
                    continue;
                }
                else
                {
                    // Fire category deleted event.
                    FoundDiffCategoryEventArgs diffCat = new FoundDiffCategoryEventArgs(secondNOde, catOnFirstNode, DiffType.Remove);
                    FireDiffCategoryEvent(diffCat);

                    isEqual = false;
                }
            }

            // Find the added categories
            foreach (GraphCategory catOnSecondNode in secondNOde.Categories)
            {
                if (firstNode.HasCategory(catOnSecondNode))
                {
                    continue;
                }
                else
                {
                    // Fire category added event.
                    FoundDiffCategoryEventArgs diffCat = new FoundDiffCategoryEventArgs(secondNOde, catOnSecondNode, DiffType.Add);
                    FireDiffCategoryEvent(diffCat);
                    isEqual = false;
                }
            }


            if (isEqual)
            {
                // do nothing.
            }
            else
            {
                secondNOde.AddCategory(modifiedCategory);
            }

            return isEqual;
        }
        #endregion 

        #region Diff Properties

        private bool DiffPropertiesOnNode(GraphNode firstNode, GraphNode secondNode)
        {
            bool nodesAreEqual = true;
            Graph g1 = firstNode.Owner;
            Graph g2 = secondNode.Owner;

            // First check that a property on the first node exists on the second node.
            foreach (KeyValuePair<GraphProperty, object> propOnFirstNodeKeyValue in firstNode.Properties)
            {
                GraphProperty propOnFirstNode = propOnFirstNodeKeyValue.Key;
                GraphMetadata propOnFirstNodeMetadata = propOnFirstNode.GetMetadata(g1);

                if (!propOnFirstNodeMetadata.IsSharable || !propOnFirstNodeMetadata.IsSerializable || IgnoreProperties.Contains(propOnFirstNode.Id))
                {
                    // skip non-serializable properties.
                    continue;
                }

                if (secondNode.HasValue(propOnFirstNode))
                {
                    // The property exists in the secodn node, now we need to check the contents of the property
                    object secondValue = secondNode.GetValue(propOnFirstNode) ?? "";

                    // CASE: MODIFIED PROPERTY
                    if (propOnFirstNodeKeyValue.Value == secondValue) 
                    {
                        // special case to avoid expensive ToString.
                    }
                    else if (propOnFirstNodeKeyValue.Value.ToString() != secondValue.ToString())
                    {
                        // Fire modified property event
                        FoundDiffPropertyEventArgs diffProp = new FoundDiffPropertyEventArgs(firstNode, secondNode, propOnFirstNodeKeyValue.Key, DiffType.Modify);
                        FireDiffPropertyEvent(diffProp);
                        nodesAreEqual = false;
                        break;
                    }
                }
                else
                {
                    // CASE: DELETED PROPERTY If the property was not found on the second node - its an deleted property.                
                    // Fire delete proerty event (since property was not found on second node)
                    FoundDiffPropertyEventArgs diffProp = new FoundDiffPropertyEventArgs(firstNode, secondNode, propOnFirstNodeKeyValue.Key, DiffType.Remove);
                    FireDiffPropertyEvent(diffProp);
                    nodesAreEqual = false;
                    break;
                }

            }


            // CASE: ADDED PROPERTY : second check that a property on the second node exists on the first node.
            foreach (KeyValuePair<GraphProperty, object> propOnSecondNodeKeyValue in secondNode.Properties)
            {
                GraphProperty propOnSecondNode = propOnSecondNodeKeyValue.Key;
                GraphMetadata propOnSecondNodeMetadata = propOnSecondNode.GetMetadata(g1);

                if (!propOnSecondNodeMetadata.IsSharable || !propOnSecondNodeMetadata.IsSerializable || IgnoreProperties.Contains(propOnSecondNode.Id))
                {
                    // skip non-serializable properties.
                    continue;
                }
                if (!firstNode.HasValue(propOnSecondNode)) 
                {
                    nodesAreEqual = false;
                    // fire add property event.
                    // Fire modified property event
                    FoundDiffPropertyEventArgs diffProp = new FoundDiffPropertyEventArgs(firstNode, secondNode, propOnSecondNodeKeyValue.Key, DiffType.Add);
                    FireDiffPropertyEvent(diffProp);
                }
            }

            // 
            if (nodesAreEqual)
            {
                // do nothing
            }
            else
            {
                secondNode.AddCategory(modifiedCategory);
            }
            return nodesAreEqual;
        }


        #endregion

        #region Events

        public event EventHandler<FoundDiffEventArgs> FoundDifferentNodeEvent;
        public event EventHandler<FoundDiffPropertyEventArgs> FoundDifferentPropertyEvent;
        public event EventHandler<FoundDiffCategoryEventArgs> FoundDifferentCategoryEvent;


        private void FireDiffEvent(FoundDiffEventArgs arguments)
        {
            if (this.FoundDifferentNodeEvent != null)
            {
                FoundDifferentNodeEvent(this, arguments);
            }
        }

        private void FireDiffCategoryEvent(FoundDiffCategoryEventArgs arguments)
        {
            if (this.FoundDifferentCategoryEvent != null)
            {
                FoundDifferentCategoryEvent(this, arguments);
            }
        }

        private void FireDiffPropertyEvent(FoundDiffPropertyEventArgs arguments)
        {
            if (this.FoundDifferentPropertyEvent != null)
            {
                FoundDifferentPropertyEvent(this, arguments);
            }
        }

        #endregion 
    }


}
