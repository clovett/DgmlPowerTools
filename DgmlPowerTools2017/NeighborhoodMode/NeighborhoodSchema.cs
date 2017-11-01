using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.GraphModel;
using Microsoft.VisualStudio.Diagrams.View;
using Microsoft.VisualStudio.Progression;
using System.Windows;

namespace LovettSoftware.DgmlPowerTools
{
    static class NeighborhoodSchema
    {
        public static GraphSchema Schema { get; private set; }

        static NeighborhoodSchema()
        {
            GraphSchema schema = Schema = new GraphSchema("NeighborhoodSchema");

            ButterflyMode = schema.Properties.AddNewProperty("ButterflyMode", typeof(bool), () => { return new GraphMetadata(GraphMetadataOptions.Removable | GraphMetadataOptions.Sharable | GraphMetadataOptions.Serializable); });
            NeighborhoodCenter = schema.Properties.AddNewProperty("NeighborhoodCenter", typeof(bool));
            NeighborhoodDistance = schema.Properties.AddNewProperty("NeighborhoodDistance", typeof(int), () => { return new GraphMetadata(GraphMetadataOptions.Removable | GraphMetadataOptions.Sharable | GraphMetadataOptions.Serializable); });
            IsButterflyHidden = schema.Properties.AddNewProperty("IsButterflyHidden", typeof(bool), () => { return new GraphMetadata(GraphMetadataOptions.Removable); });
            Layer = schema.Properties.AddNewProperty("Layer", typeof(int), () => { return new GraphMetadata(GraphMetadataOptions.Removable | GraphMetadataOptions.Sharable); });
            
            DgmlCommonSchema.Schema.AddSchema(Schema);
        }

        ///<summary>
        /// NeighborhoodCenter 
        ///</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1721:PropertyNamesShouldNotMatchGetMethods")]
        public static GraphProperty NeighborhoodCenter { get; private set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1011")]
        public static bool GetNeighborhoodCenter(this GraphNode node)
        {
            return node.GetValueNoException<bool>(NeighborhoodCenter);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1011")]
        public static void SetNeighborhoodCenter(this GraphNode node, bool center)
        {
            if (center)
            {
                node.SetValue<bool>(NeighborhoodCenter, center);
            }
            else
            {
                node.ClearValue(NeighborhoodCenter);
            }
        }

        ///<summary>
        /// NeighborhoodDistance
        ///</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1721:PropertyNamesShouldNotMatchGetMethods")]
        public static GraphProperty NeighborhoodDistance { get; private set; }

        public static int GetNeighborhoodDistance(this GraphObject graphObject)
        {
            return graphObject.GetValueNoException<int>(NeighborhoodDistance);
        }
        public static void SetNeighborhoodDistance(this GraphObject graphObject, int distance)
        {
            graphObject.SetValue<int>(NeighborhoodDistance, distance);
        }

        ///<summary>
        /// ButterflyMode
        ///</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1721:PropertyNamesShouldNotMatchGetMethods")]
        public static GraphProperty ButterflyMode { get; private set; }

        public static bool GetButterflyMode(this GraphObject graphObject)
        {
            return graphObject.GetValueNoException<bool>(ButterflyMode);
        }
        public static void SetButterflyMode(this GraphObject graphObject, bool on)
        {
            if (on)
            {
                graphObject.SetValue<bool>(ButterflyMode, on);
            }
            else
            {
                graphObject.ClearValue(ButterflyMode);
            }
        }

        ///<summary>
        /// IsButterflyHidden
        ///</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1721:PropertyNamesShouldNotMatchGetMethods")]
        public static GraphProperty IsButterflyHidden { get; private set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1011")]
        public static bool GetIsButterflyHidden(this GraphObject graphObject)
        {
            return graphObject.GetValueNoException<bool>(IsButterflyHidden);
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1011")]
        public static void SetIsButterflyHidden(this GraphObject graphObject, bool isButterflyHidden)
        {
            if (isButterflyHidden)
            {
                graphObject.SetValue<bool>(IsButterflyHidden, isButterflyHidden);
            }
            else
            {
                graphObject.ClearValue(IsButterflyHidden);
            }
        }

        ///<summary>
        /// Layer - for butterfly mode
        ///</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1721:PropertyNamesShouldNotMatchGetMethods")]
        public static GraphProperty Layer { get; private set; }

        public static int GetLayer(this GraphObject graphObject)
        {
            return graphObject.GetValueNoException<int>(Layer);
        }
        public static void SetLayer(this GraphObject graphObject, int layer)
        {
            graphObject.SetValue<int>(Layer, layer);
        }

        /// <summary>
        /// Helper method
        /// </summary>
        /// <param name="g"></param>
        /// <returns></returns>
        public static bool IsVisible(this GraphObject g)
        {
            return g.Visibility == Visibility.Visible;
        }
    }
}
