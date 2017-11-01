using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.VisualStudio.GraphModel;
using Microsoft.VisualStudio.GraphModel.Styles;
using System.ComponentModel;
using Microsoft.VisualStudio.Diagrams.Gestures;
using Microsoft.VisualStudio.Diagrams.Selection;
using Microsoft.VisualStudio.Diagrams.View;
using Microsoft.VisualStudio.Progression;

namespace LovettSoftware.DgmlPowerTools
{
    /// <summary>
    /// This class manages the neighborhood mode 
    /// </summary>
    public class NeighborhoodAnalyzer : INotifyPropertyChanged
    {
        #region Fields
        ISelectionSet<GraphObject> _selection;

        Graph _graph;

        /// <summary>
        /// determines if we are in Neighborhood browse mode or not
        /// </summary>
        bool _neighborhoodBrowseMode;

        /// <summary>
        /// Neighborhood distance 
        /// </summary>
        int _neighborhoodDistance = 1;

        /// <summary>
        /// Determines if we are showing the butterfly view
        /// </summary>
        bool _butterflyMode;

        HashSet<GraphObject> _hidden = new HashSet<GraphObject>();

        HashSet<GraphNode> _roots = new HashSet<GraphNode>();

        bool _cancelled;

        #endregion 
        
        public NeighborhoodAnalyzer(ISelectionSet<GraphObject> selection)
        {
            _selection = selection;
            IsEnabled = true;
        }

        public void Cancel()
        {
            _cancelled = true;
        }

        public Graph Graph { 
            get { return _graph; } 
            set { _graph = value; DeserializeState(); } 
        }

        public bool IsEnabled { get; set; }

        /// <summary>
        /// Turn neighborhood mode on if it is off, or "recenter" the neighborhood around the
        /// new selection if neighborhood is already on.
        /// </summary>
        public void ToggleNeighborhoodBrowseMode()
        {
            if (!UsingNeighborhoodBrowseMode)
            {
                UsingNeighborhoodBrowseMode = true;
            }
            else
            {
                UpdateNeighborhood();                
            }
        }

        /// <summary>
        /// Returns if we are in Neighborhood browse mode
        /// </summary>
        public bool UsingNeighborhoodBrowseMode
        {
            get { return _neighborhoodBrowseMode; }
            set {
                if (_neighborhoodBrowseMode != value)
                {
                    _neighborhoodBrowseMode = value;
                    // Make this a non-undoable transaction so that it does not dirty the document.
                    using (var scope = new UndoableGraphTransactionScope(UndoOption.Disable))
                    {
                        if (UsingNeighborhoodBrowseMode)
                        {
                            _graph.SetNeighborhoodDistance(_neighborhoodDistance);
                        }
                        else
                        {
                            _graph.ClearValue(NeighborhoodSchema.NeighborhoodDistance);
                        }
                        UpdateNeighborhood();
                        scope.Complete();
                    }
                    OnPropertyChanged("UsingNeighborhoodBrowseMode");
                }
            }
        }

        /// <summary>
        /// get or set the current Neighborhood distance
        /// </summary>
        public int NeighborhoodDistance
        {
            get
            {
                return _neighborhoodDistance;
            }
            set
            {
                if (_neighborhoodDistance != value)
                {
                    _neighborhoodDistance = value;
                    OnPropertyChanged("NeighborhoodDistance");
                }
                if (UsingNeighborhoodBrowseMode)
                {
                    using (var scope = new UndoableGraphTransactionScope(UndoOption.Disable))
                    {
                        UpdateNeighborhood();
                        scope.Complete();
                    }
                }
            }
        }


        /// <summary>
        /// Toggle the the butterfly mode on/off
        /// </summary>
        public void ToggleButterflyMode()
        {
            if (!UsingButterflyMode || !IsCenterChanged())
            {
                UsingButterflyMode = !UsingButterflyMode;                
            }

            UpdateNeighborhood();
        }

        /// <summary>
        /// Make sure we have at least one node centered.  If there's none then use the current selection.
        /// If there's no current selection, use the largest Hub.
        /// </summary>
        internal void UpdateCenter()
        {            
            if (!_selection.AsNodes().Any())
            {
                GraphNode hub = FindBiggestHub(Graph);
                if (hub != null)
                {
                    _selection.Add(hub);
                }
            }
            SetCenter(_selection.AsNodes());
        }

        private bool IsCenterChanged()
        {
            foreach (GraphNode n in _selection.AsNodes())
            {
                if (n.GetNeighborhoodCenter() == false)
                {
                    // center was changed!
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Find the node that has the most links (incoming + outgoing).
        /// </summary>
        public static GraphNode FindBiggestHub(Graph graph)
        {
            GraphNode result = null;
            int max = 0;
            foreach (GraphNode n in graph.VisibleNodes)
            {
                int count = 0;
                foreach (GraphLink link in n.AllLinks)
                {
                    if (link.Visibility == System.Windows.Visibility.Visible && link.Target.Visibility == System.Windows.Visibility.Visible)
                    {
                        count++;
                    }
                }
                if (count >= max)
                {
                    max = count;
                    result = n;
                }
            }
            return result;
        }

        /// <summary>
        /// Get current status of butterfly mode
        /// </summary>
        public bool UsingButterflyMode
        {
            get { return _butterflyMode; }
            set
            {
                if (_butterflyMode != value)
                {
                    _butterflyMode = value;
                    // Make this a non-undoable transaction so that it does not dirty the document.
                    using (var scope = new UndoableGraphTransactionScope(UndoOption.Disable))
                    {
                        _graph.SetButterflyMode(_butterflyMode);
                        scope.Complete();
                    }
                    OnPropertyChanged("UsingButterflyMode");
                }
            }
        }

        /// <summary>
        /// Restore the neighborhood analyzer state from a Graph object.
        /// This is potentially slow on a large graph so it should only be done when the Graph object is changed.
        /// </summary>
        public void DeserializeState()
        {            
            ResetHiddenNodes();
            if (_graph != null)
            {
                _butterflyMode = _graph.GetButterflyMode();
                _neighborhoodBrowseMode = false;
                _neighborhoodDistance = 1;

                if (_graph.HasValue(NeighborhoodSchema.NeighborhoodDistance))
                {
                    int distance = _graph.GetNeighborhoodDistance();
                    if (distance > 0 && distance < 6)
                    {
                        _neighborhoodBrowseMode = true;
                        _neighborhoodDistance = distance;
                    }
                }

                // Find the new center nodes
                SetCenter(_graph.Nodes.GetByProperty(NeighborhoodSchema.NeighborhoodCenter, true));
            }
            else
            {
                SetCenter(new GraphNode[0]);
            }
        }

        /// <summary>
        /// Recompute the visible neighborhood given the current selection state and the neighborhood
        /// distance and/or butterfly mode.  
        /// </summary>
        public void UpdateNeighborhood()
        {
            _cancelled = false;
            using (GraphTransactionScope scope = new UndoableGraphTransactionScope(UndoOption.Disable))
            {
                UpdateCenter();

                if ((UsingNeighborhoodBrowseMode || UsingButterflyMode) && IsEnabled && _roots.Count > 0)
                {
                    ComputeNeighborhood();

                    AddNeighborhoodStyle();
                }
                else if (!UsingNeighborhoodBrowseMode && !UsingButterflyMode)
                {
                    RemoveNeighborhoodStyle();
                    SetCenter(new GraphNode[0]);
                }

                scope.Complete();
            }
            OnNeighborhoodChanged();
        }

        public event EventHandler NeighborhoodChanged;

        private void OnNeighborhoodChanged()
        {
            if (NeighborhoodChanged != null)
            {
                NeighborhoodChanged(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Get the current center nodes.
        /// </summary>
        public IEnumerable<GraphNode> GetCenter()
        {
            return _roots;
        }

        /// <summary>
        /// Set the center of the neighborhood.
        /// </summary>
        /// <param name="roots"></param>
        public void SetCenter(IEnumerable<GraphNode> roots)
        {
            ResetHiddenNodes();

            // reset previous roots
            foreach (GraphNode n in _roots)
            {
                n.SetNeighborhoodCenter(false);
            }
            _roots.Clear();
            foreach (GraphNode n in roots)
            {
                GraphNode rootNode = n;

                // is root a pseudo/surrogate?
                if (rootNode.HasValue(DgmlProperties.SurrogateOf))
                {
                    GraphNodeId realId = rootNode.GetValue<GraphNodeId>(DgmlProperties.SurrogateOf);
                    GraphNode realNode = _graph.Nodes.Get(realId);
                    if (realNode != null)
                    {
                        rootNode = realNode;
                    }
                }

                rootNode.SetNeighborhoodCenter(true);
                _roots.Add(rootNode);
            }
        }

        /// <summary>
        /// finds a style with given expression
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        GraphConditionalStyle FindNeighborhoodStyle(Type targetType, string expression)
        {
            Graph graph = this.Graph;
            foreach (GraphConditionalStyle style in graph.Styles)
            {
                if (style.TargetType == targetType)
                {
                    foreach (GraphCondition c in style.Conditions)
                    {
                        if (c.Expression == expression)
                        {
                            return style;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Remove the neighborhood style.
        /// </summary>
        void RemoveNeighborhoodStyle()
        {
            Graph graph = this.Graph;
            GraphConditionalStyle style = FindNeighborhoodStyle(typeof(GraphNode), "HasValue('NeighborhoodCenter')");
            if (style != null)
            {
                graph.Styles.Remove(style);
            }
        }

        /// <summary>
        /// Add the neighborhood style.
        /// </summary>
        void AddNeighborhoodStyle()
        {
            Graph graph = this.Graph;
            GraphConditionalStyle style = FindNeighborhoodStyle(typeof(GraphNode), "HasValue('NeighborhoodCenter')");
            if (style == null)
            {
                style = new GraphConditionalStyle(graph);
                style.TargetType = typeof(GraphNode);
                style.GroupLabel = Resources.NeighborhoodCenterLegendLabel;
                style.ValueLabel = "True";
                style.Conditions.Add(new GraphCondition(style) { Expression = "HasValue('NeighborhoodCenter')" });
                style.Setters.Add(new GraphSetter(style, "Stroke") { Value = "#C0A0B862" });
                style.Setters.Add(new GraphSetter(style, "StrokeThickness") { Value = "4" });
                graph.Styles.Add(style);
            }
        }

        /// <summary>
        /// Hide the given graph object and remember we have hidden it.
        /// </summary>
        /// <param name="graphObject"></param>
        void Hide(GraphObject graphObject, bool hideGroup)
        {
            if (!_hidden.Contains(graphObject))
            {
                graphObject.SetIsButterflyHidden(true);
                graphObject.Visibility = Visibility.Hidden;
                _hidden.Add(graphObject);

                GraphNode node = graphObject as GraphNode;
                if (node != null && node.IsGroup && hideGroup)
                {
                    GraphGroup g = Graph.FindGroup(node);
                    if (g != null)
                    {
                        HideChildren(g);
                    }
                }
            }
        }

        void HideChildren(GraphGroup g)
        {
            foreach (GraphNode n in g.ChildNodes)
            {
                Hide(n, true);
            }
            foreach (GraphGroup c in g.ChildGroups)
            {
                Hide(c.GroupNode, true);
            }
        }

        /// <summary>
        /// Prepare the roots by peeling them out of their containers.  This forces
        /// layout to create required arteries connecting the roots to other stuff 
        /// on the canvas.  We also check for deleted roots.
        /// </summary>
        public void PrepareRoots()
        {
            using (GraphTransactionScope scope = new UndoableGraphTransactionScope(UndoOption.Disable))
            {
                CreateLayers();

                foreach (GraphNode root in new HashSet<GraphNode>(_roots))
                {
                    if (!_graph.Nodes.Contains(root))
                    {
                        // This node has been removed from the graph, so it cannot be a root any more.
                        _roots.Remove(root);
                    }
                }
                scope.Complete();
            }

        }

        /// <summary>
        /// Resets visibility of all the nodes we have hidden.
        /// </summary>
        public void ResetHiddenNodes()
        {            
            foreach (GraphObject g in _hidden)
            {
                g.SetIsButterflyHidden(false);
                g.Visibility = Visibility.Visible;
            }
            _hidden.Clear();
           
            if (_layerMap != null)
            {
                foreach (GraphNode n in _layerMap.Keys)
                {
                    n.ClearValue(NeighborhoodSchema.Layer);
                }
            }
        }

        /// <summary>
        /// We organize incoming and outgoing nodes into layers (1 layer per link level) 
        /// </summary>
        Dictionary<GraphNode, int> _layerMap;

        void ThrowIfCancelled()
        {
            if (_cancelled)
            {
                throw new OperationCanceledException();
            }
        }

        /// <summary>
        /// Compute the neighborhood and create the butterfly if in butterfly mode then also hide
        /// anything not in the neighborhood.
        /// </summary>
        /// <returns></returns>
        void ComputeNeighborhood()
        {           
            CreateLayers();
            ThrowIfCancelled();
            if (UsingButterflyMode)
            {
                CreateButterfly();
            }
            ThrowIfCancelled();
            PopulateGroups();
            ThrowIfCancelled();
            HideNodes();            
        }

        /// <summary>
        /// Create the _layerMap according to distance from the center nodes.
        /// </summary>
        private void CreateLayers()
        {
            _layerMap = new Dictionary<GraphNode, int>();

            foreach (GraphNode n in _roots)
            {           
                n.SetLayer(0);
                _layerMap[n] = 0;
            }

            foreach (GraphNode n in _roots)
            {                
                PopulateOutgoingLayers(n.OutgoingLinks, 1, new HashSet<GraphNode>());
                // If we're not using butterfly mode, then just create a "distance" map where incoming are
                // treated the same as outgoing.
                int start = this.UsingButterflyMode ? -1 : 1;
                int direction = start;
                PopulateIncomingLayers(n, n.IncomingLinks, start, direction, new HashSet<GraphNode>());
            }
        }
        
        /// <summary>
        /// Now, hide all nodes that are not in the layer map.
        /// </summary>
        /// <returns></returns>
        private int HideNodes()
        {
            int count = 0;
            // Now hide everything that is not in the neighborhood
            foreach (GraphNode n in this.Graph.VisibleNodes)
            {
                if (!_layerMap.ContainsKey(n))
                {
                    Hide(n, true);
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Recompute the butterfly 
        /// </summary>
        public void ApplyButterfly()
        {
            using (GraphTransactionScope scope = new UndoableGraphTransactionScope(UndoOption.Disable))
            {
                ComputeNeighborhood();
                scope.Complete();
            }
        }

        /// <summary>
        /// The butterfly is created by removing links that do not connect adjacent layers.
        /// </summary>
        void CreateButterfly()
        {
            Graph g = Graph;
                
            foreach (KeyValuePair<GraphNode,int> pair in _layerMap)
            {
                int layer = pair.Value;
                GraphNode n = pair.Key;

                IEnumerable<GraphLink> incoming = n.IncomingLinks;
                IEnumerable<GraphLink> outgoing = n.OutgoingLinks;

                // Ok, now remove any incoming links that connect nodes that are not in the previous layer.
                foreach (GraphLink link in incoming)
                {
                    if (IsLinkInNeighborhood(link))
                    {
                        GraphNode source = link.Source;
                        int sourceLayer;
                        if (source != null && _layerMap.TryGetValue(source, out sourceLayer) && sourceLayer != layer - 1)
                        {
                            Hide(link, false);
                        }
                    }
                }


                // And remove any outgoing links that connect nodes that are not in the next layer.
                foreach (GraphLink link in outgoing)
                {
                    if (IsLinkInNeighborhood(link))
                    {
                        GraphNode target = link.Target;
                        int targetLayer;
                        if (target != null && _layerMap.TryGetValue(target, out targetLayer) && targetLayer != layer + 1)
                        {
                            Hide(link, false);
                        }
                    }
                }

            }
            
            // Remove severed nodes, find all nodes still connected to the roots and remove everything else.
            HashSet<GraphNode> connected = new HashSet<GraphNode>();
            foreach (GraphNode n in _roots)
            {
                connected.Add(n);

                // The reason we need a new hashset here is because this also acts as the 
                // 'visited' set for the FindOutgoing method.
                HashSet<GraphNode> outgoing = new HashSet<GraphNode>();
                FindOutgoing(n.OutgoingLinks, outgoing);
                connected.UnionWith(outgoing);

                HashSet<GraphNode> incoming = new HashSet<GraphNode>();
                FindIncoming(n.IncomingLinks, incoming);
                connected.UnionWith(incoming);
            }

            HashSet<GraphNode> toRemove = new HashSet<GraphNode>();
            foreach (KeyValuePair<GraphNode, int> pair in _layerMap)
            {
                GraphNode n = pair.Key;
                if (!connected.Contains(n))
                {
                    toRemove.Add(n);
                } 
            }
            foreach (GraphNode n in toRemove)
            {
                _layerMap.Remove(n);
            }
        }

        void FindOutgoing(IEnumerable<GraphLink> links, HashSet<GraphNode> connected)
        {
            Graph g = Graph;
            foreach (GraphLink link in links)
            {
                if (IsLinkInNeighborhood(link))
                {
                    GraphNode target = link.Target;
                    if (target != null && !connected.Contains(target) && _layerMap.ContainsKey(target))
                    {
                        connected.Add(target);
                        FindOutgoing(target.OutgoingLinks, connected);
                    }
                }
            }
        }

        void FindIncoming(IEnumerable<GraphLink> links, HashSet<GraphNode> connected)
        {
            Graph g = Graph;
            foreach (GraphLink link in links)
            {
                if (IsLinkInNeighborhood(link))
                {
                    GraphNode source = link.Source;
                    if (source != null && !connected.Contains(source) && _layerMap.ContainsKey(source))
                    {
                        connected.Add(source);
                        FindIncoming(source.IncomingLinks, connected);
                    }
                }
            }
        }

        /// <summary>
        /// We have to also populate the parent groups of the nodes we have selected to be in the neighborhood.
        /// </summary>
        void PopulateGroups()
        {
            // If this node is in a collapsed group then we need to add the parent groups up to the first visible node.
            foreach (KeyValuePair<GraphNode, int> pair in new Dictionary<GraphNode, int>(_layerMap))
            {
                GraphNode n = pair.Key;
                int layer = pair.Value;
                AddParents(n, layer, new HashSet<GraphNode>());
            }
        }

        void AddParents(GraphNode n, int layer, HashSet<GraphNode> visited)
        {
            visited.Add(n);
            foreach (GraphGroup parent in n.ParentGroups)
            {
                GraphNode gn = parent.GroupNode;
                if (!_layerMap.ContainsKey(gn))
                {
                    _layerMap[gn] = layer;
                }
                if (!visited.Contains(gn))
                {
                    AddParents(gn, layer, visited);
                }
            }
        }

        /// <summary>
        /// Return true if the link should be considered in the neighborhood view
        /// </summary>
        bool IsLinkInNeighborhood(GraphLink link)
        {
            if (!link.IsVisible() || link.IsChildLink)
                return false;

            return true;
        }

        /// <summary>
        /// Populate all outgoing layers by traversing all the outgoing links and assign them to layer+1, +2, +3, etc.
        /// </summary>
        void PopulateOutgoingLayers(IEnumerable<GraphLink> links, int layer, HashSet<GraphNode> visited)
        {
            Graph g = Graph;
            foreach (GraphLink link in links)
            {
                if (IsLinkInNeighborhood(link))
                {
                    GraphNode target = link.Target;

                    if (target != null && !visited.Contains(target))
                    {
                        visited.Add(target);
                        int targetLayer;
                        if (!_layerMap.TryGetValue(target, out targetLayer) || Math.Abs(targetLayer) > Math.Abs(layer))
                        {
                            _layerMap[target] = layer;
                            if ((!_neighborhoodBrowseMode || layer + 1 <= _neighborhoodDistance) && target.OutgoingLinkCount > 0)
                            {
                                PopulateOutgoingLayers(target.OutgoingLinks, layer + 1, visited);
                            }
                        }
                        visited.Remove(target);
                    }
                }
                ThrowIfCancelled();
            }
        }

        /// <summary>
        /// Populate all incoming layers by traversing all the incoming links and assign them to layer-1,-2,-3,etc.
        /// NOTE: if we are not in butterfly mode then we instead just add to layer+1,+2,+3 and so on because "neighborhood" is
        /// undirected in that case.
        /// </summary>
        void PopulateIncomingLayers(GraphNode targetNode, IEnumerable<GraphLink> links, int layer, int direction, HashSet<GraphNode> visited)
        {
            Graph graph = Graph;
            List<GraphLink> linksList = new List<GraphLink>(links);

            foreach (GraphLink link in linksList)
            {
                ThrowIfCancelled();
                if (IsLinkInNeighborhood(link))
                {
                    GraphNode source = link.Source;
                    if (source != null && !visited.Contains(source))
                    {
                        int sourceLayer;

                        // remember we visited this node
                        GraphNode originalSource = source;
                        visited.Add(source);

                        bool keepGoing = (!_neighborhoodBrowseMode || Math.Abs(layer + direction) <= _neighborhoodDistance) && source.IncomingLinkCount > 0;

                        if (source == originalSource || !visited.Contains(source))
                        {
                            visited.Add(source);

                            // traverse deeper into the incoming layers, as needed
                            // make sure we have not already visited the Cloned node.
                            if (!_layerMap.TryGetValue(source, out sourceLayer) || Math.Abs(sourceLayer) >= Math.Abs(layer))
                            {
                                _layerMap[source] = layer;
                                if (keepGoing)
                                {
                                    PopulateIncomingLayers(source, source.IncomingLinks, layer + direction, direction, visited);
                                }
                            }                       
                            // removed cloned source.
                            visited.Remove(source);
                        }

                        // remove original source 
                        visited.Remove(originalSource);
                    }
                }
            }
        }

        void CopySpecialLinkProperties(GraphLink original, GraphLink copy)
        { 
            // Copy the link weight over so that it looks the same, but avoid other properties that are problematic here, like IsHidden.
            if (original.HasValue(VisualGraphProperties.Weight))
            {
                copy.SetWeight(original.GetWeight());
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
