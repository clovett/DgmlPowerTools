using Microsoft.VisualStudio.Progression;
using Microsoft.VisualStudio.Shell;
using System;

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
