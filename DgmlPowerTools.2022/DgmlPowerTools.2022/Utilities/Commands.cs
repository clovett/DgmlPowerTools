using Microsoft.VisualStudio.Diagrams.Gestures;
using Microsoft.VisualStudio.GraphModel;
using Microsoft.VisualStudio.GraphModel.Schemas;
using Microsoft.VisualStudio.Progression;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using System;
using System.ComponentModel.Design;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace LovettSoftware.DgmlPowerTools
{
    class Commands : IDisposable
    {
        protected IServiceProvider serviceProvider;

        public Commands(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
        }

        ~Commands()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            this.serviceProvider = null;
        }


        #region helpers

        protected T GetService<ST, T>()
            where ST : class
            where T : class
        {
            IServiceProvider sp = this.serviceProvider;
            if (typeof(T).IsEquivalentTo(typeof(IServiceProvider)))
            {
                return (T)sp;
            }
            return sp.GetService(typeof(ST)) as T;
        }

        protected T GetService<T>()
            where T : class
        {
            IServiceProvider sp = this.serviceProvider;
            if (typeof(T).IsEquivalentTo(typeof(IServiceProvider)))
            {
                return (T)sp;
            }
            return sp.GetService(typeof(T)) as T;
        }


        /// <summary>
        /// See help for method with signature of DefineCommandHandler(EventHandler, EventHandler, EventHandler, CommandID, string)
        /// </summary>
        internal OleMenuCommand DefineCommandHandler(EventHandler invokeHandler, CommandID id)
        {
            return DefineCommandHandler(invokeHandler, null, null, id, null);
        }

        /// <summary>
        /// See help for method with signature of DefineCommandHandler(EventHandler, EventHandler, EventHandler, CommandID, string)
        /// </summary>
        internal OleMenuCommand DefineCommandHandler(EventHandler invokeHandler, CommandID id, string parametersDescription)
        {
            return DefineCommandHandler(invokeHandler, null, null, id, parametersDescription);
        }

        /// <summary>
        /// See help for method with signature of DefineCommandHandler(EventHandler, EventHandler, EventHandler, CommandID, string)
        /// </summary>
        /// <param name="invokeHandler"></param>
        /// <param name="changeStatusHandler"></param>
        /// <param name="beforeQueryStatusHandler"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        internal OleMenuCommand DefineCommandHandler(EventHandler invokeHandler, EventHandler changeStatusHandler, EventHandler beforeQueryStatusHandler, CommandID id)
        {
            return DefineCommandHandler(invokeHandler, changeStatusHandler, beforeQueryStatusHandler, id, null);
        }

        /// <summary>
        /// Create the command for the command ID and associates the handler methods, if specified
        /// </summary>
        /// <param name="invokeHandler">Method that should be called to execute the command</param>
        /// <param name="changeStatusHandler">Method that should be called when the status of the command changes</param>
        /// <param name="beforeQueryStatusHandler">Method that should be called before VS queries for the status of the command</param>
        /// <param name="id">The Id of the command with which to associate the handlers</param>
        /// <param name="parametersDescription">The description of the parameters of the command (may be null)</param>
        /// <returns>The OleMenuCommand object representing the command and the handlers associated with the command</returns>
        internal OleMenuCommand DefineCommandHandler(EventHandler invokeHandler, EventHandler changeStatusHandler, EventHandler beforeQueryStatusHandler, CommandID id, string parametersDescription)
        {
            // Get the OleCommandService object provided by the MPF; this object is the one
            // responsible for handling the collection of commands implemented by the package.
            OleMenuCommandService menuService = GetService<IMenuCommandService, OleMenuCommandService>();

            OleMenuCommand command = null;
            if (null != menuService)
            {
                // Add the command handler
                command = new OleMenuCommand(invokeHandler, changeStatusHandler, beforeQueryStatusHandler, id);

                // Set the ParametersDescription property
                if (!String.IsNullOrEmpty(parametersDescription))
                {
                    command.ParametersDescription = parametersDescription;
                }

                menuService.AddCommand(command);
            }
            return command;
        }

        #endregion

    }

    class PackageCommands : Commands
    {
        public PackageCommands(IServiceProvider serviceProvider) : base(serviceProvider)
        {
            // filter view
            var commandId = new CommandID(GuidList.guidDgmlPowerToolsCmdSet, PkgCmdIDList.cmdidDgmlFilterView);
            DefineCommandHandler(new EventHandler(this.FilterView_InvokeHandler), null,
                new EventHandler(this.FilterView_BeforeQueryStatus), commandId, null);

            // graph project dependencies
            commandId = new CommandID(GuidList.guidDgmlPowerToolsCmdSet, PkgCmdIDList.cmdidGraphProjectDependencies);
            DefineCommandHandler(new EventHandler(this.GraphProjectDependencies_InvokeHandler), null,
                new EventHandler(this.GraphProjectDependencies_BeforeQueryStatus), commandId, null);

        }

        #region handlers

        private void FilterView_BeforeQueryStatus(object sender, EventArgs e)
        {
            OleMenuCommand oleMenuCommand = sender as OleMenuCommand;
            if (oleMenuCommand == null)
            {
                return;
            }
            // Set the settings
            oleMenuCommand.Supported = true;    // visible
            oleMenuCommand.Enabled = true;      // enabled or disabled (gray)
        }

        private void FilterView_InvokeHandler(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            // Get the command being executed
            OleMenuCommand oleMenuCommand = sender as OleMenuCommand;
            if (oleMenuCommand == null)
            {
                return;
            }
            ToolWindowPane pane = VSPackage.Instance.FindToolWindow(typeof(FilterViewToolWindow), 0, true);
            IVsWindowFrame window = pane.Frame as IVsWindowFrame;
            window.Show();
        }

        private void GraphProjectDependencies_BeforeQueryStatus(object sender, EventArgs e)
        {
            OleMenuCommand oleMenuCommand = sender as OleMenuCommand;
            if (oleMenuCommand == null)
            {
                return;
            }
            // Set the settings
            oleMenuCommand.Supported = true;    // visible
            oleMenuCommand.Enabled = true;      // enabled or disabled (gray)
        }

        private void GraphProjectDependencies_InvokeHandler(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            // Get the command being executed
            OleMenuCommand oleMenuCommand = sender as OleMenuCommand;
            if (oleMenuCommand == null)
            {
                return;
            }
            try
            {
                ProjectDependencies generator = (ProjectDependencies)this.serviceProvider.GetService(typeof(SProjectDependencies));
                Graph result = generator.GetProjectDependencies();

                string resultPath = System.IO.Path.GetTempPath();
                resultPath = System.IO.Path.Combine(resultPath, "projects.dgml");

                result.Save(resultPath);
                result = null;

                ShellHelpers.OpenDocument(serviceProvider, resultPath);
                IVsWindowPane pane = ShellHelpers.GetActiveDocumentWindowPane(serviceProvider);
                if (pane != null)
                {
                    IGraphDocumentWindowPane graphWindow = (IGraphDocumentWindowPane)pane;
                    Graph resultGraph = graphWindow.Graph;
                    // todo: check the graph is correct!
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error opening new graph: " + ex.Message, "Unhandled Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        #endregion
    }

    class WindowCommands : Commands
    {
        IGraphDocumentWindowPane graphWindow;
        NeighborhoodAnalyzer analyzer;

        public WindowCommands(IGraphDocumentWindowPane window) : base((IServiceProvider) window)
        {
            graphWindow = window;
            analyzer = new NeighborhoodAnalyzer(graphWindow.Selection);
            analyzer.Graph = window.Graph;
            analyzer.NeighborhoodChanged += new EventHandler(OnNeighborhoodChanged);
            window.GraphChanged += new EventHandler(OnGraphChanged);

            CommandID commandId;

            // compare graphs...
            commandId = new CommandID(GuidList.guidDgmlPowerToolsCmdSet, (int)PkgCmdIDList.cmdidCompareGraphs);
            DefineCommandHandler(new EventHandler(this.CompareGraphs_InvokeHandler), null,
                new EventHandler(this.CompareGraphs_BeforeQueryStatus), commandId, null);

            // hide internals
            commandId = new CommandID(GuidList.guidDgmlPowerToolsCmdSet, (int)PkgCmdIDList.cmdidHideInternals);
            DefineCommandHandler(new EventHandler(this.HideInternals_InvokeHandler), null,
                new EventHandler(this.HideInternals_BeforeQueryStatus), commandId, null);

            // cmdidSaveAsSvg
            commandId = new CommandID(GuidList.guidDgmlPowerToolsCmdSet, PkgCmdIDList.cmdidSaveAsSvg);
            DefineCommandHandler(new EventHandler(this.SaveAsSvg_InvokeHandler), null,
                new EventHandler(this.SaveAsSvg_BeforeQueryStatus), commandId, null);

            // BrowseMode
            commandId = new CommandID(GuidList.guidDgmlPowerToolsCmdSet, PkgCmdIDList.cmdIdGraph_Layout_NeighborhoodBrowseMode);
            DefineCommandHandler(new EventHandler(this.NeighborhoodBrowseMode_InvokeHandler), null,
                new EventHandler(this.NeighborhoodBrowseMode_BeforeQueryStatus), commandId, null);

            // Neighborhood Distance commands
            commandId = new CommandID(GuidList.guidDgmlPowerToolsCmdSet, PkgCmdIDList.cmdIdGraph_Layout_NeighborhoodDistance_Combo);
            DefineCommandHandler(new EventHandler(this.NeighborhoodDistanceCombo_InvokeHandler), commandId, "$");

            commandId = new CommandID(GuidList.guidDgmlPowerToolsCmdSet, PkgCmdIDList.cmdIdGraph_Layout_NeighborhoodDistance_ComboGetList);
            DefineCommandHandler(new EventHandler(this.NeighborhoodDistanceComboGetList_InvokeHandler), commandId);

            DefineCommandHandler(null, null, this.NeighborhoodBrowseMenu_BeforeQueryStatus,
                new CommandID(GuidList.guidDgmlPowerToolsCmdSet, PkgCmdIDList.menuID_NeighborhoodDistance));

            for (int i = PkgCmdIDList.cmdidNeighborhoodDistance1; i <= PkgCmdIDList.cmdidNeighborhoodDistanceAll; i++)
            {
                commandId = new CommandID(GuidList.guidDgmlPowerToolsCmdSet, i);
                DefineCommandHandler(new EventHandler(this.NeighborhoodDistance_InvokeHandler), null,
                    new EventHandler(this.NeighborhoodDistance_BeforeQueryStatus), commandId);
            }

            // Butterfly mode
            commandId = new CommandID(GuidList.guidDgmlPowerToolsCmdSet, PkgCmdIDList.cmdidButterflyMode);
            DefineCommandHandler(new EventHandler(this.ButterflyMode_InvokeHandler), null,
                new EventHandler(this.ButterflyMode_BeforeQueryStatus), commandId, null);

        }

        void OnNeighborhoodChanged(object sender, EventArgs e)
        {
            GraphControl control = this.GraphControl;
            if (control != null && control.Diagram != null)
            {
                control.Diagram.RedoLayout();
            }
        }

        void OnGraphChanged(object sender, EventArgs e)
        {
            analyzer.Graph = graphWindow.Graph;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (this.graphWindow != null)
            {
                this.graphWindow.GraphChanged -= new EventHandler(OnGraphChanged);
            }
            this.graphWindow = null;
            this.analyzer = null;
        }

        #region Helpers

        private bool HasSelectedNodes
        {
            get
            {                
                return graphWindow.Selection.AsNodes().Any();
            }
        }
        #endregion

        #region Handlers

        /// <summary>
        /// We have to respond to the menuID_NeighborhoodDistance in order to set the checked status on this menu
        /// so that when we are in neighborhood browse mode the drop down button shows a checked state.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arguments"></param>
        void NeighborhoodBrowseMenu_BeforeQueryStatus(object sender, EventArgs arguments)
        {
            OleMenuCommand oleMenuCommand = sender as OleMenuCommand;
            if (oleMenuCommand == null)
            {
                return;
            }
            bool isChecked = analyzer.UsingNeighborhoodBrowseMode;

            // Set the settings
            oleMenuCommand.Supported = true;    // visible
            oleMenuCommand.Enabled = true;      // enabled or disabled (gray)
            oleMenuCommand.Checked = isChecked;
        }

        /// <summary>
        /// Handles entering and exiting Neighborhood browse mode
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arguments"></param>
        void NeighborhoodBrowseMode_InvokeHandler(object sender, EventArgs arguments)
        {
            // Get the command being executed
            OleMenuCommand oleMenuCommand = sender as OleMenuCommand;
            if (oleMenuCommand == null)
            {
                return;
            }

            analyzer.ToggleNeighborhoodBrowseMode();            
        }

        /// <summary>
        /// determines current state of the UI control based on Neighborhood browse mode
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arguments"></param>
        private void NeighborhoodBrowseMode_BeforeQueryStatus(object sender, EventArgs arguments)
        {
            // Get the command being queried
            OleMenuCommand oleMenuCommand = sender as OleMenuCommand;
            if (oleMenuCommand == null)
            {
                return;
            }

            bool isChecked = analyzer.UsingNeighborhoodBrowseMode;

            // Set the settings
            oleMenuCommand.Supported = true;    // visible
            // Only enabled if we have a selection, or we are already in neighborhood mode.
            oleMenuCommand.Enabled = HasSelectedNodes || isChecked;
            oleMenuCommand.Checked = isChecked;
        }


        #endregion

        #region Neighborhood distance Commands

        /// <summary>
        /// Strings to place into the combo box dropdown
        /// </summary>
        private string[] NeighborhoodDistances = { Resources.NeighborhoodDistance_All, "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" };

        // DynamicCombo
        //   A DYNAMICCOMBO allows the user to type into the edit box or pick from the list. The 
        //	 list of choices is usually fixed and is managed by the command handler for the command.
        //
        //   A Combo box requires two commands:
        //     One command is used to ask for the current value of the combo box and to set the new value when the user
        //     makes a choice in the combo box.
        //
        //     The second command is used to retrieve this list of choices for the combo box.
        private void NeighborhoodDistanceCombo_InvokeHandler(object sender, EventArgs e)
        {
            if ((null == e) || (e == EventArgs.Empty))
            {
                // We should never get here; EventArgs are required.
                return;
            }

            OleMenuCmdEventArgs eventArgs = e as OleMenuCmdEventArgs;

            if (eventArgs != null)
            {
                object input = eventArgs.InValue;
                IntPtr vOut = eventArgs.OutValue;

                if (vOut != IntPtr.Zero && input != null)
                {
                    return;
                }
                else if (vOut != IntPtr.Zero)
                {
                    int currentNeighborhoodDistance = analyzer.NeighborhoodDistance;

                    string result;

                    if (currentNeighborhoodDistance == int.MaxValue)
                    {
                        result = Resources.NeighborhoodDistance_All;
                    }
                    else
                    {
                        result = currentNeighborhoodDistance.ToString(System.Globalization.CultureInfo.CurrentCulture);
                    }
                    Marshal.GetNativeVariantForObject(result, vOut);
                }
                else if (input != null)
                {
                    // new Neighborhood value was selected or typed in
                    string inputString = input.ToString();

                    if (inputString.Equals(Resources.NeighborhoodDistance_All))
                    {
                        analyzer.NeighborhoodDistance = int.MaxValue;
                    }
                    else
                    {
                        try
                        {
                            // Confirm value is a number
                            int distance = int.Parse(inputString, System.Globalization.CultureInfo.CurrentCulture);

                            // Call HostControl LOD distance setting
                            analyzer.NeighborhoodDistance = distance;
                        }
                        catch (FormatException)
                        {
                            // user typed in a non-numeric value, ignore it
                        }
                        catch (OverflowException)
                        {
                            // user typed in too large of a number, ignore it
                        }
                    }
                }
            }
        }

        // A Combo box requires two commands:
        //    This command is used to retrieve this list of choices for the combo box.
        // 
        // Normally IOleCommandTarget::QueryStatus is used to determine the state of a command, e.g.
        // enable vs. disable, shown vs. hidden, etc. The QueryStatus method does not have any way to 
        // control the status of a combo box, e.g. what list of items should be shown and what is the 
        // current value. In order to communicate this information actually IOleCommandTarget::Exec
        // is used with a non-NULL varOut parameter. You can think of these Exec calls as extended 
        // QueryStatus calls. There are two pieces of information needed for a combo, thus it takes
        // two commands to retrieve this information. The main command id for the command is used to 
        // retrieve the current value and the second command is used to retrieve the full list of 
        // choices to be displayed as an array of strings.
        private void NeighborhoodDistanceComboGetList_InvokeHandler(object sender, EventArgs e)
        {
            if ((null == e) || (e == EventArgs.Empty))
            {
                // We should never get here; EventArgs are required.
                return;
            }

            OleMenuCmdEventArgs eventArgs = e as OleMenuCmdEventArgs;

            if (eventArgs != null)
            {
                object inParam = eventArgs.InValue;
                IntPtr vOut = eventArgs.OutValue;

                if (inParam != null)
                {
                    return;
                }
                else if (vOut != IntPtr.Zero)
                {
                    Marshal.GetNativeVariantForObject(NeighborhoodDistances, vOut);
                }
            }
        }


        /// <summary>
        /// Handles changing neighborhood browse mode distance
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arguments"></param>
        void NeighborhoodDistance_InvokeHandler(object sender, EventArgs arguments)
        {
            // Get the command being executed
            OleMenuCommand oleMenuCommand = sender as OleMenuCommand;
            if (oleMenuCommand == null)
            {
                return;
            }

            int id = oleMenuCommand.CommandID.ID;
            if (id == PkgCmdIDList.cmdidNeighborhoodDistanceAll)
            {
                analyzer.UsingNeighborhoodBrowseMode = false;
            }
            else
            {
                int newDistance = id - (int)PkgCmdIDList.cmdidNeighborhoodDistance1 + 1;
                analyzer.NeighborhoodDistance = newDistance;
                analyzer.UsingNeighborhoodBrowseMode = true;
            }
        }

        /// <summary>
        /// determines current state of the UI control based on Neighborhood browse mode
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arguments"></param>
        private void NeighborhoodDistance_BeforeQueryStatus(object sender, EventArgs arguments)
        {
            // Get the command being queried
            OleMenuCommand oleMenuCommand = sender as OleMenuCommand;
            if (oleMenuCommand == null)
            {
                return;
            }

            bool isChecked = false;

            if (analyzer.UsingNeighborhoodBrowseMode)
            {
                int distance = analyzer.NeighborhoodDistance;
                int menuId = (int)PkgCmdIDList.cmdidNeighborhoodDistance1 + distance - 1;
                isChecked = (oleMenuCommand.CommandID.ID == menuId);
            }
            else
            {
                isChecked = (oleMenuCommand.CommandID.ID == PkgCmdIDList.cmdidNeighborhoodDistanceAll);
            }

            // Set the settings
            oleMenuCommand.Supported = true;    // visible
            oleMenuCommand.Enabled = true;      // always enabled.
            oleMenuCommand.Checked = isChecked; // checked    
        }


        private void ButterflyMode_BeforeQueryStatus(object sender, EventArgs e)
        {
            // Get the command being queried
            OleMenuCommand oleMenuCommand = sender as OleMenuCommand;
            if (oleMenuCommand == null)
            {
                return;
            }

            bool isChecked = analyzer.UsingButterflyMode;

            // Set the settings
            oleMenuCommand.Supported = true;    // visible
            oleMenuCommand.Enabled = this.HasSelectedNodes;
            oleMenuCommand.Checked = isChecked; // checked    
        }

        private void ButterflyMode_InvokeHandler(object sender, EventArgs e)
        {
            this.analyzer.ToggleButterflyMode();
        }

        #endregion

        #region SVG

        public GraphControl GraphControl
        {
            get
            {
                WindowPane pane = (WindowPane)graphWindow;
                return pane.Content as GraphControl;
            }
        }

        private void SaveAsSvg_BeforeQueryStatus(object sender, EventArgs arguments)
        {
            // Get the command being queried
            OleMenuCommand oleMenuCommand = sender as OleMenuCommand;
            if (oleMenuCommand == null)
            {
                return;
            }

            GraphControl control = GraphControl;

            // Set the settings
            oleMenuCommand.Supported = true;
            // Only enabled if we have a visible graph
            oleMenuCommand.Enabled = !(control.IsSplashScreenVisible || control.IsLayoutProgressVisible || control.Diagram.IsLayoutPending);
            oleMenuCommand.Checked = false;
        }

        void SaveAsSvg_InvokeHandler(object sender, EventArgs arguments)
        {
            SaveFileDialog sd = new SaveFileDialog();
            sd.Title = "Save as SVG";
            sd.CheckPathExists = true;
            sd.Filter = "SVG Files (*.svg)|*.svg";
            sd.FilterIndex = 0;
            string filename = this.graphWindow?.Document?.FileName;
            if (!string.IsNullOrEmpty(filename))
            {
                string baseName = System.IO.Path.GetFileNameWithoutExtension(filename);
                sd.FileName = baseName + ".svg";
            }

            if (true == sd.ShowDialog())
            {
                SvgExporter svg = new SvgExporter();
                svg.Export(GraphControl, sd.FileName);

                NativeMethods.OpenUrl(sd.FileName);
            }
        }

        #endregion 

        #region Compare Graphs

        private void CompareGraphs_BeforeQueryStatus(object sender, EventArgs arguments)
        {
            // Get the command being queried
            OleMenuCommand oleMenuCommand = sender as OleMenuCommand;
            if (oleMenuCommand == null)
            {
                return;
            }

            GraphControl control = GraphControl;

            // Set the settings
            oleMenuCommand.Supported = true;
            // Only enabled if we have a visible graph
            oleMenuCommand.Enabled = !(control.IsSplashScreenVisible || control.IsLayoutProgressVisible);
            oleMenuCommand.Checked = false;
        }
        Graph GetUserDiffTemplate()
        {
            var manager = new Microsoft.VisualStudio.Shell.Settings.ShellSettingsManager(serviceProvider);
            var documents = manager.GetApplicationDataFolder(ApplicationDataFolder.Documents);
            string path = System.IO.Path.Combine(documents, "DgmlPowerTools");
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
            Graph result = null;
            string template = System.IO.Path.Combine(path, "GraphDiffTemplate.dgml");
            try
            {
                return Graph.Load(template, Microsoft.VisualStudio.GraphModel.Schemas.DgmlCommonSchema.Schema);
            }
            catch (Exception)
            {
                result = ShellHelpers.GetEmbeddedGraphResource("Resources.template.dgml");
                result.Save(template);
            }

            return result;
        }


        void CompareGraphs_InvokeHandler(object sender, EventArgs arguments)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Graph original = this.graphWindow.Graph;
            if (original == null)
            {
                return;
            }

            // OpenFileDialog, pick the other file to compare with...
            OpenFileDialog od = new OpenFileDialog();
            od.Title = "Open Other File to Compare";
            od.Filter = "DGML Files (*.dgml)|*.dgml|All Files (*.*)|*.*";
            od.CheckFileExists = true;
            if (od.ShowDialog() != true)
            {
                return;
            }

            try
            {
                Graph target = Graph.Load(od.FileName, Microsoft.VisualStudio.Progression.DgmlCommonSchema.Schema);

                Graph result = GetUserDiffTemplate();

                GraphDiff diff = new GraphDiff();
                string resultPath = diff.Compare(original, target, result);

                ShellHelpers.OpenDocument(serviceProvider, resultPath);

                IVsWindowPane pane = ShellHelpers.GetActiveDocumentWindowPane(serviceProvider);
                if (pane != null)
                {
                    IGraphDocumentWindowPane graphWindow = (IGraphDocumentWindowPane)pane;
                    Graph resultGraph = graphWindow.Graph;
                    IEditingContext context = (IEditingContext)graphWindow.GetService(typeof(IEditingContext));
                    if (context != null)
                    {
                        IGraphStatus currentStatus = context.GetValue<IGraphStatus>();

                        // todo: copy icons from first document.
                        var iconService = context.GetValue<IIconService>();

                        if (currentStatus != null)
                        {
                            foreach (DiffResultInfo e in diff.Differences)
                            {
                                GraphObject found = FindResultObject(e.GraphObject, resultGraph);
                                if (found != null)
                                {
                                    currentStatus.ReportError(found, ErrorLevel.Warning, e.Mesage, "GraphDiffPackage");
                                }
                            }
                            currentStatus.ShowOutput();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Error comparing graphs: " + e.Message, "Unhandled Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        private void HideInternals_BeforeQueryStatus(object sender, EventArgs arguments)
        {
            // Get the command being queried
            OleMenuCommand oleMenuCommand = sender as OleMenuCommand;
            if (oleMenuCommand == null)
            {
                return;
            }

            GraphControl control = GraphControl;

            // Set the settings
            oleMenuCommand.Supported = true;
            // Only enabled if we have a visible graph
            oleMenuCommand.Enabled = !(control.IsSplashScreenVisible || control.IsLayoutProgressVisible);
            oleMenuCommand.Checked = false;
        }

        void HideInternals_InvokeHandler(object sender, EventArgs arguments)
        {
            Graph graph = this.graphWindow.Graph;
            if (graph == null)
            {
                return;
            }
            HideInternals(graph);
        }

        void HideInternals(Graph graph)
        {
            object id = new object();
            using (GraphTransactionScope scope = new UndoableGraphTransactionScope(id, "Hide Internals", UndoOption.Add))
            {
                foreach (var node in graph.Nodes)
                {
                    if (node.GetValue<bool>("CodeSchemaProperty_IsInternal") || node.GetValue<bool>("CodeSchemaProperty_IsPrivate"))
                    {
                        Hide(node, true);
                    }
                }
                scope.Complete();
            }
        }

        /// <summary>
        /// Hide the given graph object and remember we have hidden it.
        /// </summary>
        /// <param name="graphObject"></param>
        void Hide(GraphObject graphObject, bool hideGroup)
        {
            Graph owner = graphObject.Owner;
            graphObject.Visibility = Visibility.Hidden;

            GraphNode node = graphObject as GraphNode;
            if (node != null && node.IsGroup && hideGroup)
            {
                GraphGroup g = owner.FindGroup(node);
                if (g != null)
                {
                    HideChildren(g);
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


        GraphObject FindResultObject(GraphObject source, Graph resultGraph)
        {
            GraphNode node = source.AsNode();
            if (node != null)
            {
                return resultGraph.Nodes.Get(node.Id);
            }
            GraphLink link = source as GraphLink;
            if (link != null)
            {
                if (link.IsChildLink)
                {
                    // don't report containment links for groups - you can't see them anyway.
                    return null;
                }
                return resultGraph.Links.Get(link.Source.Id, link.Target.Id);
            }
            return null;
        }

        #endregion

    }
}
