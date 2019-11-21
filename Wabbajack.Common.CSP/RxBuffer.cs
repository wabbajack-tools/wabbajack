using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Wabbajack.Common.CSP
{
    public class RxBuffer<TIn, TOut> : LinkedList<TOut>, IBuffer<TOut>
    {
        private Subject<TIn> _inputSubject;
        private IObservable<TOut> _outputObservable;
        private bool _completed;
        private int _maxSize;

        public RxBuffer(int size, Func<IObservable<TIn>, IObservable<TOut>> transform) : base()
        {
            _maxSize = size;
            _inputSubject = new Subject<TIn>();
            _outputObservable = transform(_inputSubject);
            _outputObservable.Subscribe(itm => AddFirst(itm), () => {
                _completed = true;
            });
        }

        public bool TransformAdd(TIn val)
        {
            _inputSubject.OnNext(val);
            return _completed;
        }

        public static bool TransformAdd(IBuffer<TOut> buf, TIn itm)
        {
            return ((RxBuffer<TIn, TOut>) buf).TransformAdd(itm);
        }

        public void Finalize()
        {
            _inputSubject.OnCompleted();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public static void Finalize(IBuffer<TOut> buf)
        {
            ((RxBuffer<TIn, TOut>)buf).Finalize();
        }

        public bool IsFull => Count >= _maxSize;
        public bool IsEmpty => Count == 0;
        public TOut Remove()
        {
            var ret = Last.Value;
            RemoveLast();
            return ret;
        }

        public void Add(TOut itm)
        {
            
        }
    }
}
