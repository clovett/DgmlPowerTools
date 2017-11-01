using Microsoft.VisualStudio.GraphModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LovettSoftware.DgmlPowerTools
{
    public class GroupViewModel
    {
        public const string NewItemCaption = "<new>";
        public const string NewItemExpression = "<expr>";

        Graph graph;
        ObservableCollection<GroupItemViewModel> items = new ObservableCollection<GroupItemViewModel>();

        public ObservableCollection<GroupItemViewModel> Items { get { return items;  } }

        internal void AddNewItem()
        {
            this.items.Add(new GroupItemViewModel() { Label = NewItemCaption, Expression = NewItemExpression });
        }

        internal void SetGraph(Graph graph)
        {
            this.groupIndex = null;
            this.allGroupedNodes = null;
            this.graph = graph;

            this.items.Clear();

            List<GroupItemViewModel> list = new List<GroupItemViewModel>();

            foreach (GraphGroup group in graph.Groups)
            {
                string e = group.GetValue<string>("Expression");
                if (e != null)
                {
                    list.Add(new GroupItemViewModel() { Label = group.Label, Expression = e, Priority = group.GetValue<int>("Priority") });
                }
            }

            list.Sort(new Comparison<GroupItemViewModel>((a, b) =>
            {
                return a.Priority - b.Priority;
            }));

            foreach (var item in list)
            {
                this.items.Add(item);
            }

        }


        internal void ApplyGroups()
        {
            using (GraphTransactionScope scope = new UndoableGraphTransactionScope(UndoOption.Disable))
            {
                CreateGroups();
                scope.Complete();
            }
        }

        private string GetUniqueNodeId(string baseName)
        {
            int index = 0;
            while (true)
            {
                string name = (index == 0) ? baseName : baseName + index;
                GraphNode node = graph.Nodes.Get(name);
                if (node == null)
                {
                    return name;
                }
                index++;
            }
        }

        Dictionary<GraphNode, HashSet<GraphNode>> groupIndex;
        HashSet<GraphNode> allGroupedNodes;

        private void CreateGroups()
        {
            if (graph == null)
            {
                return;
            }
            groupIndex = new Dictionary<GraphNode, HashSet<GraphNode>>();
            allGroupedNodes = new HashSet<GraphNode>();
            RemoveGroups();
            int priority = 0;
            foreach (var item in this.Items)
            {
                if (!string.IsNullOrEmpty(item.Expression) && item.Label != NewItemCaption)
                {
                    string groupId = GetUniqueNodeId(item.Label);
                    GraphNode node = graph.Nodes.GetOrCreate(groupId, item.Label, null);
                    node.SetValue(GroupViewModelSchema.ExpressionProperty, item.Expression);
                    node.SetValue<int>(GroupViewModelSchema.GroupPriority, priority);
                    node.SetValue<GraphGroupStyle>(GraphCommonSchema.Group, GraphGroupStyle.Expanded);
                    foreach (var term in item.Expression.Split(','))
                    {
                        string st = term.Trim();
                        if (!string.IsNullOrEmpty(st))
                        {
                            GroupNodesMatching(node, st);
                        }
                    }
                }
                priority++;
            }

            SimpleExpandGroups();
        }

        internal void RemoveGroups()
        {
            if (graph == null)
            {
                return;
            }
            using (GraphTransactionScope scope = new UndoableGraphTransactionScope(UndoOption.Disable))
            {
                foreach (GraphNode node in graph.Nodes.ToArray())
                {
                    if (node.HasValue(GraphCommonSchema.Group))
                    {
                        graph.Nodes.Remove(node);
                    }
                }
                scope.Complete();
            }
        }

        private void SimpleExpandGroups()
        {
            // pull into any group any node that is only linked to other nodes in that group.
            bool converged = false;
            while (!converged)
            {
                converged = true;
                foreach (var pair in groupIndex)
                {
                    GraphNode group = pair.Key;
                    HashSet<GraphNode> children = pair.Value;

                    foreach (GraphNode node in graph.Nodes.ToArray())
                    {
                        if (node.Label == "asynccancellation.h" && group.Label == "Media")
                        {
                            Debug.WriteLine("debug");
                        }
                        bool include = false;
                        bool exclude = false;
                        if (!allGroupedNodes.Contains(node) && !node.IsGroup)
                        {
                            foreach (GraphLink link in node.OutgoingLinks)
                            {
                                if (link.Target.IsGroup || (!children.Contains(link.Target) && allGroupedNodes.Contains(node)))
                                {
                                    // this link points to a node inside another group.
                                    exclude = true;
                                    break;
                                }
                                else if (children.Contains(link.Target))
                                {
                                    include = true;
                                }
                            }
                            if (!exclude)
                            {
                                foreach (GraphLink link in node.IncomingLinks)
                                {
                                    if (link.Source.IsGroup || (!children.Contains(link.Source) && allGroupedNodes.Contains(node)))
                                    {
                                        // this link comes from a node inside another group.
                                        exclude = true;
                                        break;
                                    }
                                    else if (children.Contains(link.Source))
                                    {
                                        include = true;
                                    }
                                }
                            }
                            if (include && !exclude)
                            {
                                converged = false; // need another iteration to do full functional closure.
                                graph.Links.GetOrCreate(group, node, null, GraphCommonSchema.Contains);
                                allGroupedNodes.Add(node);
                                children.Add(node);
                            }
                        }
                    }
                }
            }
        }

        private void GroupNodesMatching(GraphNode group, string term)
        {
            if (graph == null)
            {
                return;
            }

            HashSet<GraphNode> children = null;

            if (!groupIndex.TryGetValue(group, out children))
            {
                children = new HashSet<GraphNode>();
                groupIndex[group] = children;
            }

            foreach (GraphNode node in graph.Nodes)
            {
                string label = node.Label;
                if (!node.IsGroup && !allGroupedNodes.Contains(node))
                {
                    if (label.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        allGroupedNodes.Add(node);
                        children.Add(node);
                        graph.Links.GetOrCreate(group, node, null, GraphCommonSchema.Contains);
                    }
                }
            }
        }


    }


    /// <summary>
    /// This is a DGML schema which defines GraphProperty objects that are 
    /// used by the GroupViewModel to remember the grouping expressions.
    /// </summary>
    public class GroupViewModelSchema
    {
        /// <summary>
        /// Instance of the GraphSchema
        /// </summary>
        public static GraphSchema Schema;

        static GroupViewModelSchema()
        {
            Schema = new GraphSchema("GroupViewModelSchema");

            ExpressionProperty = Schema.Properties.AddNewProperty("Expression", typeof(string));
            GroupPriority = Schema.Properties.AddNewProperty("Priority", typeof(int));
        }

        /// <summary>
        /// This property can be used on a group to remember the expression for this group.
        /// </summary>
        public static GraphProperty ExpressionProperty;

        /// <summary>
        /// This property can be used on a group to remember where it comes in the list.
        /// </summary>
        public static GraphProperty GroupPriority;
    }

    public class GroupItemViewModel : INotifyPropertyChanged
    {
        string label;
        string expression;
        int priority;

        public string Expression
        {
            get { return expression; }
            set {
                if (expression != value)
                {
                    expression = value;
                    OnPropertyChanged("Expression");
                }
            }
        }


        public string Label
        {
            get { return label; }
            set
            {
                if (label != value)
                {
                    label = value;
                    OnPropertyChanged("Label");
                }
            }
        }

        public int Priority
        {
            get { return priority; }
            set
            {
                if (priority != value)
                {
                    priority = value;
                    OnPropertyChanged("Priority");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

    }

}
