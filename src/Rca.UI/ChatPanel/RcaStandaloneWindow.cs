using System.Windows;

namespace Rca.UI.ChatPanel
{
    /// <summary>
    /// Standalone window for the RCA Chat Assistant.
    /// </summary>
    public class RcaStandaloneWindow : Window
    {
        public RcaStandaloneWindow()
        {
            Title = "RCA Chat Assistant (Standalone)";
            Width = 400;
            Height = 600;
            // Always use parameterless constructor, which resolves UIApplication from RevitContext
            Content = new RcaDockablePanel();
        }
    }
}
