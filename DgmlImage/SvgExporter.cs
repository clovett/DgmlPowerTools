using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using Microsoft.VisualStudio.GraphModel;
using Microsoft.VisualStudio.Progression;
using Microsoft.VisualStudio.Diagrams.View;
using System.Xml;
using System.Globalization;
using System.Windows.Media.TextFormatting;
using Microsoft.VisualStudio.Progression.Controls;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.GraphModel.Styles;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.IO;

namespace LovettSoftware.DgmlPowerTools
{
    /// <summary>
    /// Exports the graph currently being displayed by the given GraphControl as an SVG diagram.
    /// </summary>
    class SvgExporter
    {
        Svg svg;
        Rect extent;
        GraphControl control;
        HashSet<GraphLink> crossGroup;
        CrossGroupLinkStyle crossGroupLinkStyle;
        string SelectionBrush;
        string GraphGroupBackground;
        string GraphNodeBorder;
        string GraphGroupBorder;

        private double GetFontSize(GraphObject obj)
        {
            return obj.GetFontSize(control);
        }

        const double Margin = 20;
        const double NodeVerticalMargin = 5;

        internal string ToString(Color c)
        {
            return "#" + XmlExtensions.HexDigits(c.R) + XmlExtensions.HexDigits(c.G) + XmlExtensions.HexDigits(c.B);
        }

        internal string GetNamedBrush(string name)
        {
            return GetNamedBrush(name, null);
        }

        internal string GetNamedBrush(string name, Brush brush)
        {
            if (brush == null)
            {
                if (name == null)
                {
                    return null;
                }
                object resource = control.Resources[name];
                if (resource is Color)
                {
                    return ToString((Color)resource);
                }
                brush = (Brush)resource;
            }

            SolidColorBrush sb = brush as SolidColorBrush;
            if (sb != null)
            {
                // note: SVG cannot put ALPHA channel here, Alpha has to be separate opacity argument.
                return ToString(sb.Color);
            }

            LinearGradientBrush lgb = brush as LinearGradientBrush;
            if (lgb != null)
            {
                if (!string.IsNullOrEmpty(name))
                {
                    Point startPoint = lgb.StartPoint;
                    Point endPoint = lgb.EndPoint;

                    SvgLinearGradient lg = new SvgLinearGradient()
                    {
                        Id = name,
                        x1 = ToPercent(startPoint.X),
                        x2 = ToPercent(startPoint.Y),
                        y1 = ToPercent(endPoint.X),
                        y2 = ToPercent(endPoint.Y)
                    };

                    foreach (GradientStop stop in lgb.GradientStops)
                    {
                        // <stop offset="100%" stop-color="#00b400" stop-opacity="5"/> 
                        SvgLinearGradientStop ls = new SvgLinearGradientStop()
                        {
                            StopColor = ToString(stop.Color),
                            Offset = ToPercent(stop.Offset)
                        };
                        lg.AddChild(ls);
                    }

                    svg.Defs.AddChild(lg);
                    return string.Format("url(#{0})", name);
                }

            }

            return "unsupported brush " + brush.GetType().Name;
        }

        public string ToPercent(double d)
        {
            int i = (int)(d * 100);
            return i.ToString() + "%";
        }

        public void Export(GraphControl control, string filename)
        {
            this.control = control;
            this.crossGroup = new HashSet<GraphLink>();
            this.crossGroupLinkStyle = control.Diagram.CrossGroupLinks;
            GraphGroupBackground = GetNamedBrush("GraphGroupBackground");
            SelectionBrush = GetNamedBrush("SelectionBorder");
            GraphNodeBorder = GetNamedBrush("GraphNodeBorder");
            GraphGroupBorder = GetNamedBrush("GraphGroupBorder");

            // We use the PrepareGraphForPrinting method to position the Legend and realize all the shapes
            // so we have all the layout & color information we need 
            control.PrepareGraphForPrinting(10, true, 1.0, true, new PrintHandler((visual) =>
            {
                svg = new Svg();
                svg.FontFamily = control.FontFamily.ToString();
                svg.FontSize = control.FontSize;
                svg.StrokeLineCap = "round";

                extent = Rect.Empty;

                SvgRectangle backdrop = null;
                Paper paper = (Paper)control.Diagram.Backdrop;
                Brush background = control.Graph.GetBackground(control);
                if (background != null) 
                {
                    background = paper.TryGetBrushOrCustom(GraphColors.DiagramPaperFill, background);
                }
                else
                {
                    background = paper.TryGetBrushOrCustom(GraphColors.DiagramPaperFill);
                }
                
                if (background != null)
                {
                    backdrop = new SvgRectangle()
                    {
                        Fill = GetNamedBrush(null, background)
                    };
                    svg.AddChild(backdrop);
                }                

                SvgGroup root = new SvgGroup();
                svg.AddChild(root);


                // root level links go underneath everything
                foreach (GraphNode node in control.Graph.VisibleOrphanNodes)
                {
                    ExportLinks(root, node.OutgoingLinks);
                }

                // then the group hierarchy.
                foreach (GraphGroup group in control.Graph.VisibleTopLevelGroups)
                {
                    extent = Rect.Union(extent, group.GroupNode.GetBounds());
                    ExportGroup(control, root, group);
                }

                // then orphan nodes on top of groups
                foreach (GraphNode node in control.Graph.VisibleOrphanNodes)
                {
                    if (!node.IsGroup)
                    {
                        extent = Rect.Union(extent, node.GetBounds());
                        ExportNode(control, root, node);
                    }
                }

                // then cross-group links on top of everything
                ExportLinks(root, crossGroup);

                if (backdrop != null)
                {
                    backdrop.Width = extent.Width;
                    backdrop.Height = extent.Height;
                }

                Rect bounds = ExportLegend(root, extent);

                extent.Width += bounds.Width;
                extent.Height = Math.Max(extent.Height, bounds.Height);
                extent.Inflate(Margin, Margin);

                root.Transform = string.Format("translate({0},{1})", -extent.X, -extent.Y);
                svg.Width = Margin + extent.Width + Margin;
                svg.Height = Margin + extent.Height + Margin;

                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = true;
                using (XmlWriter w = XmlWriter.Create(filename, settings))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(Svg));
                    serializer.Serialize(w, svg);
                }
            }));
        }

        private void ExportLinks(SvgGroup root, IEnumerable<GraphLink> links)
        {
            foreach (GraphLink link in links)
            {
                if (!link.IsChildLink)
                {
                    ExportLink(control, root, link);
                }
            }
        }

        // copied from BubbleGroup
        public const double SelectionThickness = 4;
        public const double DefaultGroupNodeRadius = 5.0;
        public const double DefaultGroupMinWidth = 50;
        public const double DefaultGroupMinHeight = 20;
        public static Thickness DefaultContentMargin = new Thickness(5, 17, 5, 5);

        private void ExportGroup(GraphControl control, SvgGroup parent, GraphGroup group)
        {
            GraphNode node = group.GroupNode;
            if (!node.IsReallyTrulyVisible(control.Graph))
            {
                return;
            }
            string background = GetNamedBrush(null, node.GetBackground(control));
            if (background == null)
            {
                background = GraphGroupBackground;
            }
            string stroke = GetNamedBrush(null, node.GetStroke(control));
            if (stroke == null)
            {
                stroke = GraphGroupBorder;
            }
            double thickness = node.GetStrokeThickness(control);
            if (thickness == 0)
            {
                thickness = 1;
            }

            Rect bounds = node.GetBounds();
            if (bounds.Width == 0 || bounds.Height == 0 || double.IsInfinity(bounds.Width) || double.IsInfinity(bounds.Height))
            {
                return;
            }
            double radius = node.GetNodeRadius(control, DefaultGroupNodeRadius);

            if (node.GetIsSelected())
            {
                // selection outline.
                parent.AddChild(new SvgRectangle()
                {
                    X = bounds.Left,
                    Y = bounds.Top,
                    RadiusX = radius,
                    RadiusY = radius,
                    Width = bounds.Width,
                    Height = bounds.Height,
                    Stroke = SelectionBrush,
                    StrokeWidth = thickness + SelectionThickness
                });
            }

            parent.AddChild(new SvgRectangle()
            {
                X = bounds.Left,
                Y = bounds.Top,
                RadiusX = radius,
                RadiusY = radius,
                Width = bounds.Width,
                Height = bounds.Height,
                Stroke = stroke,
                StrokeWidth = thickness,
                Fill = background
            });

            bounds.Inflate(-10, -5); // inset by label margin.

            Size labelSize = ExportIconLabel(parent, bounds, node, node.GetForeground(control), VerticalAlignment.Top);

            string contentBrush = GetNamedBrush(null, node.GetGroupContentColor());
            if (contentBrush == null)
            {
                contentBrush = "white";
            }

            bounds = node.GetBounds();
            bounds.Y += labelSize.Height + 7;
            if (labelSize.Height + 7 < bounds.Height)
            {
                bounds.Height -= labelSize.Height + 7;
            }
            bounds.Inflate(-5, -5);

            // group inner rectangle
            parent.AddChild(new SvgRectangle()
            {
                X = bounds.Left,
                Y = bounds.Top,
                Width = bounds.Width,
                Height = bounds.Height,
                Stroke = stroke,
                StrokeWidth = thickness,
                Fill = contentBrush
            });

            // links in the group first underneath the nodes

            foreach (GraphNode child in group.ChildNodes)
            {
                if (child.IsVisible() && !child.IsGroup)
                {
                    foreach (GraphLink link in child.OutgoingLinks)
                    {
                        if (!link.IsChildLink)
                        {
                            if (link.GetIsCrossGroup())
                            {
                                AddCrossGroupLink(link);
                            }
                            else
                            {
                                ExportLink(control, parent, link);
                            }
                        }
                    }
                }
            }

            foreach (GraphGroup childGroup in group.ChildGroups)
            {
                if (childGroup.IsVisible())
                {
                    foreach (GraphLink link in childGroup.GroupNode.OutgoingLinks)
                    {
                        if (!link.IsChildLink)
                        {
                            if (link.GetIsCrossGroup())
                            {
                                AddCrossGroupLink(link);
                            }
                            else
                            {
                                ExportLink(control, parent, link);
                            }
                        }
                    }
                }
            }
            // now the nodes
            foreach (GraphNode child in group.ChildNodes)
            {
                if (child.IsVisible() && !child.IsGroup)
                {
                    ExportNode(control, parent, child);
                }
            }

            // and the child groups
            foreach (GraphGroup childGroup in group.ChildGroups)
            {
                if (childGroup.IsVisible())
                {
                    ExportGroup(control, parent, childGroup);
                }
            }

        }

        private void AddCrossGroupLink(GraphLink link)
        {
            Graph g = control.Graph;
            if (link.IsReallyTrulyVisible(g))
            {
                switch (this.crossGroupLinkStyle)
                {
                    case CrossGroupLinkStyle.Show:
                        this.crossGroup.Add(link);
                        break;
                    case CrossGroupLinkStyle.ShowOnSelectedNodes:
                        GraphNode src = link.GetVisibleSource(g);
                        GraphNode target = link.GetVisibleTarget(g);
                        if (src == null)
                        {
                            src = link.Source;
                        }
                        if (target == null)
                        {
                            target = link.Target;
                        }
                        if (src.GetIsSelected() || target.GetIsSelected())
                        {
                            this.crossGroup.Add(link);
                        }
                        break;
                    case CrossGroupLinkStyle.Hide:
                    case CrossGroupLinkStyle.HideAllLinks:
                    default:
                        break;
                }
            }
        }

        private void ExportLink(GraphControl control, SvgGroup parent, GraphLink link)
        {
            if (!link.IsReallyTrulyVisible(control.Graph) || this.crossGroupLinkStyle == CrossGroupLinkStyle.HideAllLinks)
            {
                return;
            }

            Geometry g = link.GetGeometry();
            if (g != null)
            {
                extent = Rect.Union(extent, g.Bounds);

                string stroke = GetNamedBrush("LinkBrush", link.GetStroke(control));
                double thickness = VisualGraphProperties.GetStrokeThickness(link);
                if (thickness == 0)
                {
                    thickness = 1;
                }

                double weight = VisualGraphProperties.GetWeight(link);
                if (weight != 0)
                {
                    if (weight > 1)
                    {
                        weight = 1 + Math.Min(16, Math.Log(weight, 2)) / 2d;
                    }
                    thickness *= weight;
                }

                string pathData = XmlExtensions.ToString(g);
                string arrowHead = GetArrowheadPath(parent, link, thickness);

                if (link.GetIsSelected())
                {
                    // selected link
                    parent.AddChild(new SvgPath()
                    {
                        Stroke = SelectionBrush,
                        StrokeWidth = thickness + SelectionThickness,
                        Data = pathData
                    });

                    // selected arrowhead
                    parent.AddChild(new SvgPath()
                    {
                        Stroke = SelectionBrush,
                        Fill = SelectionBrush,
                        StrokeWidth = SelectionThickness,
                        Data = arrowHead,
                        StrokeLineJoin = "round"
                    });
                }

                parent.AddChild(new SvgPath()
                {
                    Stroke = stroke,
                    StrokeWidth = thickness,
                    Data = pathData
                });

                // arrowhead
                parent.AddChild(new SvgPath()
                {
                    Stroke = stroke,
                    Fill = stroke,
                    StrokeWidth = 1.0,
                    Data = arrowHead,
                    StrokeLineJoin = "round"
                });


                string label = link.Label;
                if (!string.IsNullOrWhiteSpace(label))
                {
                    Brush foreground = link.GetStroke(control);
                    if (foreground == null)
                    {
                        foreground = (Brush)control.FindResource("LinkBrush");
                    }

                    Rect labelBounds = link.GetLabelBounds();
                    ExportIconLabel(parent, labelBounds, link, foreground, VerticalAlignment.Top);
                }

            }
        }

        // a cache of the most recently used font info.
        FontFamily family;
        FontStyle style;
        FontWeight weight;
        Typeface typeface;

        Typeface GetTypeface(GraphObject g)
        {
            FontFamily family = g.GetFontFamily(control);
            FontStyle style = g.GetFontStyle(control);
            FontWeight weight = g.GetFontWeight(control);
            if (this.typeface != null && family == this.family && this.style == style && this.weight == weight)
            {
                return this.typeface;
            }

            this.family = family;
            this.style = style;
            this.weight = weight;
            this.typeface = new Typeface(family, style, weight, FontStretches.Normal);
            return typeface;
        }

        // Copied from LinkEndArrowhead.cs
        const double MinHeadWidth = 8;

        private string GetArrowheadPath(SvgGroup parent, GraphLink link, double strokeThickness)
        {
            // get arrowhead dimensions.
            Point endPoint = XmlExtensions.GetEndPoint(link.GetGeometry());
            Point tip = link.GetLinkTargetDecoratorTip();
            double tipOffset = link.GetLinkTargetDecoratorTipOffset();

            var curveDepth = link.GetLinkTargetDecoratorBaseTipDepth();
            var depth = link.GetLinkTargetDecoratorHeight();
            double width = Math.Max(MinHeadWidth, strokeThickness + 4);

            Vector start = new Vector(tip.X, tip.Y);
            Vector direction = (endPoint - tip);
            double len = curveDepth + Math.Abs(curveDepth - depth);

            Vector leftNormal = new Vector(-direction.Y, direction.X);
            leftNormal.Normalize();
            leftNormal *= width / 2;
            Vector rightNormal = new Vector(direction.Y, -direction.X);
            rightNormal.Normalize();
            rightNormal *= width / 2;

            // the base of the arrow
            direction.Normalize();
            direction *= len;
            Point basePoint = tip + direction;
            Point leftCorner = basePoint + leftNormal;
            Point rightCorner = basePoint + rightNormal;

            // the base curve control points
            Point a = endPoint + leftNormal;
            Point b = endPoint + rightNormal;
            Vector diff = b - a;
            len = diff.Length;
            diff.Normalize();
            diff *= (len / 3);

            Point ctrl1 = a + diff;
            diff *= 2;
            Point ctrl2 = a + diff;

            StringBuilder sb = new StringBuilder();
            sb.Append("M ");
            sb.Append(XmlExtensions.ToString(tip));
            sb.Append(" L ");
            sb.Append(XmlExtensions.ToString(leftCorner));
            sb.Append(" C ");
            sb.Append(XmlExtensions.ToString(ctrl1));
            sb.Append(" ");
            sb.Append(XmlExtensions.ToString(ctrl2));
            sb.Append(" ");
            sb.Append(XmlExtensions.ToString(rightCorner));

            sb.Append(" z");

            return sb.ToString();

        }

        private void ExportNode(GraphControl control, SvgGroup parent, GraphNode node)
        {
            if (!node.IsReallyTrulyVisible(control.Graph))
            {
                return;
            }
            string background = GetNamedBrush(null, node.GetBackground(control));
            string stroke = GetNamedBrush(null, node.GetStroke(control));
            if (stroke == null)
            {
                stroke = GraphNodeBorder;
            }
            double thickness = node.GetStrokeThickness(control);
            if (thickness == 0)
            {
                thickness = 1;
            }

            Rect bounds = node.GetBounds();
            if (bounds.IsEmpty || bounds.Width == 0 || bounds.Height == 0)
            {
                return;
            }
            double radius = node.GetNodeRadius(control, 3);

            bool noShape = false;
            string shape = node.GetShape();
            if (!string.IsNullOrEmpty(shape) && string.Compare(shape.Trim(), "none", StringComparison.OrdinalIgnoreCase) == 0)
            {
                noShape = true;
            }

            if (node.GetIsSelected())
            {
                if (noShape)
                {
                    // blue selection 
                    background = GetNamedBrush("NoShapeSelectionBrush", null);
                    string border = GetNamedBrush("NoShapeSelectionBorder", (Brush)control.FindResource(GraphColors.NoShapeSelectionBorder));

                    parent.AddChild(new SvgRectangle()
                    {
                        X = bounds.Left,
                        Y = bounds.Top,
                        RadiusX = radius,
                        RadiusY = radius,
                        Width = bounds.Width,
                        Height = bounds.Height,
                        Stroke = border,
                        StrokeWidth = 1,
                        Fill = background
                    });
                }
                else
                {
                    // selection outline
                    parent.AddChild(new SvgRectangle()
                    {
                        X = bounds.Left,
                        Y = bounds.Top,
                        RadiusX = radius,
                        RadiusY = radius,
                        Width = bounds.Width,
                        Height = bounds.Height,
                        Stroke = SelectionBrush,
                        StrokeWidth = thickness + SelectionThickness
                    });
                }
            }

            if (!noShape)
            {
                parent.AddChild(new SvgRectangle()
                {
                    X = bounds.Left,
                    Y = bounds.Top,
                    RadiusX = radius,
                    RadiusY = radius,
                    Width = bounds.Width,
                    Height = bounds.Height,
                    Stroke = stroke,
                    StrokeWidth = thickness,
                    Fill = background
                });
            }

            bounds.Inflate(-10, -5); // inset by label margin.
            ExportIconLabel(parent, bounds, node, node.GetForeground(control), VerticalAlignment.Center);
        }

        private bool IsAlmostEqual(double a, double b, double tolerance)
        {
            return Math.Abs(a - b) <= tolerance;
        }

        private SvgUse ExportIcon(SvgGroup parent, string iconPath, double x, double y, out Rect iconExtent)
        {
            iconExtent = Rect.Empty;
            SvgUse imageUse = null;
            Uri iconUri;
            if (!string.IsNullOrWhiteSpace(iconPath) &&
                Uri.TryCreate(iconPath, UriKind.RelativeOrAbsolute, out iconUri))
            {
                IIconDownload download = control.IconService.BeginLoadingIcon(iconUri, iconPath);
                if (download != null)
                {
                    ImageSource src = download.DownloadedImage;
                    if (src != null)
                    {
                        AddImageDef(iconPath, src);
                        double w = src.Width;
                        double h = src.Height;

                        imageUse = new SvgUse()
                        {
                            X = x,
                            Y = y,
                            Href = "#" + iconPath
                        };

                        parent.AddChild(imageUse);
                        iconExtent = new Rect(x, y, w, h);
                    }
                }
            }
            return imageUse;
        }

        private void ScaleIcon(SvgUse imageUse, double scaleX, double scaleY)
        {
            string transform = null;

            if (scaleX != 1 || scaleY != 1)
            {
                // need to transform the image to fit the bounds.
                // problem is when you apply a scale transform, it transforms the X,Y coordinates also
                // so we have to undo that here by using translate.
                transform = string.Format("translate({0},{1}),scale({2},{3})", imageUse.X, imageUse.Y, scaleX, scaleY);
            
                imageUse.X = 0;
                imageUse.Y = 0;
                imageUse.Transform = transform;
            }

        }

        private Size ExportIconLabel(SvgGroup parent, Rect bounds, GraphObject graphObject, Brush foreground, VerticalAlignment valign)
        {
            Size iconSize = new Size(0, 0);
            SvgUse imageUse = null;
            Rect iconExtent = Rect.Empty;

            string iconPath = graphObject.GetIcon();
            imageUse = ExportIcon(parent, iconPath, bounds.X, bounds.Y, out iconExtent);
            if (imageUse != null)
            {
                iconSize = iconExtent.Size;
            }
            if (foreground == null)
            {
                foreground = Brushes.Black;
            }
            foreground = GetContrasting(foreground, graphObject.GetRenderedBackground(), false);

            string label = null;
            GraphNode node = graphObject as GraphNode;
            if (node != null)
            {
                label = node.Label;
            }
            else
            {
                GraphLink link = graphObject as GraphLink;
                if (link != null)
                {
                    label = link.Label;
                }
            }

            Rect labelExtent = new Rect(0, 0, 0, 0);
            bool isMultiline = false;
            SvgGroup labelGroup = parent;
            SvgText firstLine = null;

            if (!string.IsNullOrWhiteSpace(label))
            {
                double? fontSize = graphObject.GetFontSize();
                if (fontSize == null)
                {
                    fontSize = control.FontSize;
                }
                Typeface typeface = GetTypeface(graphObject);

                int lineCount = 0;

                string fg = GetNamedBrush(null, foreground);
                SvgTextFormatter stf = new SvgTextFormatter();

                Rect labelBounds = bounds;
                double maxWidth = graphObject.GetValue<double>(NodePropertyExtensions.MaxWidth);

                if (maxWidth == 0)
                {
                    // not doing multi-line, so make sure FormatSvgTextRuns returns only 1 run.
                    labelBounds.Width += 50;
                }
                else
                {
                    // expecting multiple lines... but SVG might be off by a few pixels...                    
                    labelBounds.Width += 3;
                }

                foreach (SvgText line in stf.FormatSvgTextRuns(label, typeface, fontSize.Value, labelBounds, fg))
                {
                    if (firstLine == null)
                    {
                        firstLine = line;
                    }
                    else if (!isMultiline)
                    {
                        isMultiline = true;
                        labelGroup = new SvgGroup();
                        parent.AddChild(labelGroup);
                        parent.RemoveChild(firstLine);
                        labelGroup.AddChild(firstLine);
                    }
                    lineCount++;
                    if (typeface.FontFamily != control.FontFamily)
                    {
                        line.FontFamily = typeface.FontFamily.ToString();
                    }
                    if (fontSize.Value != control.FontSize)
                    {
                        line.FontSize = fontSize.Value;
                    }

                    labelGroup.AddChild(line);
                }
                labelExtent = stf.Extent;
            }

            const double gap = 5;
            var placement = graphObject.GetIconPlacement();

            double totalWidth = labelExtent.Width;
            double totalHeight = labelExtent.Height;

            if (imageUse != null)
            {
                switch (placement)
                {
                    case Dock.Bottom:
                    case Dock.Top:
                        totalWidth = Math.Max(totalWidth, iconSize.Width);
                        totalHeight += gap + iconSize.Height;
                        break;
                    case Dock.Left:
                    case Dock.Right:
                        totalWidth += gap + iconSize.Width;
                        totalHeight = Math.Max(totalHeight, iconSize.Height);
                        break;
                }
            }

            double dx = 0;
            double dy = 0;
            if (imageUse != null)
            {
                switch (node.GetIconPlacement())
                {
                    case Dock.Bottom:
                        dx = (int)((iconSize.Width - labelExtent.Width) / 2);
                        imageUse.Y += labelExtent.Height + gap;
                        if (dx < 0)
                        {
                            imageUse.X += -dx;
                            dx = 0;
                        }
                        break;
                    case Dock.Left:
                        dx = iconSize.Width + gap;
                        dy = (int)((iconSize.Height - labelExtent.Height) / 2);
                        if (dy < 0)
                        {
                            imageUse.Y += -dy;
                            dy = 0;
                        }
                        break;
                    case Dock.Right:
                        imageUse.X += labelExtent.Width + gap;
                        dy = (int)((iconSize.Height - labelExtent.Height) / 2);
                        if (dy < 0)
                        {
                            imageUse.Y += -dy;
                            dy = 0;
                        }
                        break;
                    case Dock.Top:
                        dy = iconSize.Height + gap;
                        dx = (int)((iconSize.Width - labelExtent.Width) / 2);
                        if (dx < 0)
                        {
                            imageUse.X += -dx;
                            dx = 0;
                        }
                        break;
                }
            }
            if (valign == VerticalAlignment.Center)
            {
                // then vertically center the icon and label inside the bigger node bounds.
                double vcy = (int)((bounds.Height - totalHeight) / 2);
                dy += vcy;
                if (imageUse != null)
                {
                    imageUse.Y += vcy;
                }
            }

            // horizontally center the icon and label inside the bigger node or group bounds.
            double vcx = (int)((bounds.Width - totalWidth) / 2);
            dx += vcx;
            if (imageUse != null)
            {
                imageUse.X += vcx;
            }


            if (dx != 0 || dy != 0)
            {
                if (isMultiline)
                {
                    labelGroup.Transform = string.Format("translate({0},{1})", dx, dy);
                }
                else if (firstLine != null)
                {
                    firstLine.X += dx;
                    firstLine.Y += dy;
                }
            }

            return new Size(totalWidth, totalHeight);
        }

        private string EncodeImage(ImageSource src)
        {
            PngBitmapEncoder encoder = new PngBitmapEncoder();

            BitmapSource bs = src as BitmapSource;
            if (bs != null)
            {
                encoder.Frames.Add(BitmapFrame.Create(bs));
            }
            else
            {
                Image img = new Image();
                img.Source = src;
                img.Width = src.Width;
                img.Height = src.Height;
                img.Arrange(new Rect(0, 0, src.Width, src.Height));
                RenderTargetBitmap bitmap = new RenderTargetBitmap((int)src.Width, (int)src.Height, 96, 95, PixelFormats.Pbgra32);
                bitmap.Render(img);
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
            }

            MemoryStream mem = new MemoryStream();
            encoder.Save(mem);

            byte[] data = mem.ToArray();
            return Convert.ToBase64String(data);
        }

        private string AddImageDef(string uri, ImageSource icon)
        {
            SvgObject img = svg.Defs.GetChild(uri);
            if (img != null)
            {
                // already added
                return uri;
            }
            string encoded = EncodeImage(icon);

            SvgImage image = new SvgImage()
            {
                Id = uri,
                Width = icon.Width,
                Height = icon.Height,
                Href = "data:image/png;base64," + encoded
            };

            svg.Defs.AddChild(image);

            return uri;
        }

        const double LegendMargin = 10;
        const double LegendItemSeparation = 7;
        const double LegendIconSize = 20;

        private Rect ExportLegend(SvgGroup root, Rect extent)
        {
            Rect bounds = new Rect(extent.Right, extent.Top, 0, 0);

            if (control.IsLegendVisible)
            {
                GraphLegend legend = control.Legend;
                Legend panel = legend.LegendPanel;
                ObservableCollection<LegendData> items = (ObservableCollection<LegendData>)panel.DataContext;
                List<double> rows = new List<double>();
                if (items.Count == 0)
                {
                    return bounds;
                }

                bounds.X += 20;
                double x = bounds.Left + LegendMargin;
                double y = bounds.Top + LegendMargin;

                double fontSize = control.FontSize;
                FormattedText ft = new FormattedText("LEGEND", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, fontSize, Brushes.Black);
                root.AddChild(new SvgText()
                {
                    Fill = "black",
                    Content = "LEGEND",
                    X = x,
                    Y = y + ft.Baseline
                });
                bounds.Width = ft.Width + (2 * LegendMargin);
                y += ft.Height + LegendItemSeparation;

                foreach (LegendData item in items)
                {
                    string label = item.Label;
                    x = bounds.Left + LegendMargin;
                    ft = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, fontSize, Brushes.Black);
                    SvgText text = new SvgText()
                    {
                        Fill = "black",
                        Content = label,
                        X = x,
                        Y = y + ft.Baseline
                    };
                    root.AddChild(text);
                    rows.Add(y);
                    y += ft.Height + LegendItemSeparation;
                    bounds.Width = Math.Max(bounds.Width, ft.Width + (2 * LegendMargin));
                }

                PropertyInfo getter = null;
                Graph graph = control.Graph;
                int maxIcons = 0;
                int index = 0;
                foreach (LegendData item in items)
                {
                    y = rows[index++];
                    x = bounds.Right + LegendMargin;

                    for (int i = 0, n = item.Values.Count; i < n && i < 6; i++)
                    {
                        maxIcons = Math.Max(maxIcons, i + 1);
                        LegendValue v = item.Values[i];
                        if (getter == null)
                        {
                            getter = v.GetType().GetProperty("Style", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        }
                        GraphConditionalStyle style = (GraphConditionalStyle)getter.GetValue(v, null);
                        string background = null;
                        GraphSetter s = style.Setters.Get("Background");
                        if (s != null)
                        {
                            background = GetNamedBrush(null, s.TypedValue as Brush);
                        }
                        string stroke = null;
                        s = style.Setters.Get("Stroke");
                        if (s != null)
                        {
                            stroke = GetNamedBrush(null, s.TypedValue as Brush);
                        }

                        if (background != null || stroke != null)
                        {
                            root.AddChild(new SvgRectangle()
                            {
                                X = x,
                                Y = y,
                                Width = LegendIconSize,
                                Height = LegendIconSize,
                                Fill = background,
                                Stroke = stroke,
                                StrokeWidth = (stroke == null) ? 0 : 1
                            });
                        }

                        s = style.Setters.Get("Icon");
                        if (s != null)
                        {
                            string path = s.Value;
                            if (!string.IsNullOrEmpty(path))
                            {
                                Rect imgExtent;
                                SvgUse use = ExportIcon(root, path, x + 2, y + 2, out imgExtent);
                                double scale = Math.Min(20 / imgExtent.Width, 20 / imgExtent.Height);
                                ScaleIcon(use, scale, scale);
                            }
                        }

                        string foreground = null;
                        bool hasExpression = false;
                        s = style.Setters.Get("Foreground");
                        if (s != null)
                        {
                            hasExpression = string.IsNullOrEmpty(s.Expression);
                            foreground = GetNamedBrush(null, s.TypedValue as Brush);
                        }
                        if (foreground == null && hasExpression)
                        {
                            foreground = "black"; // todo: get good contrast.
                        }
                        if (foreground != null)
                        {
                            ft = new FormattedText("f", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, fontSize, Brushes.Black);
                            root.AddChild(new SvgText()
                            {
                                X = x + (LegendIconSize - ft.Width) / 2,
                                Y = y + ft.Baseline + (LegendIconSize - ft.Height) / 2,
                                Content = "f",
                                Fill = foreground
                            });
                        }

                        x += LegendIconSize;
                    }
                }

                bounds.Width += LegendMargin + (maxIcons * LegendIconSize) + LegendMargin;

                bounds.Height = y - bounds.Top + LegendIconSize + LegendMargin;

                root.AddChild(new SvgRectangle()
                {
                    Fill = null,
                    Stroke = "black",
                    StrokeWidth = 1,
                    X = bounds.Left,
                    Y = bounds.Top,
                    Width = bounds.Width,
                    Height = bounds.Height
                });

            }
            return bounds;
        }

        private static Brush GetContrasting(Brush foreground, Brush background, bool linkLabel)
        {
            if (background != null)
            {
                Color backgroundColor = background.GetBrushColor();
                Color foregroundColor = foreground.GetBrushColor();

                // Get the Contrast color that best suits this foreground/background pair
                Color contrastColor;
                if (linkLabel)
                {
                    // links are less aggressive
                    contrastColor = foregroundColor.GetReasonableContrastColor(backgroundColor);
                }
                else
                {
                    contrastColor = foregroundColor.GetContrastColor(backgroundColor, 0);
                }
                return new SolidColorBrush(contrastColor);
            }
            return foreground;
        }

    }

    public abstract class SvgObject : IXmlSerializable
    {
        public string Id { get; set; }

        public string Fill { get; set; }

        public string Stroke { get; set; }

        public double StrokeWidth { get; set; }

        public string StrokeLineJoin { get; set; }

        public string StrokeLineCap { get; set; }

        public string FontFamily { get; set; }

        public double FontSize { get; set; }

        public string Transform { get; set; }

        public System.Xml.Schema.XmlSchema GetSchema()
        {
            return null;
        }

        public virtual void ReadXml(XmlReader reader)
        {
            while (reader.MoveToNextAttribute())
            {
                ReadAttribute(reader.LocalName, reader.NamespaceURI, reader.Value);
            }
        }

        protected virtual bool ReadAttribute(string name, string namespaceUri, string value)
        {
            switch (name)
            {
                case "id":
                    Id = value;
                    break;
                case "fill":
                    Fill = value;
                    break;
                case "stroke":
                    Stroke = value;
                    break;
                case "stroke-width":
                    StrokeWidth = XmlExtensions.ToDouble(value);
                    break;
                case "stroke-linejoin":
                    StrokeLineJoin = value;
                    break;
                case "stroke-linecap":
                    StrokeLineCap = value;
                    break;
                case "font-size":
                    FontSize = XmlExtensions.ToDouble(value);
                    break;
                case "font-family":
                    FontFamily = value;
                    break;
                case "transform":
                    Transform = value;
                    break;
                default:
                    return false;
            }
            return true;
        }

        public virtual bool HasDefaultFill { get { return false; } }

        public virtual bool HasDefaultStroke { get { return false; } }

        public virtual void WriteXml(XmlWriter writer)
        {
            if (Id != null)
            {
                writer.WriteAttributeString("id", Id);
            }
            if (Fill != null)
            {
                writer.WriteAttributeString("fill", Fill);
            }
            else if (HasDefaultFill)
            {
                writer.WriteAttributeString("fill", "none");
            }
            if (Stroke != null)
            {
                writer.WriteAttributeString("stroke", Stroke);
            }
            else if (HasDefaultStroke)
            {
                writer.WriteAttributeString("stroke", "none");
            }
            if (StrokeWidth != 0)
            {
                writer.WriteAttributeString("stroke-width", XmlConvert.ToString(StrokeWidth));
            }
            if (StrokeLineJoin != null)
            {
                writer.WriteAttributeString("stroke-linejoin", StrokeLineJoin);
            }
            if (StrokeLineCap != null)
            {
                writer.WriteAttributeString("stroke-linecap", StrokeLineCap);
            }

            if (FontSize != 0)
            {
                writer.WriteAttributeString("font-size", XmlConvert.ToString(FontSize));
            }
            if (FontFamily != null)
            {
                writer.WriteAttributeString("font-family", FontFamily);
            }
            if (Transform != null)
            {
                writer.WriteAttributeString("transform", Transform);
            }
        }
    }

    // <rect x="0" y="0" width="400" height="200" rx="50" ry="50"
    //        fill="none" stroke="purple" stroke-width="30"/>
    public class SvgRectangle : SvgObject
    {
        public SvgRectangle()
        {
        }

        public override bool HasDefaultFill { get { return true; } }

        [XmlAttribute("x")]
        public double X { get; set; }

        [XmlAttribute("y")]
        public double Y { get; set; }

        [XmlAttribute("rx")]
        public double RadiusX { get; set; }

        [XmlAttribute("ry")]
        public double RadiusY { get; set; }

        [XmlAttribute("width")]
        public double Width { get; set; }

        [XmlAttribute("height")]
        public double Height { get; set; }

        protected override bool ReadAttribute(string name, string namespaceUri, string value)
        {
            if (!base.ReadAttribute(name, namespaceUri, value))
            {
                switch (name)
                {
                    case "x":
                        X = XmlExtensions.ToDouble(value);
                        break;
                    case "y":
                        Y = XmlExtensions.ToDouble(value);
                        break;
                    case "rx":
                        RadiusX = XmlExtensions.ToDouble(value);
                        break;
                    case "ry":
                        RadiusY = XmlExtensions.ToDouble(value);
                        break;
                    case "width":
                        Width = XmlExtensions.ToDouble(value);
                        break;
                    case "height":
                        Height = XmlExtensions.ToDouble(value);
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }

        public override void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("rect");
            if (X != 0)
            {
                writer.WriteAttributeString("x", XmlConvert.ToString(X));
            }
            if (Y != 0)
            {
                writer.WriteAttributeString("y", XmlConvert.ToString(Y));
            }
            if (RadiusX != 0)
            {
                writer.WriteAttributeString("rx", XmlConvert.ToString(RadiusX));
            }
            if (RadiusY != 0)
            {
                writer.WriteAttributeString("ry", XmlConvert.ToString(RadiusY));
            }
            if (Width != 0)
            {
                writer.WriteAttributeString("width", XmlConvert.ToString(Width));
            }
            if (Height != 0)
            {
                writer.WriteAttributeString("height", XmlConvert.ToString(Height));
            }
            base.WriteXml(writer);

            writer.WriteEndElement();
        }
    }

    // <path d="M 100 100 L 300 100 L 200 300 z"
    //     fill="red" stroke="blue" stroke-width="3" />
    public class SvgPath : SvgObject
    {
        public SvgPath()
        {
        }

        public override bool HasDefaultFill { get { return true; } }

        [XmlAttribute("d")]
        public string Data { get; set; }

        protected override bool ReadAttribute(string name, string namespaceUri, string value)
        {
            if (!base.ReadAttribute(name, namespaceUri, value))
            {
                switch (name)
                {
                    case "d":
                        Data = value;
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }

        public override void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("path");
            if (Data != null)
            {
                writer.WriteAttributeString("d", Data);
            }
            base.WriteXml(writer);

            writer.WriteEndElement();
        }
    }

    public class SvgUse : SvgObject
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public string Href { get; set; }

        public SvgUse()
        {
        }

        public override void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("use");

            base.WriteXml(writer);

            if (X != 0)
            {
                writer.WriteAttributeString("x", XmlConvert.ToString(X));
            }
            if (Y != 0)
            {
                writer.WriteAttributeString("y", XmlConvert.ToString(Y));
            }
            if (Width != 0)
            {
                writer.WriteAttributeString("width", XmlConvert.ToString(Width));
            }
            if (Height != 0)
            {
                writer.WriteAttributeString("height", XmlConvert.ToString(Height));
            }
            if (Href != null)
            {
                writer.WriteAttributeString("xlink", "href", Svg.XLinkNamepsace, Href);
            }
            writer.WriteEndElement();
        }

        protected override bool ReadAttribute(string name, string namespaceUri, string value)
        {
            if (!base.ReadAttribute(name, namespaceUri, value))
            {
                switch (name)
                {
                    case "x":
                        X = XmlExtensions.ToDouble(value);
                        break;
                    case "y":
                        Y = XmlExtensions.ToDouble(value);
                        break;
                    case "width":
                        Width = XmlExtensions.ToDouble(value);
                        break;
                    case "height":
                        Height = XmlExtensions.ToDouble(value);
                        break;
                    case "href":
                        Href = value;
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }
    }

    public class SvgDefs : SvgContainer
    {
        private Dictionary<string, SvgObject> index = new Dictionary<string, SvgObject>();

        public SvgDefs()
        {
        }

        public override void AddChild(SvgObject child)
        {
            base.AddChild(child);
            if (child.Id != null)
            {
                index[child.Id] = child;
            }
        }

        public SvgObject GetChild(string id)
        {
            SvgObject result = null;
            index.TryGetValue(id, out result);
            return result;
        }

        public override void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("defs");

            // none of the default attributes.
            //base.WriteXml(writer);

            WriteChildren(writer);
            writer.WriteEndElement();
        }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            ReadChildren(reader);
        }

    }

    public class SvgText : SvgObject
    {
        public double X { get; set; }

        public double Y { get; set; }

        // XML Serializer can't hide this field name... so we need custom IXmlSerializable for this
        public string Content { get; set; }

        public override bool HasDefaultFill { get { return true; } }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            Content = reader.ReadString();
        }

        protected override bool ReadAttribute(string name, string namespaceUri, string value)
        {
            if (!base.ReadAttribute(name, namespaceUri, value))
            {
                switch (name)
                {
                    case "x":
                        X = XmlExtensions.ToDouble(value);
                        break;
                    case "y":
                        Y = XmlExtensions.ToDouble(value);
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }

        public override void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("text");
            if (X != 0)
            {
                writer.WriteAttributeString("x", XmlConvert.ToString(X));
            }
            if (Y != 0)
            {
                writer.WriteAttributeString("y", XmlConvert.ToString(Y));
            }
            base.WriteXml(writer);

            if (this.Content != null)
            {
                writer.WriteString(this.Content);
            }
            writer.WriteEndElement();
        }
    }

    public class SvgLinearGradientStop : SvgObject
    {
        public string Offset { get; set; }
        public string StopColor { get; set; }
        public string StopOpacity { get; set; }

        protected override bool ReadAttribute(string name, string namespaceUri, string value)
        {
            if (!base.ReadAttribute(name, namespaceUri, value))
            {
                switch (name)
                {
                    case "offset":
                        Offset = value;
                        break;
                    case "stop-color":
                        StopColor = value;
                        break;
                    case "stop-opacity":
                        StopOpacity = value;
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }

        public override void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("stop");
            if (Offset != null)
            {
                writer.WriteAttributeString("offset", Offset);
            }
            if (StopColor != null)
            {
                writer.WriteAttributeString("stop-color", StopColor);
            }
            if (StopOpacity != null)
            {
                writer.WriteAttributeString("StopOpacity", StopOpacity);
            }

            base.WriteXml(writer);

            writer.WriteEndElement();
        }
    }

    /*
       <linearGradient id="myHorizonalgreen" x1="0%" y1="0%" x2="100%" y2="0%">
         <stop offset="10%" stop-color="#00cc00" stop-opacity="5"/>
         <stop offset="100%" stop-color="#00b400" stop-opacity="5"/> 
        </linearGradient>
    */
    public class SvgLinearGradient : SvgContainer
    {
        public string x1 { get; set; }
        public string y1 { get; set; }
        public string x2 { get; set; }
        public string y2 { get; set; }

        protected override bool ReadAttribute(string name, string namespaceUri, string value)
        {
            if (!base.ReadAttribute(name, namespaceUri, value))
            {
                switch (name)
                {
                    case "id":
                        Id = value;
                        break;
                    case "x1":
                        x1 = value;
                        break;
                    case "y1":
                        y1 = value;
                        break;
                    case "x2":
                        x2 = value;
                        break;
                    case "y2":
                        y2 = value;
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }

        public override void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("linearGradient");

            if (x1 != null)
            {
                writer.WriteAttributeString("x1", x1);
            }
            if (y1 != null)
            {
                writer.WriteAttributeString("y1", y1);
            }
            if (x2 != null)
            {
                writer.WriteAttributeString("x2", x2);
            }
            if (y2 != null)
            {
                writer.WriteAttributeString("y2", y2);
            }

            base.WriteXml(writer);

            WriteChildren(writer);
            writer.WriteEndElement();
        }
    }

    public class SvgContainer : SvgObject
    {
        List<SvgObject> children;

        public virtual void AddChild(SvgObject child)
        {
            if (children == null)
            {
                children = new List<SvgObject>();
            }
            children.Add(child);
        }

        public virtual void RemoveChild(SvgObject child)
        {
            if (children == null)
            {
                return;
            }
            children.Remove(child);
        }

        public void WriteChildren(XmlWriter writer)
        {
            if (children != null)
            {
                foreach (SvgObject o in children)
                {
                    o.WriteXml(writer);
                }
            }
        }

        public virtual SvgObject ReadChild(string localName, string namespaceURI, XmlReader reader)
        {
            SvgObject child = null;
            switch (reader.LocalName)
            {
                case "text":
                    child = new SvgText();
                    break;
                case "rect":
                    child = new SvgRectangle();
                    break;
                case "path":
                    child = new SvgPath();
                    break;
                case "g":
                    child = new SvgGroup();
                    break;
                case "image":
                    child = new SvgImage();
                    break;
                default:
                    break;
            }
            return child;
        }

        public void ReadChildren(XmlReader reader)
        {
            while (reader.Read() && reader.NodeType != XmlNodeType.EndElement)
            {
                SvgObject child = ReadChild(reader.LocalName, reader.NamespaceURI, reader);
                if (child != null)
                {
                    child.ReadXml(reader);
                    AddChild(child);
                }
            }
        }
    }

    public class SvgGroup : SvgContainer
    {
        public SvgGroup()
        {
        }

        public override void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("g");
            base.WriteXml(writer);
            WriteChildren(writer);
            writer.WriteEndElement();
        }

        protected override bool ReadAttribute(string name, string namespaceUri, string value)
        {
            if (!base.ReadAttribute(name, namespaceUri, value))
            {
                switch (name)
                {
                    default:
                        return false;
                }
            }
            return true;
        }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            ReadChildren(reader);
        }
    }

    public class SvgImage : SvgObject
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public string Href { get; set; }

        public override void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("image");
            base.WriteXml(writer);

            if (X != 0)
            {
                writer.WriteAttributeString("x", XmlConvert.ToString(X));
            }
            if (Y != 0)
            {
                writer.WriteAttributeString("y", XmlConvert.ToString(Y));
            }
            if (Width != 0)
            {
                writer.WriteAttributeString("width", XmlConvert.ToString(Width));
            }
            if (Height != 0)
            {
                writer.WriteAttributeString("height", XmlConvert.ToString(Height));
            }
            if (Href != null)
            {
                writer.WriteAttributeString("xlink", "href", Svg.XLinkNamepsace, Href);
            }
            writer.WriteEndElement();
        }

        protected override bool ReadAttribute(string name, string namespaceUri, string value)
        {
            if (!base.ReadAttribute(name, namespaceUri, value))
            {
                switch (name)
                {
                    case "x":
                        X = XmlExtensions.ToDouble(value);
                        break;
                    case "y":
                        Y = XmlExtensions.ToDouble(value);
                        break;
                    case "width":
                        Width = XmlExtensions.ToDouble(value);
                        break;
                    case "height":
                        Height = XmlExtensions.ToDouble(value);
                        break;
                    case "xlink:href":
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }


    }

    [XmlRoot(ElementName = "svg", Namespace = "http://www.w3.org/2000/svg")]
    public class Svg : SvgContainer
    {
        public static string XLinkNamepsace = "http://www.w3.org/1999/xlink";
        public static string XmlnsNamespace = "http://www.w3.org/2000/xmlns/";

        public double Width { get; set; }

        public double Height { get; set; }

        public SvgDefs Defs { get; set; }

        public Svg()
        {
            AddChild(Defs = new SvgDefs());
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);

            if (Width != 0)
            {
                writer.WriteAttributeString("width", XmlConvert.ToString(Width));
            }
            if (Height != 0)
            {
                writer.WriteAttributeString("height", XmlConvert.ToString(Height));
            }

            writer.WriteAttributeString("xmlns", "xlink", XmlnsNamespace, Svg.XLinkNamepsace);

            WriteChildren(writer);
        }
        protected override bool ReadAttribute(string name, string namespaceUri, string value)
        {
            if (!base.ReadAttribute(name, namespaceUri, value))
            {
                switch (name)
                {
                    case "width":
                        Width = XmlExtensions.ToDouble(value);
                        break;
                    case "height":
                        Height = XmlExtensions.ToDouble(value);
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            ReadChildren(reader);
        }

        public override SvgObject ReadChild(string localName, string namespaceURI, XmlReader reader)
        {
            SvgObject child = base.ReadChild(localName, namespaceURI, reader);
            if (child == null)
            {
                if (localName == "defs")
                {
                    return Defs = new SvgDefs();
                }
            }
            return null;
        }
    }


    static class XmlExtensions
    {
        public static string HexDigits(byte v)
        {
            return HexDigit((byte)(v >> 4)) + HexDigit((byte)(v % 16));
        }

        private static string HexDigit(byte v)
        {
            if (v >= 0 && v <= 9)
            {
                return v.ToString();
            }
            else if (v < 16)
            {
                return Convert.ToChar(Convert.ToInt32('A') + (v - 10)).ToString();
            }
            else
            {
                throw new ArgumentOutOfRangeException("Argument must be less than 16");
            }
        }

        public static Brush ToBrush(string value)
        {
            try
            {
                if (value == null || string.IsNullOrWhiteSpace(value) || value.Trim().ToLowerInvariant() == "none")
                {
                    return null;
                }
                ColorConverter cc = new ColorConverter();
                Color c = (Color)cc.ConvertFromInvariantString(value);
                return new SolidColorBrush(c);
            }
            catch
            {
                return Brushes.Red;
            }
        }

        public static double ToDouble(string value)
        {
            double result = 0;
            double.TryParse(value, out result);
            return result;
        }

        internal static Point GetEndPoint(Geometry g)
        {
            LineGeometry line = g as LineGeometry;
            if (line != null)
            {
                return line.EndPoint;
            }

            PathGeometry path = g as PathGeometry;
            if (path != null)
            {
                foreach (PathFigure fig in path.Figures)
                {
                    Point pt = fig.StartPoint;

                    PathSegment seg = fig.Segments[fig.Segments.Count - 1];
                    if (seg != null)
                    {
                        LineSegment lineseg = seg as LineSegment;
                        if (lineseg != null)
                        {
                            return lineseg.Point;
                        }
                        BezierSegment bez = seg as BezierSegment;
                        if (bez != null)
                        {
                            return bez.Point3;
                        }
                        QuadraticBezierSegment quad = seg as QuadraticBezierSegment;
                        if (quad != null)
                        {
                            // todo: can SVG draw these?
                        }
                    }
                }
            }
            return new Point(0, 0);

        }


        internal static string ToString(Geometry g)
        {
            LineGeometry line = g as LineGeometry;
            if (line != null)
            {
                return "M " + ToString(line.StartPoint) + " L " + ToString(line.EndPoint);
            }

            PathGeometry path = g as PathGeometry;
            if (path != null)
            {
                StringBuilder sb = new StringBuilder();
                foreach (PathFigure fig in path.Figures)
                {
                    Point start = fig.StartPoint;

                    sb.Append("M ");
                    sb.Append(ToString(start));

                    foreach (PathSegment seg in fig.Segments)
                    {
                        LineSegment lineseg = seg as LineSegment;
                        if (lineseg != null)
                        {
                            sb.Append(" L ");
                            sb.Append(ToString(lineseg.Point));
                            continue;
                        }
                        BezierSegment bez = seg as BezierSegment;
                        if (bez != null)
                        {
                            sb.Append(" C ");
                            sb.Append(ToString(bez.Point1));
                            sb.Append(" ");
                            sb.Append(ToString(bez.Point2));
                            sb.Append(" ");
                            sb.Append(ToString(bez.Point3));
                            continue;
                        }
                        QuadraticBezierSegment quad = seg as QuadraticBezierSegment;
                        if (quad != null)
                        {
                            // todo: can SVG draw these?
                        }
                    }

                    if (fig.IsClosed)
                    {
                        sb.Append(" z");
                    }

                    break; // todo: more than one figure?
                }
                return sb.ToString();
            }

            return "unknown geometry type " + g.GetType().Name;
        }

        internal static string ToString(Point p)
        {
            return p.X.ToString(CultureInfo.InvariantCulture) + "," +
                   p.Y.ToString(CultureInfo.InvariantCulture);
        }
    }

    // SvgTextSource is our implementation of TextSource that is used in the TextFormatter to
    // capture line information.
    internal class SvgTextFormatter : TextSource
    {
        public Rect Extent { get; set; }

        const double IconLabelImageGap = 5;

        public IEnumerable<SvgText> FormatSvgTextRuns(string text, Typeface typeface, double fontSize, Rect labelBounds, string foreground)
        {
            Extent = Rect.Empty;

            double maxWidth = labelBounds.Width;
            FormattedText ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface,
                    fontSize, Brushes.Black);
            ft.MaxTextWidth = maxWidth;
            double baseline = ft.Baseline;

            Size labelSize = new Size(ft.Width, ft.Height);

            StringBuilder sb = new StringBuilder();

            int textStorePosition = 0;                //Index into the text of the textsource
            Point linePosition = new Point(labelBounds.X, labelBounds.Y);

            // Update the text store.
            this.Text = text;
            this.FontRendering = new FontRendering(fontSize, TextAlignment.Left, null, Brushes.Black, typeface);

            // Create a TextFormatter object.
            TextFormatter formatter = TextFormatter.Create();

            // Format each line of text from the text store and draw it.
            int length = this.Text.Length;
            while (textStorePosition < length)
            {
                // Create a textline from the text store using the TextFormatter object.
                using (TextLine line = formatter.FormatLine(
                    this,
                    textStorePosition,
                    maxWidth,
                    new GenericTextParagraphProperties(this.FontRendering),
                    null))
                {
                    int lineLength = line.Length;
                    int end = Math.Min(textStorePosition + lineLength, length);

                    Extent = Rect.Union(Extent, new Rect(linePosition.X, linePosition.Y, line.Width, line.Height));

                    // Yield the formatted Svg text .
                    yield return new SvgText()
                    {
                        Content = text.Substring(textStorePosition, end - textStorePosition),
                        X = linePosition.X,
                        Y = linePosition.Y + baseline,
                        Fill = foreground,
                    };

                    // Update the index position in the text store.
                    textStorePosition += lineLength;

                    // Update the line position coordinate for the displayed line.
                    linePosition.Y += line.Height;
                }
            }
        }


        // Used by the TextFormatter object to retrieve a run of text from the text source.
        public override TextRun GetTextRun(int textSourceCharacterIndex)
        {
            // Make sure text source index is in bounds.
            if (textSourceCharacterIndex < 0)
                throw new ArgumentOutOfRangeException("textSourceCharacterIndex", "Value must be greater than 0.");
            if (textSourceCharacterIndex >= _text.Length)
            {
                return new TextEndOfParagraph(1);
            }

            // Create TextCharacters using the current font rendering properties.
            if (textSourceCharacterIndex < _text.Length)
            {
                return new TextCharacters(
                   _text,
                   textSourceCharacterIndex,
                   _text.Length - textSourceCharacterIndex,
                   new GenericTextRunProperties(_currentRendering));
            }

            // Return an end-of-paragraph if no more text source.
            return new TextEndOfParagraph(1);
        }

        public override TextSpan<CultureSpecificCharacterBufferRange> GetPrecedingText(int textSourceCharacterIndexLimit)
        {
            CharacterBufferRange cbr = new CharacterBufferRange(_text, 0, textSourceCharacterIndexLimit);
            return new TextSpan<CultureSpecificCharacterBufferRange>(
             textSourceCharacterIndexLimit,
             new CultureSpecificCharacterBufferRange(System.Globalization.CultureInfo.CurrentUICulture, cbr)
             );
        }

        public override int GetTextEffectCharacterIndexFromTextSourceCharacterIndex(int textSourceCharacterIndex)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #region Properties
        public string Text
        {
            get { return _text; }
            set { _text = value; }
        }

        public FontRendering FontRendering
        {
            get { return _currentRendering; }
            set { _currentRendering = value; }
        }
        #endregion

        #region Private Fields

        private string _text;      //text store
        private FontRendering _currentRendering;

        #endregion
    }
    /// <summary>
    /// Class to implement TextParagraphProperties, used by TextSource
    /// </summary>
    class GenericTextParagraphProperties : TextParagraphProperties
    {
        #region Constructors
        public GenericTextParagraphProperties(
           FlowDirection flowDirection,
           TextAlignment textAlignment,
           bool firstLineInParagraph,
           bool alwaysCollapsible,
           TextRunProperties defaultTextRunProperties,
           TextWrapping textWrap,
           double lineHeight,
           double indent)
        {
            _flowDirection = flowDirection;
            _textAlignment = textAlignment;
            _firstLineInParagraph = firstLineInParagraph;
            _alwaysCollapsible = alwaysCollapsible;
            _defaultTextRunProperties = defaultTextRunProperties;
            _textWrap = textWrap;
            _lineHeight = lineHeight;
            _indent = indent;
        }

        public GenericTextParagraphProperties(FontRendering newRendering)
        {
            _flowDirection = FlowDirection.LeftToRight;
            _textAlignment = newRendering.TextAlignment;
            _firstLineInParagraph = false;
            _alwaysCollapsible = false;
            _defaultTextRunProperties = new GenericTextRunProperties(
               newRendering.Typeface, newRendering.FontSize, newRendering.FontSize,
               newRendering.TextDecorations, newRendering.TextColor, null,
               BaselineAlignment.Baseline, CultureInfo.CurrentUICulture);
            _textWrap = TextWrapping.Wrap;
            _lineHeight = 0;
            _indent = 0;
            _paragraphIndent = 0;
        }
        #endregion

        #region Properties
        public override FlowDirection FlowDirection
        {
            get { return _flowDirection; }
        }

        public override TextAlignment TextAlignment
        {
            get { return _textAlignment; }
        }

        public override bool FirstLineInParagraph
        {
            get { return _firstLineInParagraph; }
        }

        public override bool AlwaysCollapsible
        {
            get { return _alwaysCollapsible; }
        }

        public override TextRunProperties DefaultTextRunProperties
        {
            get { return _defaultTextRunProperties; }
        }

        public override TextWrapping TextWrapping
        {
            get { return _textWrap; }
        }

        public override double LineHeight
        {
            get { return _lineHeight; }
        }

        public override double Indent
        {
            get { return _indent; }
        }

        public override TextMarkerProperties TextMarkerProperties
        {
            get { return null; }
        }

        public override double ParagraphIndent
        {
            get { return _paragraphIndent; }
        }
        #endregion

        #region Private Fields
        private FlowDirection _flowDirection;
        private TextAlignment _textAlignment;
        private bool _firstLineInParagraph;
        private bool _alwaysCollapsible;
        private TextRunProperties _defaultTextRunProperties;
        private TextWrapping _textWrap;
        private double _indent;
        private double _paragraphIndent;
        private double _lineHeight;
        #endregion
    }

    /// <summary>
    /// Class used to implement TextRunProperties
    /// </summary>
    class GenericTextRunProperties : TextRunProperties
    {
        #region Constructors
        public GenericTextRunProperties(
           Typeface typeface,
           double size,
           double hintingSize,
           TextDecorationCollection textDecorations,
           Brush forgroundBrush,
           Brush backgroundBrush,
           BaselineAlignment baselineAlignment,
           CultureInfo culture)
        {
            if (typeface == null)
                throw new ArgumentNullException("typeface");

            ValidateCulture(culture);


            _typeface = typeface;
            _emSize = size;
            _emHintingSize = hintingSize;
            _textDecorations = textDecorations;
            _foregroundBrush = forgroundBrush;
            _backgroundBrush = backgroundBrush;
            _baselineAlignment = baselineAlignment;
            _culture = culture;
        }

        public GenericTextRunProperties(FontRendering newRender)
        {
            _typeface = newRender.Typeface;
            _emSize = newRender.FontSize;
            _emHintingSize = newRender.FontSize;
            _textDecorations = newRender.TextDecorations;
            _foregroundBrush = newRender.TextColor;
            _backgroundBrush = null;
            _baselineAlignment = BaselineAlignment.Baseline;
            _culture = CultureInfo.CurrentUICulture;
        }
        #endregion

        #region Private Methods
        private static void ValidateCulture(CultureInfo culture)
        {
            if (culture == null)
                throw new ArgumentNullException("culture");
            if (culture.IsNeutralCulture || culture.Equals(CultureInfo.InvariantCulture))
                throw new ArgumentException("Specific Culture Required", "culture");
        }

        private static void ValidateFontSize(double emSize)
        {
            if (emSize <= 0)
                throw new ArgumentOutOfRangeException("emSize", "Parameter Must Be Greater Than Zero.");
            //if (emSize > MaxFontEmSize)
            //   throw new ArgumentOutOfRangeException("emSize", "Parameter Is Too Large.");
            if (double.IsNaN(emSize))
                throw new ArgumentOutOfRangeException("emSize", "Parameter Cannot Be NaN.");
        }
        #endregion

        #region Properties
        public override Typeface Typeface
        {
            get { return _typeface; }
        }

        public override double FontRenderingEmSize
        {
            get { return _emSize; }
        }

        public override double FontHintingEmSize
        {
            get { return _emHintingSize; }
        }

        public override TextDecorationCollection TextDecorations
        {
            get { return _textDecorations; }
        }

        public override Brush ForegroundBrush
        {
            get { return _foregroundBrush; }
        }

        public override Brush BackgroundBrush
        {
            get { return _backgroundBrush; }
        }

        public override BaselineAlignment BaselineAlignment
        {
            get { return _baselineAlignment; }
        }

        public override CultureInfo CultureInfo
        {
            get { return _culture; }
        }

        public override TextRunTypographyProperties TypographyProperties
        {
            get { return null; }
        }

        public override TextEffectCollection TextEffects
        {
            get { return null; }
        }

        public override NumberSubstitution NumberSubstitution
        {
            get { return null; }
        }
        #endregion

        #region Private Fields
        private Typeface _typeface;
        private double _emSize;
        private double _emHintingSize;
        private TextDecorationCollection _textDecorations;
        private Brush _foregroundBrush;
        private Brush _backgroundBrush;
        private BaselineAlignment _baselineAlignment;
        private CultureInfo _culture;
        #endregion
    }

    /// <summary>
    /// Class for combining Font and other text related properties. 
    /// (Typeface, Alignment, Decorations, etc)
    /// </summary>
    class FontRendering
    {
        #region Constructors
        public FontRendering(
           double emSize,
           TextAlignment alignment,
           TextDecorationCollection decorations,
           Brush textColor,
           Typeface face)
        {
            _fontSize = emSize;
            _alignment = alignment;
            _textDecorations = decorations;
            _textColor = textColor;
            _typeface = face;
        }

        public FontRendering()
        {
            _fontSize = 12.0f;
            _alignment = TextAlignment.Left;
            _textDecorations = new TextDecorationCollection();
            _textColor = Brushes.Black;
            _typeface = new Typeface(new FontFamily("Arial"),
               FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        }
        #endregion

        #region Properties
        public double FontSize
        {
            get { return _fontSize; }
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException("value", "Parameter Must Be Greater Than Zero.");
                if (double.IsNaN(value))
                    throw new ArgumentOutOfRangeException("value", "Parameter Cannot Be NaN.");
                _fontSize = value;
            }
        }

        public TextAlignment TextAlignment
        {
            get { return _alignment; }
            set { _alignment = value; }
        }

        public TextDecorationCollection TextDecorations
        {
            get { return _textDecorations; }
            set { _textDecorations = value; }
        }

        public Brush TextColor
        {
            get { return _textColor; }
            set { _textColor = value; }
        }

        public Typeface Typeface
        {
            get { return _typeface; }
            set { _typeface = value; }
        }
        #endregion

        #region Private Fields
        private double _fontSize;
        private TextAlignment _alignment;
        private TextDecorationCollection _textDecorations;
        private Brush _textColor;
        private Typeface _typeface;
        #endregion
    }
}
