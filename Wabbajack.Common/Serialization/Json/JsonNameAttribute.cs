using System;

namespace Wabbajack.Common.Serialization.Json
{
    /// <summary>
    /// Defines the polymorphic name of this type when serialized via Json. This value will
    /// be stored in the "_wjType" field. 
    /// </summary>
    public class JsonNameAttribute : Attribute
    {
        public string Name { get; }

        public JsonNameAttribute(string name)
        {
            Name = name;
        }
            
    }
}
