// Guids.cs
// MUST match guids.h
using System;

namespace LovettSoftware.DgmlPowerTools
{
    static class GuidList
    {
#if VS2013
        public const string guidDgmlPowerToolsPkgString = "a5891a6d-0a83-4503-9456-d5e588c246f4";
#else
        public const string guidDgmlPowerToolsPkgString = "360c28fa-e40e-4a30-9edf-f29a1f349739";
#endif

        public const string guidDgmlPowerToolsCmdSetString = "b6fdf30f-2021-4ac7-8749-8f55376673c6";
        public const string guidToolWindowPersistanceString = "c47640de-1f4a-4c9c-a834-c9bf0707fc36";

        public static readonly Guid guidDgmlPowerToolsCmdSet = new Guid(guidDgmlPowerToolsCmdSetString);
    };
}