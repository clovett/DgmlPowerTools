using System.Collections.Generic;
using System.Windows.Media;
using System.Xml;
using System.Xml.Serialization;

namespace LovettSoftware.DgmlPowerTools
{
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

        internal string ToSvgColor(Color c)
        {
            // SVG does not support providing Alpha channels in the colors.
            return "#" + XmlExtensions.HexDigits(c.R) + XmlExtensions.HexDigits(c.G) + XmlExtensions.HexDigits(c.B);
        }

        internal string ToSvgColor(string color)
        {
            Color c = (Color)ColorConverter.ConvertFromString(color);
            return ToSvgColor(c);
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
                writer.WriteAttributeString("fill", ToSvgColor(Fill));
            }
            else if (HasDefaultFill)
            {
                writer.WriteAttributeString("fill", "none");
            }
            if (Stroke != null)
            {
                writer.WriteAttributeString("stroke", ToSvgColor(Stroke));
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

        [XmlAttribute("stroke-dasharray")]
        public double[] Dashes { get; set; }

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
            if (Dashes != null)
            {
                writer.WriteAttributeString("stroke-dasharray", string.Join(",", Dashes));
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
                writer.WriteAttributeString("stop-color", ToSvgColor(StopColor));
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

}
