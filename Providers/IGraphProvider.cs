using Microsoft.VisualStudio.GraphModel;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.GraphProviders
{
    public interface IGraphDependencyProvider
    {
        bool ExpandDependencies(IEnumerable<GraphNode> context, int depth);
    }
}
