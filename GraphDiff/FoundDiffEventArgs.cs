using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.GraphModel;


namespace Microsoft.VisualStudio.GraphModel.GraphDiff
{
    public class FoundDiffEventArgs : EventArgs
    {
        GraphObject _graphObject;
        GraphObject _graphObject2;
        
        DiffType _diffType;
        public DiffType DiffType
        {
            get { return _diffType; }
        }
       
        public GraphObject GraphObject
        {
            get { return _graphObject; }
        }

        public GraphObject GraphObject2
        {
            get { return _graphObject2; }
            set { _graphObject2 = value; }
        }
        
        public FoundDiffEventArgs(
            GraphObject theDifferentGraphObject,
            DiffType diffType)
        {
            this._graphObject = theDifferentGraphObject;
            this._diffType = diffType;
        }
    }

    public class FoundDiffPropertyEventArgs : FoundDiffEventArgs
    {
        GraphProperty _graphProp;

        public GraphProperty GraphProp
        {
            get { return _graphProp; }
        }

        public FoundDiffPropertyEventArgs(
            GraphObject parentDiffGraphObject,
            GraphObject parent2DiffGraphObject,
            GraphProperty theDifferentProperty,
            DiffType diffType)
           :base(parentDiffGraphObject, diffType)
        {
            this._graphProp = theDifferentProperty;
            this.GraphObject2 = parent2DiffGraphObject;
        }
    }

    public class FoundDiffCategoryEventArgs : FoundDiffEventArgs
    {
        GraphCategory _graphCat;

        public GraphCategory GraphCat
        {
            get { return _graphCat; }
        }

        public FoundDiffCategoryEventArgs(
            GraphObject parentDiffGraphObject,
            GraphCategory theDifferentCategory,
            DiffType diffType)
            : base(parentDiffGraphObject, diffType)
        {
            this._graphCat = theDifferentCategory;
        }
    }
}
