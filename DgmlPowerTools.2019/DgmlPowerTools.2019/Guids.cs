// Guids.cs
// MUST match guids.h
using System;

namespace LovettSoftware.DgmlPowerTools
{
    static class GuidList
    {
        public const string PackageGuidString = "2517bb47-303a-4633-a65e-86867c9e6bcb";

        public const string DgmlPackageGuidString = "AD1A73B0-C489-4c9c-B1FE-EEA54CD19A4F";

        public const string guidDgmlPowerToolsCmdSetString = "b6fdf30f-2021-4ac7-8749-8f55376673c6";
        public const string guidToolWindowPersistanceString = "c47640de-1f4a-4c9c-a834-c9bf0707fc36";

        public static readonly Guid guidDgmlPowerToolsCmdSet = new Guid(guidDgmlPowerToolsCmdSetString);
    };
}