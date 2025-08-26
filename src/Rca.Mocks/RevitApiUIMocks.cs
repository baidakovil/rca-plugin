using System;

namespace Autodesk.Revit.UI
{
    /// <summary>
    /// Mock implementation of UIApplication for testing and CI builds.
    /// </summary>
    public class UIApplication
    {
        public static UIApplication Current { get; set; } = new UIApplication();
        public UIDocument ActiveUIDocument { get; set; } = new UIDocument();

        public DockablePane GetDockablePane(DockablePaneId id)
        {
            return new DockablePane();
        }
    }

    /// <summary>
    /// Mock implementation of DockablePane for testing and CI builds.
    /// </summary>
    public class DockablePane
    {
        public void Show()
        {
            // Mock implementation - no action needed
        }

        public void Hide()
        {
            // Mock implementation - no action needed
        }
    }

    /// <summary>
    /// Mock implementation of UIDocument for testing and CI builds.
    /// </summary>
    public class UIDocument
    {
        public Autodesk.Revit.DB.Document Document { get; set; } = new Autodesk.Revit.DB.Document();
    }

    /// <summary>
    /// Mock implementation of ExternalEvent for testing and CI builds.
    /// </summary>
    public class ExternalEvent
    {
        public static ExternalEvent Create(IExternalEventHandler handler)
        {
            return new ExternalEvent();
        }

        public void Raise()
        {
            // Mock implementation - no action needed
        }
    }

    /// <summary>
    /// Mock interface for external event handler.
    /// </summary>
    public interface IExternalEventHandler
    {
        void Execute(UIApplication app);
        string GetName();
    }

    /// <summary>
    /// Mock implementation of IDockablePaneProvider for testing and CI builds.
    /// </summary>
    public interface IDockablePaneProvider
    {
        void SetupDockablePane(DockablePaneProviderData data);
    }

    /// <summary>
    /// Mock implementation of DockablePaneProviderData for testing and CI builds.
    /// </summary>
    public class DockablePaneProviderData
    {
        public object FrameworkElement { get; set; }
        public string InitialDockingState { get; set; }
    }

    /// <summary>
    /// Mock implementation of TaskDialog for testing and CI builds.
    /// </summary>
    public static class TaskDialog
    {
        public static void Show(string title, string message) 
        { 
            // Mock implementation - no action needed
            Console.WriteLine($"{title}: {message}");
        }
    }

    /// <summary>
    /// Mock interface for external applications.
    /// </summary>
    public interface IExternalApplication
    {
        Autodesk.Revit.DB.Result OnStartup(UIControlledApplication application);
        Autodesk.Revit.DB.Result OnShutdown(UIControlledApplication application);
    }

    /// <summary>
    /// Mock interface for external commands.
    /// </summary>
    public interface IExternalCommand
    {
        Autodesk.Revit.DB.Result Execute(ExternalCommandData commandData, ref string message, Autodesk.Revit.DB.ElementSet elements);
    }

    /// <summary>
    /// Mock implementation of UIControlledApplication for testing and CI builds.
    /// </summary>
    public class UIControlledApplication
    {
        public string ApplicationName { get; set; } = "Mock Revit";

        public void RegisterDockablePane(DockablePaneId id, string name, IDockablePaneProvider provider)
        {
            // Mock implementation - no action needed
        }

        public void CreateRibbonTab(string name)
        {
            // Mock implementation - no action needed
        }

        public RibbonPanel CreateRibbonPanel(string tabName, string panelName)
        {
            return new RibbonPanel();
        }
    }

    /// <summary>
    /// Mock implementation of RibbonPanel for testing and CI builds.
    /// </summary>
    public class RibbonPanel
    {
        public void AddItem(object item)
        {
            // Mock implementation - no action needed
        }
    }

    /// <summary>
    /// Mock implementation of PushButtonData for testing and CI builds.
    /// </summary>
    public class PushButtonData
    {
        public string Name { get; set; }
        public string Text { get; set; }
        public string AssemblyName { get; set; }
        public string ClassName { get; set; }

        public PushButtonData(string name, string text, string assemblyName, string className)
        {
            Name = name;
            Text = text;
            AssemblyName = assemblyName;
            ClassName = className;
        }
    }

    /// <summary>
    /// Mock implementation of ExternalCommandData for testing and CI builds.
    /// </summary>
    public class ExternalCommandData
    {
        public UIApplication Application { get; set; } = new UIApplication();
    }

    /// <summary>
    /// Mock implementation of DockablePaneId for testing and CI builds.
    /// </summary>
    public class DockablePaneId
    {
        public Guid Id { get; }

        public DockablePaneId(Guid id)
        {
            Id = id;
        }
    }
}