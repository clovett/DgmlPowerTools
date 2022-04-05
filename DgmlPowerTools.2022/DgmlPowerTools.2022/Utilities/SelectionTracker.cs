using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Progression;

namespace LovettSoftware.DgmlPowerTools
{
    public class ActiveWindowChangedEventArgs : EventArgs
    {
        IGraphDocumentWindowPane pane;

        public ActiveWindowChangedEventArgs(IGraphDocumentWindowPane pane)
        {
            this.pane = pane;
        }

        public IGraphDocumentWindowPane Window { get { return this.pane; } }
    }

    class SelectionTracker : IVsSelectionEvents, IDisposable
    {
        uint selectionCookie;
        IVsMonitorSelection monitorSelection;
        IGraphDocumentWindowPane activeWindow;

        public SelectionTracker()
        {
        }

        internal void Initialize(System.IServiceProvider provider)
        {         
            IVsMonitorSelection monitorSelection = provider.GetService(typeof(IVsMonitorSelection)) as IVsMonitorSelection;
            if (monitorSelection != null)
            {

                object currentValue;
                if (0 == monitorSelection.GetCurrentElementValue((uint)Microsoft.VisualStudio.Shell.Interop.Constants.SEID_DocumentFrame, out currentValue))
                {
                    OnActiveWindowChanged(currentValue as IVsWindowFrame);
                }

                monitorSelection.AdviseSelectionEvents(this, out selectionCookie);
            }
        }

        public IGraphDocumentWindowPane ActiveWindow
        {
            get
            {
                return activeWindow;
            }
            set
            {
                if (value != activeWindow)
                {
                    ActiveWindowChanged(this, new ActiveWindowChangedEventArgs(value));
                }
                this.activeWindow = value;
            }
        }

        public event EventHandler<ActiveWindowChangedEventArgs> ActiveWindowChanged;

        public void Dispose()
        {
            if (monitorSelection != null)
            {
                monitorSelection.UnadviseSelectionEvents(selectionCookie);
                monitorSelection = null;
            }
        }

        void OnActiveWindowChanged(IVsWindowFrame frame)
        {
            if (frame == null)
            {
                return;
            }
            // First we have to show the frame so that WPF control is connected to window
            // so that we can translate coordinates to screen coordinates and scroll things into view.
            object docView = null;
            if (0 == frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out docView))
            {
                IGraphDocumentWindowPane pane = docView as IGraphDocumentWindowPane;
                if (ActiveWindowChanged != null && pane != null)
                {
                    ActiveWindow = pane;
                }
            }
        }

        public int OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld, ISelectionContainer pSCOld, IVsHierarchy pHierNew, uint itemidNew, IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew)
        {
            return 0;
        }

        public int OnCmdUIContextChanged(uint dwCmdUICookie, int fActive)
        {
            return 0;
        }

        public int OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
        {
            switch (elementid)
            {
                case (uint)Microsoft.VisualStudio.Shell.Interop.Constants.SEID_DocumentFrame:
                    OnActiveWindowChanged(varValueNew as IVsWindowFrame);
                    break;
            }
            return 0;
        }

    }
}
