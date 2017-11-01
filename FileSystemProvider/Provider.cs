using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Progression;
using Microsoft.VisualStudio.GraphModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using System.ComponentModel.Composition;
using System.IO;
using grc = Microsoft.VisualStudio.Progression.Global.Resources;
using Action = Microsoft.VisualStudio.Progression.Action;

namespace Microsoft.Samples.FileSystemProvider
{
    [LegacyProvider(typeof(IProvider), Name="FileSystemProvider", Priority=0)]
    public class FileSystemProvider : IProvider
    {
        
        // actions
        static Action FileSystemViewAction = Action.Register("Microsoft.FileSystemView", Resources.FileSystemViewLabel, ActionFlags.IsInBrowser, null);

        // custom qualified names
        static GraphNodeIdName PathName = GraphNodeIdName.Get("Assembly", "Assembly", typeof(Uri)); 

        #region IProvider

        IDataManager _dataManager;
        IIconService _iconService;

        public void Initialize(IServiceProvider serviceProvider)
        {
            
            // Register our column 0 actions.
            _dataManager = serviceProvider.GetService(typeof(IDataManager)) as IDataManager;

            // TODO: Looks like Architecture Expolorer has been pulled !!

            //if (_dataManager != null)
            //{
            //    AddRootAction(_dataManager.Graph, grc.BrowserGroup_FileSystem_Id_NOLOC_, FileSystemViewAction, "Microsoft.FileSystemView");
            //}

            //// Register action handlers
            //FileSystemViewAction[DgmlProperties.BrowserGroup] = grc.BrowserGroup_FileSystem_Id_NOLOC_;
            FileSystemViewAction.ActionHandlers.Add(new ActionHandler(HandleFileSystemViewAction));

            Actions.Contains.ActionHandlers.Add(new ActionHandler(HandleContains));

            // Register icons.
            _iconService = serviceProvider.GetService(typeof(IIconService)) as IIconService;
            if (_iconService != null)
            {
                RegisterCategoryIcon(_iconService, FileSchema.DriveCategory, "Drive.png");
                RegisterCategoryIcon(_iconService, FileSchema.FolderCategory, "Folder.png");
            }
        }

        public Graph Schema
        {
            get { return null; }
        }
        #endregion 

        #region Action Handlers

        /// <summary>
        /// Handle the column 0 action to browse file system.
        /// </summary>
        /// <param name="context"></param>
        void HandleFileSystemViewAction(ActionContext context)
        {
            using (var scope = new GraphTransactionScope())
            {
                Graph graph = context.Graph;
                var nodes = graph.Nodes;
                foreach (string drive in Directory.GetLogicalDrives())
                {
                    string driveLetter = drive.Replace(":", "").Replace("\\", "");
                    var id = GraphNodeId.GetNested(GraphNodeId.GetPartial(PathName, new Uri(drive)));
                    // Create the node (if it doesn't already exist).
                    GraphNode n = nodes.GetOrCreate(id, driveLetter, FileSchema.DriveCategory);
                    // And make it show up in next column
                    context.OutputObjects.Add(n);
                }
                scope.Complete();
            }
        }

        /// <summary>
        /// Check if input node is one of ours and handle it.  This is performance critical, we
        /// do not want to slow down actions that do not belong to us, and we do not want to handle
        /// nodes that were intercepted by some other provider.  This is good provider etiquette.
        /// </summary>
        /// <param name="context"></param>
        void HandleContains(ActionContext context)
        {            
            using (var scope = new GraphTransactionScope())
            {
                foreach (GraphNode n in context.InputNodes)
                {
                    if (!context.IsHandled(n))
                    {
                        if (n.HasCategory(FileSchema.DriveCategory) || n.HasCategory(FileSchema.FolderCategory))
                        {
                            // it's ours!
                            AddFiles(context, n);
                            // tell other providers we handled it.
                            context.AddHandled(n);
                        }
                    }
                }
                scope.Complete();
            }
        }

        #endregion 

        #region Helpers

        void AddFiles(ActionContext context, GraphNode folder)
        {
            Uri uri = null;
            GraphNodeId inner = folder.Id.GetNestedIdByName(PathName);
            if (inner != null)
            {
                uri = inner.Value as Uri;
            }
            if (uri == null) return;
            string path = uri.LocalPath;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return;
            }
            Graph graph = context.Graph;
            var nodes = graph.Nodes;
            var links = graph.Links;

            if (path.EndsWith(Path.DirectorySeparatorChar.ToString()) && !path.EndsWith(":"+Path.DirectorySeparatorChar))
            {
                path = path.Substring(0, path.Length-1);
            }

            try
            {
                foreach (string dirs in Directory.GetDirectories(path))
                {
                    ThrowIfCancelled(context);
                    // Create the node (if it doesn't already exist).
                    var id = GraphNodeId.GetNested(GraphNodeId.GetPartial(PathName, new Uri(dirs)));
                    GraphNode n = nodes.GetOrCreate(id, Path.GetFileName(dirs), FileSchema.FolderCategory);
                    links.GetOrCreate(folder, n, null, LinkCategories.Contains);

                    DirectoryInfo info = new DirectoryInfo(dirs);
                    SetAttributes(n, info);
                    // And make it show up in next column
                    context.OutputObjects.Add(n);
                }

                foreach (string file in Directory.GetFiles(path))
                {
                    ThrowIfCancelled(context);
                    // Create the node (if it doesn't already exist).
                    var id = GraphNodeId.GetNested(GraphNodeId.GetPartial(PathName, new Uri(file)));
                    GraphNode n = nodes.GetOrCreate(id, Path.GetFileName(file), FileSchema.FileCategory);
                    links.GetOrCreate(folder, n, null, LinkCategories.Contains);
                    n.SetValue(DgmlProperties.FilePath, file); // so other providers will crack this if it's an assembly.
                    var fi = new FileInfo(file);
                    SetAttributes(n, fi);
                    SetIcon(context.Dispatcher, n, fi);
                    n.SetValue<long>(FileSchema.FileSizeProperty, fi.Length);

                    // And make it show up in next column
                    context.OutputObjects.Add(n);
                }
            }
            catch (System.UnauthorizedAccessException)
            {
                // swallow this one.
            }
            catch (System.IO.FileNotFoundException)
            {
                // swallow this one, happens in weird TEMP folder situations.
            }

        }

        private static void ThrowIfCancelled(ActionContext context)
        {
            if (context.Cancel)
            {
                throw new OperationCanceledException();
            }
        }

        void SetIcon(Dispatcher dispatcher, GraphNode n, FileInfo fi)
        {
            string iconName = FileIcons.GetSmallIconName(dispatcher, _iconService, fi.FullName);
            if (iconName != null)
            {
                n.SetValue<string>(DgmlProperties.Icon, iconName);
            }
        }


        static void SetAttributes(GraphNode n, FileSystemInfo info)
        {
            n.SetValue<DateTime>(FileSchema.DateCreatedProperty, info.CreationTime);
            n.SetValue<DateTime>(FileSchema.DateModifiedProperty, info.LastWriteTime);
            if ((info.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
            {
                n.Visibility = Visibility.Hidden;
            }
            if ((info.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                n.SetValue<bool>(FileSchema.ReadOnlyProperty, true);
            }
            if ((info.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                bool isMountedFolder;
                string reparseDir = ReparsePoint.GetTargetDir(info, out isMountedFolder);
                if (!string.IsNullOrEmpty(reparseDir))
                {
                    // add another link for reparse points.
                    var rid = GraphNodeId.GetNested(GraphNodeId.GetPartial(PathName, new Uri(reparseDir)));
                    Graph graph = n.Owner;
                    GraphNode rnode = graph.Nodes.GetOrCreate(rid, Path.GetFileName(reparseDir), FileSchema.FolderCategory);
                    graph.Links.GetOrCreate(n, rnode, null, isMountedFolder ? FileSchema.MountedFolderCategory : FileSchema.SymbolicLinkCategory);
                }
            }
                    
        }

        static GraphCategory AddRootAction(
           Graph graph,
           string groupHeaderToPutThisActionUnder,
           Action actionToAdd,
           string defaultActionId)
        {
            // Set the default action
            if (string.IsNullOrEmpty(defaultActionId))
            {
                defaultActionId = Actions.Contains.Id;
            }

            // First we create the Category
            GraphCategory category = Microsoft.VisualStudio.Progression.NodeCategories.RegisterNodeCategory(FileSchema.Schema,
                actionToAdd.Id,         // Unique ID for the Property
                actionToAdd.Label,      // Label of the Property
                null,                   // Description
                groupHeaderToPutThisActionUnder, // Browser group
                actionToAdd.Id,         // navigationActionLabel
                true,                   // isProviderRoot
                defaultActionId,
                null                   // based on
                );

            // register the node category action
            Action.RegisterNodeCategoryAction(defaultActionId, category);

            // Create the node entry for the new action
            GraphNode rootNode = graph.Nodes.GetOrCreate(GraphNodeId.GetLiteral(actionToAdd.Id), actionToAdd.Label, category);
            rootNode[DgmlProperties.IsAlwaysHidden] = true;

            return category;
        }


        void RegisterCategoryIcon(IIconService service, GraphCategory category, string relativePath)
        {
            Graph temp = new Graph();
            var image = new BitmapImage(new Uri("pack://application:,,,/Microsoft.Samples.FileSystemProvider;component/Icons/" + relativePath));
            service.AddIcon(category.Id, category.GetLabelOrId(temp), image);

            FileSchema.Schema.OverrideMetadata(category, (m) =>
            {
                m[DgmlProperties.Icon] = category.Id;
            });
        }
        #endregion 
    }
}
