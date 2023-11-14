using Microsoft.VisualStudio.GraphModel.Schemas;
using Microsoft.VisualStudio.GraphModel;
using Microsoft.VisualStudio.Progression;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace LovettSoftware.DgmlPowerTools.Utilities
{
    internal class ProjectCategories
    {
        public static GraphCategory CsProject { get; private set; }
        public static GraphCategory VbProject { get; private set; }
        public static GraphCategory CppProject { get; private set; }
        public static GraphCategory WebProject { get; private set; }
        public static GraphCategory SharingProject { get; private set; }
        public static GraphCategory TraceProject { get; private set; }
        public static GraphCategory Solution { get; private set; }

        public ProjectCategories(IIconService iconService)
        {
            CsProject = RegisterCategoryIcon(iconService, "CsProject", "CsProject.png");
            VbProject = RegisterCategoryIcon(iconService, "VbProject", "VbProject.png");
            CppProject = RegisterCategoryIcon(iconService, "CppProject", "CppProject.png");
            Solution = RegisterCategoryIcon(iconService, "Solution", "SlnIcon.png");
            SharingProject = RegisterCategoryIcon(iconService, "SharingProject", "SharingProject.png");
            WebProject = RegisterCategoryIcon(iconService, "WebProject", "WebProject.png");
            TraceProject = RegisterCategoryIcon(iconService, "TraceProject", "TraceProject.png");
        }

        const string iconFolder = "Icons/";
        static GraphCategory GetOrRegisterCategory(string id, BitmapImage icon)
        {
            GraphSchema schema = CodeSchema.Schema;
            GraphCategory category = null;
            if (!string.IsNullOrEmpty(id))
            {
                category = schema.FindCategory(id);
                if (category == null)
                {
                    category = schema.Categories.AddNewCategory(id,
                () =>
                {
                    var m = new GraphMetadata(
                        id,
                        id,
                        null,
                        GraphMetadataOptions.Default | GraphMetadataOptions.Browsable
                    );
                    m[DgmlProperties.Icon] = category.Id;
                    return m;
                });
                }
            }
            return category;
        }

        static GraphCategory RegisterCategoryIcon(IIconService service, string categoryId, string imageName)
        {
            var image = new BitmapImage(new Uri("pack://application:,,,/DgmlPowerTools.2022;component/" + iconFolder + imageName));
            image.Freeze();
            GraphCategory category = GetOrRegisterCategory(categoryId, image);
            service.AddIcon(category.Id, category.Id, image);
            return category;
        }
    }
}
