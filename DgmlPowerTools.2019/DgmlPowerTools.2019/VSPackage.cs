//------------------------------------------------------------------------------
// <copyright file="VSPackage.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using LovettSoftware.DgmlPowerTools;
using System.Collections.Generic;
using Microsoft.VisualStudio.Progression;
using System.Threading;
using System.Runtime.CompilerServices;

namespace LovettSoftware.DgmlPowerTools
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable")]
    // [ProvideAutoLoad(GuidList.DgmlPackageGuidString)] // Autoload when a graph document is opened.  (not supported in VS 2019).
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [Guid(GuidList.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(FilterViewToolWindow))]
    public sealed class VSPackage : AsyncPackage, IGraphDocumentNotify, IVsPackageExtensionProvider
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="VSPackage"/> class.
        /// </summary>
        public VSPackage()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
            Instance = this;
        }

        public static void AutoLoad()
        {
            if (Instance == null)
            {
                var pkgGuid = new Guid(GuidList.PackageGuidString);
                var result = Microsoft.VisualStudio.Shell.VsShellUtilities.TryGetPackageExtensionPoint<IVsPackageExtensionProvider, IVsPackageExtensionProvider>(pkgGuid, pkgGuid);
            }
        }

        #region Package Members

        public static Package Instance { get; set; }

        SelectionTracker _tracker;

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>       
        protected override async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
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

        public dynamic CreateExtensionInstance(ref Guid extensionPoint, ref Guid instance)
        {
            return null;
        }

        #endregion
    }
}
