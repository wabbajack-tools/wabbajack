using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common.CSP
{
    public class RxBuffer<TIn, TOut> : FixedSizeBuffer<TOut>
    {
        private Subject<TIn> _inputSubject;
        private IObservable<TOut> _outputObservable;
        private bool _completed;

        public RxBuffer(int size, Func<IObservable<TIn>, IObservable<TOut>> transform) : base(size)
        {
            _inputSubject = new Subject<TIn>();
            _outputObservable = transform(_inputSubject);
            _outputObservable.Subscribe(itm => base.Add(itm), () => {
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

        public static void Finalize(IBuffer<TOut> buf)
        {
            ((RxBuffer<TIn, TOut>)buf).Finalize();
        }
    }
}
