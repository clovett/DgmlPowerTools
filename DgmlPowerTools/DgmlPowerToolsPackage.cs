using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using Microsoft.Win32;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Progression;
using System.Collections.Generic;

namespace LovettSoftware.DgmlPowerTools
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable"), PackageRegistration(UseManagedResourcesOnly = true)]
    // Autoload when a graph document is opened.
    [ProvideAutoLoad("ADC1BC7B-958B-4548-9F9F-10FC49099825")]
    // This attribute is used to register the information needed to show this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideMenuResource("Menus.ctmenu", 1)]
    // This attribute registers a tool window exposed by this package.
    [ProvideToolWindow(typeof(FilterViewToolWindow))]
    [Guid(GuidList.guidDgmlPowerToolsPkgString)]
    public sealed class DgmlPowerToolsPackage : Package, IGraphDocumentNotify
    {
        /// <summary>
        /// Default constructor of the package.
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </summary>
        public DgmlPowerToolsPackage()
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
        }

        /// <summary>
        /// This function is called when the user clicks the menu item that shows the 
        /// tool window. See the Initialize method to see how the menu item is associated to 
        /// this function using the OleMenuCommandService service and the MenuCommand class.
        /// </summary>
        private void ShowToolWindow(object sender, EventArgs e)
        {
            // Get the instance number 0 of this tool window. This window is single instance so this instance
            // is actually the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            ToolWindowPane window = this.FindToolWindow(typeof(FilterViewToolWindow), 0, true);
            if ((null == window) || (null == window.Frame))
            {
                throw new NotSupportedException(Resources.CanNotCreateWindow);
            }
            IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }


        /////////////////////////////////////////////////////////////////////////////
        // Overridden Package Implementation
        #region Package Members

        SelectionTracker _tracker;

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            Debug.WriteLine (string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
            base.Initialize();

            // Add our package level command handlers for menu (commands must exist in the .vsct file)
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if ( null != mcs )
            {
                // Create the command for the tool window
                CommandID toolwndCommandID = new CommandID(GuidList.guidDgmlPowerToolsCmdSet, (int)PkgCmdIDList.cmdidDgmlFilterView);
                MenuCommand menuToolWin = new MenuCommand(ShowToolWindow, toolwndCommandID);
                mcs.AddCommand( menuToolWin );
            }

            IGraphDocumentManager mgr = GetService(typeof(IGraphDocumentManager)) as IGraphDocumentManager;
            if (mgr != null)
            {
                mgr.AdviseGraphDocumentEvents(this);
            }

            _tracker = new SelectionTracker();
            _tracker.ActiveWindowChanged += new EventHandler<ActiveWindowChangedEventArgs>(OnActiveWindowChanged);
            _tracker.Initialize((System.IServiceProvider)this);

            // boot strap case where we get loaded after graph document is already opened.
            OnGraphDocumentCreated(_tracker.ActiveWindow);
        }

        void OnActiveWindowChanged(object sender, ActiveWindowChangedEventArgs e)
        {
            OnGraphDocumentCreated(e.Window);
        }

        protected override void Dispose(bool disposing)
        {
            using (_tracker)
            {
                _tracker = null;
            }
            base.Dispose(disposing);
        }
        #endregion


        #region IGraphDocumentNotify

        Dictionary<IGraphDocumentWindowPane, GraphDocumentWindowTracker> _windows = new Dictionary<IGraphDocumentWindowPane, GraphDocumentWindowTracker>();

        public void OnGraphDocumentClosed(IGraphDocumentWindowPane window)
        {
            if (_windows.ContainsKey(window))
            {
                using (GraphDocumentWindowTracker tracker = _windows[window])
                {
                    _windows.Remove(window);
                }
            }
        }

        public void OnGraphDocumentCreated(IGraphDocumentWindowPane window)
        {
            if (window != null && !_windows.ContainsKey(window))
            {
                _windows[window] = new GraphDocumentWindowTracker(window);
            }
        }

        #endregion 
    }
}
