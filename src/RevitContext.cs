using Autodesk.Revit.UI;

namespace RcaPlugin
{
    /// <summary>
    /// Holds the current UIApplication for use by modeless windows and panels.
    /// </summary>
    public static class RevitContext
    {
        public static UIApplication CurrentUIApplication { get; set; }
    }
}
