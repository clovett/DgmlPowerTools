using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LovettSoftware.DgmlPowerTools
{
    /// <summary>
    /// Interaction logic for ToolbarButton.xaml
    /// </summary>
    public partial class ToolbarButton : Button
    {
        public ToolbarButton()
        {
            InitializeComponent();
        }

        public string Caption
        {
            get { return (string)GetValue(CaptionProperty); }
            set { SetValue(CaptionProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Caption.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CaptionProperty =
            DependencyProperty.Register("Caption", typeof(string), typeof(ToolbarButton), new PropertyMetadata(null));

        public ImageSource Icon
        {
            get { return (ImageSource)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Icon.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register("Icon", typeof(ImageSource), typeof(ToolbarButton), new PropertyMetadata(null));



        public string IconUri
        {
            get { return (string)GetValue(IconUriProperty); }
            set { SetValue(IconUriProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IconUri.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IconUriProperty =
            DependencyProperty.Register("IconUri", typeof(string), typeof(ToolbarButton), new PropertyMetadata(null, OnIconUriChanged));

        private static void OnIconUriChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            ((ToolbarButton)d).OnIconUriChanged();
        }

        static Color TranslucentBiasColor = Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF);

        private void OnIconUriChanged()
        {

            /* Couldn't get this to work...
             * <Image.Source>
                <MultiBinding Converter="{StaticResource ThemedImageConverter}" ConverterParameter="{StaticResource TranslucentBiasColor}">
                    <Binding Path="Icon" RelativeSource="{RelativeSource TemplatedParent}"/>
                    <Binding Path="(platformUI:ImageThemingUtilities.ImageBackgroundColor)" RelativeSource="{RelativeSource TemplatedParent}"/>
                    <Binding Path="IsEnabled" RelativeSource="{RelativeSource TemplatedParent}"/>
                </MultiBinding>
            </Image.Source>*/

            BitmapFrame src = BitmapFrame.Create(new Uri(GetResourceUri(IconUri)));

            object backgroundColor = GetValue(ImageThemingUtilities.ImageBackgroundColorProperty);

            ThemedImageConverter converter = new ThemedImageConverter();
            Image img = (Image)converter.Convert(new object[] { src, backgroundColor, IsEnabled }, typeof(Image), TranslucentBiasColor, CultureInfo.CurrentCulture);

            Icon = img.Source;

        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == ImageThemingUtilities.ImageBackgroundColorProperty ||
                e.Property == Button.IsEnabledProperty)
            {
                OnIconUriChanged();
            }
            base.OnPropertyChanged(e);
        }


        string GetResourceUri(string relativePath)
        {
            string assembly = this.GetType().Assembly.GetName().Name;
            string uri = string.Format("pack://application:,,,/{0};component/{1}", assembly, relativePath);
            return uri;
        }
        
    }
}
