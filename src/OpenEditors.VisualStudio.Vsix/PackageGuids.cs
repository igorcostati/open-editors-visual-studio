using System;

namespace OpenEditors.VisualStudio.Vsix
{
    internal static class PackageGuids
    {
        public const string OpenEditorsPackageString = "eddeda39-af65-4734-ba70-6366d3d40275";
        public const string OpenEditorsCommandSetString = "a6eaf4c4-c843-4f8f-bb94-1f7c2148d2c1";
        public const string OpenEditorsToolWindowString = "b27247d0-5f40-4608-a4fb-24d53f0f9c6b";

        public static readonly Guid OpenEditorsCommandSet = new Guid(OpenEditorsCommandSetString);
    }
}
