using System;
using System.Collections.Generic;

namespace Autodesk.Revit.DB
{
    /// <summary>
    /// Mock implementation of Document for testing and CI builds.
    /// </summary>
    public class Document
    {
        public string Title { get; set; } = "Mock Document";
    }

    /// <summary>
    /// Mock implementation of Element for testing and CI builds.
    /// </summary>
    public class Element
    {
        public string Name { get; set; } = "Mock Element";
        public ElementId Id { get; set; } = new ElementId(1);
    }

    /// <summary>
    /// Mock implementation of ElementId for testing and CI builds.
    /// </summary>
    public class ElementId
    {
        public int IntegerValue { get; }

        public ElementId(int value)
        {
            IntegerValue = value;
        }
    }

    /// <summary>
    /// Mock implementation of ElementSet for testing and CI builds.
    /// </summary>
    public class ElementSet : List<Element>
    {
    }

    /// <summary>
    /// Mock enum for transaction mode.
    /// </summary>
    public enum TransactionMode
    {
        Manual,
        Automatic,
        ReadOnly
    }

    /// <summary>
    /// Mock enum for Result.
    /// </summary>
    public enum Result
    {
        Succeeded,
        Failed,
        Cancelled
    }
}

namespace Autodesk.Revit.Attributes
{
    /// <summary>
    /// Mock implementation of TransactionAttribute for testing and CI builds.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class TransactionAttribute : Attribute
    {
        public Autodesk.Revit.DB.TransactionMode TransactionMode { get; set; }

        public TransactionAttribute(Autodesk.Revit.DB.TransactionMode mode)
        {
            TransactionMode = mode;
        }
    }
}