using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace System.Windows
{
    /// <summary>
    /// Mock implementation of Window for non-Windows builds.
    /// </summary>
    public class Window : INotifyPropertyChanged
    {
        public object Content { get; set; }
        public string Title { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsVisible { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public virtual void Show() { IsVisible = true; }
        public virtual void Hide() { IsVisible = false; }
        public virtual void Close() { IsVisible = false; }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

namespace System.Windows.Controls
{
    /// <summary>
    /// Mock implementation of UserControl for non-Windows builds.
    /// </summary>
    public class UserControl : INotifyPropertyChanged
    {
        public object Content { get; set; }
        public object DataContext { get; set; }
        
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

namespace System.Windows.Input
{
    /// <summary>
    /// Mock implementation of CommandManager for non-Windows builds.
    /// </summary>
    public static class CommandManager
    {
        public static event EventHandler RequerySuggested;
        
        public static void InvalidateRequerySuggested()
        {
            RequerySuggested?.Invoke(null, EventArgs.Empty);
        }
    }
}