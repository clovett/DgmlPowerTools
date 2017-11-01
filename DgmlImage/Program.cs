using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Progression;
using Microsoft.VisualStudio.GraphModel;
using Microsoft.VisualStudio.Progression.WpfCommon;
using Microsoft.VisualStudio.Diagrams.Layout;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.Diagrams.View;
using Microsoft.VisualStudio.Diagrams.Gestures;
using Microsoft.VisualStudio.GraphModel.Schemas;
using Microsoft.Win32;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Controls;
using LovettSoftware.DgmlPowerTools;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Security;

namespace DgmlImage
{
    class Program : IUIHost, IGraphStatus
    {
        Dispatcher dispatcher;

        [STAThread]
        static int Main(string[] args)
        {
            Program p = new Program();
            if (!p.ParseCommandLine(args))
            {
                PrintUsage();
                return 1;
            }
            try
            {
                p.Initialize();
                p.ProcessFiles();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return 1;
            }
            return 0;
        }

        private void Initialize()
        {
            dispatcher = Dispatcher.CurrentDispatcher;

            UIHost.Host = this;

            VisualGraphProperties.Initialize();
            GraphObjectExtensions.Initialize();
            NodeCategories.Initialize();
            LinkCategories.Initialize();

            // initialize WPF pack:// URI format
            Control c = new Control();

            LoadIcons();
        }

        string GetVsInstallDir()
        {
            string vsKey = @"Software\Microsoft\VisualStudio\11.0";
            if (Environment.Is64BitOperatingSystem && !IsWow64)
            {
                vsKey = @"Software\Wow6432Node\Microsoft\VisualStudio\11.0";
            }
            using (var key = Registry.LocalMachine.OpenSubKey(vsKey, false))
            {
                if (key == null)
                {
                    throw new Exception("Error: Visual Studio 2012 needs to be installed");
                }
                string path = (string)key.GetValue("InstallDir");

                if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                {
                    throw new Exception("Error: Visual Studio 2012 needs to be installed");
                }
                return path;
            }
        }

        List<string> fileNames = new List<string>();
        enum ImageFormat { Bmp, Png, Gif, Tiff, Jpg, Jpeg, Xps, Svg };
        ImageFormat format = ImageFormat.Png;
        double? width;
        double zoom = 1;
        bool transparent = true;
        bool showLegend;
        string outputPath;

        static string ImageFormats = "'png', 'bmp', 'gif', 'tiff', 'jpg', 'xps', 'svg'";

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: DgmlImage /format:png /zoom:level files...");
            Console.WriteLine("Converts given DGML documents to given image format");
            Console.WriteLine("Options:");
            Console.WriteLine("    /format:name, supported formats are " + ImageFormats + " (default png)");
            Console.WriteLine("    /f:name, short hand for /format:name");
            Console.WriteLine("    /zoom:level, default zoom is 1.");
            Console.WriteLine("    /z:level, short hand for /zoom:level");
            Console.WriteLine("    /width:n, width of the image (defaults to 100% of graph size)");
            Console.WriteLine("    /legend, show the legend (default hidden)");
            Console.WriteLine("    /out:directory, the directory in which to write the image files");
        }

        private bool ParseCommandLine(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg[0] == '-' || arg[0] == '/')
                {
                    string option = arg.Substring(1);
                    string optionarg = null;
                    int j = option.IndexOf(':');
                    if (j > 0)
                    {
                        optionarg = option.Substring(j + 1);
                        option = option.Substring(0, j);
                    }
                    switch (option)
                    {
                        case "f":
                        case "format":

                            if (optionarg == null || !Enum.TryParse<ImageFormat>(optionarg, true, out format))
                            {
                                throw new Exception("'" + optionarg + "' is not a valid image format, expecting " + ImageFormats);
                            }
                            break;
                        case "w":
                        case "width":
                            double w = 0;
                            if (optionarg == null || !double.TryParse(optionarg, out w))
                            {
                                throw new Exception("'" + optionarg + "' is not a valid floating point number");
                            }
                            else
                            {
                                width = w;
                            }
                            break;
                        case "z":
                        case "zoom":
                            if (optionarg == null || !double.TryParse(optionarg, out zoom))
                            {
                                throw new Exception("'" + optionarg + "' is not a valid floating point number");
                            }
                            break;
                        case "legend": 
                            showLegend = true;
                            break;
                        case "help":
                        case "h":
                        case "?":
                            return false;
                        case "out":
                            outputPath = optionarg;
                            break;
                    }
                }
                else
                {
                    fileNames.Add(arg);
                }
            }
            return fileNames.Count > 0;
        }

        private void ProcessFiles()
        {
            GraphControl control = new GraphControl();

            EditingContext context = new EditingContext();
            context.SetValue<IIconService>(iconLibrary);
            context.SetValue<IGraphStatus>(this);                       
            control.IconService = iconLibrary;
            control.EditingContext = context;

            // bugbug: there is a bug in ListView used by the Legend such that it refuses to layout unless
            // the thing is visible on screen!!  So we have to flash up the control here.
            double screen = SystemParameters.PrimaryScreenWidth;
            Window window = new Window();
            window.Content = control;
            window.Width = screen;
            window.Height = screen;
            window.Left = SystemParameters.VirtualScreenLeft - screen;
            window.Top = SystemParameters.VirtualScreenTop - screen;
            window.Show();

            Uri baseUri = new Uri(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar);
            foreach (string pattern in this.fileNames)
            {
                Uri resolved = new Uri(baseUri, pattern);
                if (resolved.IsFile)
                {
                    int count = 0;
                    string path = resolved.LocalPath;
                    foreach (string file in Directory.GetFiles(Path.GetDirectoryName(path), Path.GetFileName(path)))
                    {
                        count++;
                        ProcessFile(control, file);
                    }
                    if (count == 0)
                    {
                        Console.WriteLine("No files matching '" + pattern + "'");
                    }
                }
                else
                {
                    ProcessFile(control, resolved.AbsoluteUri);
                }
            }

            window.Close();
        }

        bool layoutCompleted;
        bool realizationCompleted;

        private void ProcessFile(GraphControl control, string path)
        {
            Console.WriteLine("Processing: " + path);

            Uri baseUri = new Uri(path);
            iconLibrary.BaseUri = baseUri;

            Graph graph = Graph.Load(path, new GraphSchema[] {
                Microsoft.VisualStudio.GraphModel.Schemas.DgmlCommonSchema.Schema,
                Microsoft.VisualStudio.Progression.VisualGraphSchema.Schema,
                Microsoft.VisualStudio.Diagrams.View.VisualGraphSchema.Instance
            });

            graph.SetValue<Uri>(DgmlProperties.BaseUri, baseUri);

            control.Graph = graph;

            bool hasLegend = graph.Styles.Count > 0 && showLegend;
            control.Legend.SetLegendVisibility(hasLegend ? Visibility.Visible : Visibility.Collapsed);
            
            SetupLayout(control);

            if (hasLegend)
            {
                control.Legend.ComputeStyles(true);
            }

            // enable the layout engine.
            control.Activate();

            layoutCompleted = false;
            realizationCompleted = false;

            control.Dispatcher.BeginInvoke(new System.Action(() =>
            {
                control.Diagram.QueueAfterLayoutAndRealization(new System.Action(() =>
                {
                    layoutCompleted = true;

                    // ok, we now have the desired bounds of the graph so we can set up another
                    // pass to realize all the nodes.
                    Rect bounds = control.Diagram.ScrollExtent;
                    double w = bounds.Width;
                    double h = bounds.Height;

                    if (this.zoom != 1 && width == null)
                    {
                        control.Diagram.Scale = this.zoom;
                    }
                    else if (width != null)
                    {
                        control.Diagram.Scale = (width.Value / w);
                        w = width.Value;
                    }

                    control.UpdateLayout();

                    if (hasLegend)
                    {
                        control.Legend.LegendPanel.UpdateLayout();
                    }

                    control.Diagram.QueueAfterLayoutAndRealization(new System.Action(() =>
                    {
                        control.Diagram.QueueAfterLayoutAndRealization(new System.Action(() =>
                        {
                            control.Diagram.QueueAfterLayoutAndRealization(new System.Action(() =>
                            {
                                realizationCompleted = true;
                            }));
                        }));

                    }));
                }));


            }), DispatcherPriority.Background );

            DispatcherTimer timer = new DispatcherTimer(TimeSpan.FromMilliseconds(10), DispatcherPriority.Background, new EventHandler((s, e) =>
            {
                if (layoutCompleted & realizationCompleted)
                {
                    Dispatcher.ExitAllFrames();
                }
            }), control.Dispatcher);

            Dispatcher.PushFrame(new DispatcherFrame());

            // wait for another tick just to be sure.
            Dispatcher.PushFrame(new DispatcherFrame());

            ExportImage(control, path);

            control.Deactivate();
        }


        void SetupLayout(GraphControl control)
        {
            Graph graph = control.Graph;

            // must save this before changing the layout direction...
            string layout = graph.GetLayout();

            LayoutOrientation direction = LayoutOrientation.LeftToRight;
            if (graph.HasValue(GraphObjectExtensions.GraphDirection))
            {
                direction = graph.GetGraphDirection();
            }
            
            LayoutStyle style = LayoutStyle.Sugiyama;

            if (!string.IsNullOrEmpty(layout))
            {
                Enum.TryParse<LayoutStyle>(layout, out style);
            }
            switch (style)
            {
                case LayoutStyle.Sugiyama:
                    SetLayeredLayout(control, direction);
                    break;
                case LayoutStyle.ForceDirected:
                    SetForceDirectedLayout(control);
                    break;
            }
        }

        private void SetLayeredLayout(GraphControl control, LayoutOrientation orientation)
        {
            DiagramControl diagram = control.Diagram;
            var routingStyle = EdgeRoutingStyle.Spline;
            var settings = diagram.LayoutWorkerFactory.CreateLayeredLayoutSettings(routingStyle);
            settings.Orientation = orientation;
            settings.LayerSeparation = GraphRenderCapabilities.Current.LayerSeparation;
            settings.OverlapSeparation = GraphRenderCapabilities.Current.OverlapSeparation;
            settings.GroupInnerPadding = GraphRenderCapabilities.Current.OverlapSeparation / 2;

            if (GraphRenderCapabilities.Current.AutoCompactLayoutEnabled)
            {
                var fallbackLayout = diagram.LayoutWorkerFactory.CreateForceDirectedLayoutSettings();
                fallbackLayout.Orientation = settings.Orientation;
                fallbackLayout.EdgeRoutingStyle = routingStyle;
                settings.FallbackLayoutSettings = fallbackLayout;
            }
            diagram.LayoutSettings = settings;
        }

        void SetForceDirectedLayout(GraphControl control)
        {
            var diagram = control.Diagram;
            diagram.ApplyInitialForceDirectedLayout();
            diagram.LayoutSettings.OverlapSeparation = GraphRenderCapabilities.Current.OverlapSeparation;
            diagram.LayoutSettings.GroupInnerPadding = GraphRenderCapabilities.Current.OverlapSeparation / 2;
        }

        void ExportImage(GraphControl control, string path)
        {

            BitmapEncoder encoder = null;
            string extension = null;
            switch (format)
            {
                case ImageFormat.Bmp:
                    encoder = new BmpBitmapEncoder();
                    extension = ".bmp";
                    transparent = false;
                    break;
                case ImageFormat.Png:
                    encoder = new PngBitmapEncoder();
                    extension = ".png";
                    break;
                case ImageFormat.Gif:
                    encoder = new GifBitmapEncoder();
                    extension = ".gif";
                    break;
                case ImageFormat.Tiff:
                    encoder = new TiffBitmapEncoder();
                    extension = ".tiff";
                    break;
                case ImageFormat.Jpg:
                    encoder = new JpegBitmapEncoder();
                    extension = ".jpg";
                    transparent = false;
                    break;
                case ImageFormat.Jpeg:
                    encoder = new JpegBitmapEncoder();
                    extension = ".jpeg";
                    transparent = false;
                    break;                    
                case ImageFormat.Xps:
                    extension = ".xps";
                    break;
                case ImageFormat.Svg:
                    extension = ".svg";
                    break;
            }

            string dir = outputPath;
            if (dir == null)
            {
                dir = Path.GetDirectoryName(path);
            }

            string output = Path.Combine(dir, Path.GetFileNameWithoutExtension(path) + extension);
            if (encoder != null)
            {
                DataObject data = new DataObject();
                control.CopyImage(data, transparent);

                BitmapSource image = data.GetImage();
                encoder.Frames.Add(BitmapFrame.Create(image));

                using (FileStream fs = new FileStream(output, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                {
                    encoder.Save(fs);
                }
            }
            else if (format == ImageFormat.Svg)
            {
                SvgExporter svg = new SvgExporter();
                svg.Export(control, output);
            }
            else if (format == ImageFormat.Xps)
            {
                using (var doc = control.SaveXps(output, 20))
                {
                }
            }
        }

        #region IUIHost

        public string PrivateAssembliesFolder
        {
            get { return ""; }
        }

        public string ProvidersFolder
        {
            get { return ""; }
        }

        public void ShowHelp(string keyword)
        {           
        }

        public bool ShowModalDialog(Window dialog)
        {
            return dialog.ShowDialog() == true;
        }

        public Dispatcher UIThreadDispatcher
        {
            get { return dispatcher; }
        }

        public event EventHandler<Microsoft.Win32.UserPreferenceChangedEventArgs> UserPreferencesChanged;

        public IEnumerable<string> ExtensionFolders
        {
            get { return new string[0]; }
        }

        public void Invoke(Delegate d, params object[] args)
        {
            dispatcher.Invoke(d, args);
        }

        #endregion 

        IconService iconLibrary;

        void LoadIcons()
        {
            iconLibrary = new IconService();
           
            RegisterCategoryIcon(CodeNodeCategories.Assembly, "Assembly.png");
            //RegisterCategoryIcon(CodeNodeCategories.Attribute, "Attribute.png");
            RegisterCategoryIcon(CodeNodeCategories.Class, "Class.png");
            RegisterCategoryIcon(CodeNodeCategories.Delegate, "Delegate.png");
            RegisterCategoryIcon(CodeNodeCategories.Enum, "Enum.png");
            RegisterCategoryIcon(CodeNodeCategories.Event, "Event.png");
            RegisterCategoryIcon(CodeNodeCategories.Field, "Field.png");
            //RegisterCategoryIcon(CodeNodeCategories.GlobalVariable, "GlobalVariable.png");
            //RegisterCategoryIcon(CodeNodeCategories.ImportModule, "ImportModule.png");
            RegisterCategoryIcon(CodeNodeCategories.Interface, "Interface.png");
            RegisterCategoryIcon(CodeNodeCategories.Method, "Method.png");
            RegisterCategoryIcon(CodeNodeCategories.Namespace, "Namespace.png");
            RegisterCategoryIcon(CodeNodeCategories.Project, "Project.png");
            RegisterCategoryIcon(CodeNodeCategories.ProjectItem, "ProjectItem.png");
            RegisterCategoryIcon(CodeNodeCategories.Property, "Property.png");
            RegisterCategoryIcon(CodeNodeCategories.Solution, "Solution.png");
            //RegisterCategoryIcon(CodeNodeCategories.Statement, "Statement.png");
            RegisterCategoryIcon(CodeNodeCategories.Struct, "Struct.png");

            RegisterCategoryIcon(NodeCategories.Error, "Error.png");
            RegisterCategoryIcon(NodeCategories.File, "File.png");
        }

        void RegisterCategoryIcon(GraphCategory category, string relativePath)
        {
            var image = new BitmapImage(new Uri("pack://application:,,,/DgmlImage;component/Icons/" + relativePath));
            image.Freeze();
            iconLibrary.AddIcon(category.Id, category.Id, image);
            GraphCommonSchema.Schema.OverrideMetadata(category, (m) =>
            {
                m[DgmlProperties.Icon] = category.Id;
            });
        }

        #region IGraphStatus 

        public IEnumerable<GraphObject> CurrentErrorObjects
        {
            get { return new GraphObject[0];  }
        }

        public bool HandleException(Exception exception)
        {
            Console.WriteLine(exception.Message);
            return true;
        }

        public void RemoveErrors(IEnumerable<GraphObject> errorNode)
        {
        }

        public void RemoveErrors(string owner)
        {
        }

        public void ReportError(GraphObject errorNode, ErrorLevel errorLevel, string message, string owner)
        {
        }

        public void ReportError(GraphObject errorNode, ErrorLevel errorLevel, int line, int column, string fileName, string message, string owner)
        {

        }

        public void ReportOutput(string message, bool outputTimestamp)
        {
            Console.WriteLine(message);
        }

        public void ShowOutput()
        {
        }
        #endregion 

    
        /// <summary>
        /// Determine if the specified process is a Wow64 process or not.
        /// </summary>
        public static bool IsWow64
        {
            get {
                Process process = Process.GetCurrentProcess();
                bool isWow64Process = false;
                try
                {
                    if (!IsWow64Process(process.Handle, out isWow64Process))
                    {
                        return false;
                    }
                }
                catch (Win32Exception)
                {
                    return false;
                }
                return isWow64Process;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWow64Process(
             [In] IntPtr hProcess,
             [Out] out bool wow64Process
             );
    }
}
