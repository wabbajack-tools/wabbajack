using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Wabbajack.Common.Serialization
{
    public class Serializer
    {
        private Dictionary<string, int> _internedStrings = new Dictionary<string, int>();
        private Dictionary<Type, HandlerRecord> _handlers = new Dictionary<Type, HandlerRecord>();
        public BinaryWriter Writer { get; }

        public Serializer(BinaryWriter bw)
        {
            Writer = bw;
        }

        public void RegisterWriteHandler<T>(string name, IHandler handler)
        {
            _handlers.Add(typeof(T), new HandlerRecord
            {
                TypeName = name,
                TypeId = Intern(name),
                Handler = handler
            });
        }

        public async Task Write<T>(BinaryWriter bw, T data)
        {
            var handler = _handlers[typeof(T)];
            handler.Handler.Write<T>(this, data);
        }


        private int Intern(string s)
        {
            if (_internedStrings.TryGetValue(s, out var idx))
                return idx;
            idx = _internedStrings.Count;
            _internedStrings[s] = idx;
            return idx;
        }
    }

    class HandlerRecord
    {
        public string TypeName;
        public int TypeId;
        public IHandler Handler;

    }
}
