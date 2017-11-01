using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.GraphModel;
using Microsoft.VisualStudio.Progression;

namespace Microsoft.Samples.FileSystemProvider
{
    static class FileSchema
    {
        static GraphSchema schema;

        public static GraphSchema Schema { get { return schema; } }

        static FileSchema()
        {
            // Set default actions.
            schema = new GraphSchema("FileSchema");
            DriveCategory = schema.Categories.AddNewCategory("DriveCategory", () => {
                GraphMetadata m = new GraphMetadata(Resources.DriveCategoryLabel, Resources.DriveCategoryDescription, Resources.FileSystemGroup, GraphMetadataOptions.Default);
                m.SetValue<string>(DgmlProperties.DefaultAction, Actions.Contains.Id);
                return m;
            });
            FolderCategory = schema.Categories.AddNewCategory("FolderCategory", () => {
                GraphMetadata m = new GraphMetadata(Resources.FolderCategoryLabel, Resources.FolderCategoryDescription, Resources.FolderGroup, GraphMetadataOptions.Default);
                m.SetValue<string>(DgmlProperties.DefaultAction, Actions.Contains.Id);
                return m;
            });
            FileCategory = schema.Categories.AddNewCategory("FileCategory", () =>
            {
                GraphMetadata m = new GraphMetadata(Resources.FileCategoryLabel, Resources.FileCategoryDescription, Resources.FileGroup, GraphMetadataOptions.Default);
                m.SetValue<string>(DgmlProperties.DefaultAction, Actions.Contains.Id);
                return m;
            });
            FileCategory.BasedOnCategory = NodeCategories.File;
            MountedFolderCategory = schema.Categories.AddNewCategory("MountedFolderCategory", () =>
            {
                return new GraphMetadata(Resources.MountedFolderCategoryLabel, Resources.MountedFolderCategoryDescription, Resources.FileGroup, GraphMetadataOptions.Default);
            });
            SymbolicLinkCategory = schema.Categories.AddNewCategory("SymbolicLinkCategory", () =>
            {
                return new GraphMetadata(Resources.SymbolicLinkCategoryLabel, Resources.SymbolicLinkCategoryDescription, Resources.FileGroup, GraphMetadataOptions.Default);
            });

            // Properties
            DateCreatedProperty = schema.Properties.AddNewProperty("DateCreated", typeof(DateTime), () =>
            {
                return new GraphMetadata(Resources.DateCreatedLabel, Resources.DateCreatedDescription, Resources.FileSystemGroup, GraphMetadataOptions.Default);
            });
            DateModifiedProperty = schema.Properties.AddNewProperty("DateModified", typeof(DateTime), () =>
            {
                return new GraphMetadata(Resources.DateModifiedLabel, Resources.DateModifiedDescription, Resources.FileSystemGroup, GraphMetadataOptions.Default);
            });
            FileSizeProperty = schema.Properties.AddNewProperty("FileSize", typeof(long), () =>
            {
                return new GraphMetadata(Resources.FileSizeLabel, Resources.FileSizeDescription, Resources.FileSystemGroup, GraphMetadataOptions.Default);
            });
            ReadOnlyProperty = schema.Properties.AddNewProperty("ReadOnly", typeof(bool), () =>
            {
                return new GraphMetadata(Resources.ReadOnlyLabel, Resources.ReadOnlyDescription, Resources.FileSystemGroup, GraphMetadataOptions.Default);
            });
        }

        // Graph categories
        public static GraphCategory DriveCategory { get; private set; }

        public static GraphCategory FolderCategory { get; private set; }

        public static GraphCategory FileCategory { get; private set; }

        public static GraphCategory MountedFolderCategory { get; private set; }

        public static GraphCategory SymbolicLinkCategory { get; private set; }

        // Graph properties
        public static GraphProperty DateCreatedProperty { get; private set; }

        public static GraphProperty DateModifiedProperty { get; private set; }

        public static GraphProperty FileSizeProperty { get; private set; }

        public static GraphProperty ReadOnlyProperty { get; private set; }

    }
}
