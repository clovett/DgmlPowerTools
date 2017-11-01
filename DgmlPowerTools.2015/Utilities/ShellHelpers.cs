using Microsoft.VisualStudio;
using Microsoft.VisualStudio.GraphModel;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LovettSoftware.DgmlPowerTools
{
    static class ShellHelpers
    {
        /// <summary>
        /// Get an embedded file resource as a Graph object.
        /// </summary>
        public static Graph GetEmbeddedGraphResource(string name)
        {
            string nspace = typeof(ShellHelpers).Namespace;
            using (System.IO.Stream s = typeof(ShellHelpers).Assembly.GetManifestResourceStream(nspace + "." + name))
            {
                return Graph.Load(s, Microsoft.VisualStudio.GraphModel.Schemas.DgmlCommonSchema.Schema);
            }
        }


        /// <summary>
        /// Return the current active document window pane or null.
        /// </summary>
        /// <returns></returns>
        public static IVsWindowPane GetActiveDocumentWindowPane(IServiceProvider provider)
        {
            IVsWindowFrame frame = GetActiveDocumentWindowFrame(provider);
            if (frame != null)
            {
                object docView = null;
                if (ErrorHandler.Succeeded(frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView,
                    out docView)))
                {
                    return docView as IVsWindowPane;
                }
            }
            return null;
        }

        /// <summary>
        /// Return the current active document window frame or null.
        /// </summary>
        /// <returns></returns>
        public static IVsWindowFrame GetActiveDocumentWindowFrame(IServiceProvider provider)
        {
            IVsMonitorSelection selection = (IVsMonitorSelection)provider.GetService(typeof(SVsShellMonitorSelection));
            if (selection != null)
            {
                object pvar = null;
                if (ErrorHandler.Succeeded(selection.GetCurrentElementValue(
                        (uint)VSConstants.VSSELELEMID.SEID_DocumentFrame, out pvar)))
                {
                    IVsWindowFrame frame = pvar as IVsWindowFrame;
                    return frame;
                }
            }
            return null;
        }

        public static void OpenDocument(IServiceProvider provider, string fileName)
        {
            EnvDTE.DTE dte = provider.GetService(typeof(EnvDTE._DTE)) as EnvDTE.DTE;
            if (dte != null)
            {
                EnvDTE.Window w = dte.ItemOperations.OpenFile(fileName, vsViewKindPrimary);
                w.Activate();
            }
        }

        public const string vsViewKindPrimary = "{00000000-0000-0000-0000-000000000000}";
    }
}
