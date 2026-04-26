using System;

namespace Wabbajack.DTOs.JsonConverters;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class JsonNameAttribute : Attribute
{
    public JsonNameAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
}