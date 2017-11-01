using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Progression;
using Microsoft.VisualStudio.GraphModel;
using IServiceProvider = System.IServiceProvider;
using Microsoft.VisualStudio.Diagrams.View;
using System.Windows;
using System.Windows.Input;

namespace LovettSoftware.DgmlPowerTools
{
    internal class GraphDocumentWindowTracker : IDisposable
    {
        IGraphDocumentWindowPane pane;
        Commands commands;

        public GraphDocumentWindowTracker(IGraphDocumentWindowPane pane)
        {
            this.pane = pane;
            commands = new Commands(pane);
        }

        private GraphControl GraphControl
        {
            get {
                WindowPane toolWindow = (WindowPane)pane;
                return toolWindow.Content as GraphControl;
            }
        }

        public void Dispose()
        {
            this.pane = null;

            // dispose the commands object.
            using (commands)
            {
                commands = null; 
            }
            
        }

    }
}
