using System;
using System.Reactive.Subjects;
using Wabbajack.Common.StatusFeed;

namespace Wabbajack.Common
{
    public class StatusUpdateTracker
    {
        private Subject<string> _stepName = new Subject<string>();
        public IObservable<string> StepName => _stepName;

        private Subject<int> _step = new Subject<int>();
        public IObservable<int> Step => _step;

        private Subject<int> _maxStep = new Subject<int>();
        public IObservable<int> MaxStep => _maxStep;

        private Subject<Percent> _progress = new Subject<Percent>();
        public IObservable<Percent> Progress => _progress;

        private int _internalCurrentStep;
        private int _internalMaxStep;

        public StatusUpdateTracker(int maxStep)
        {
            _internalMaxStep = maxStep;
        }

        public void Reset()
        {
            _maxStep.OnNext(_internalMaxStep);
        }

        public void NextStep(string name)
        {
            _internalCurrentStep += 1;
            Utils.Log(name);
            _step.OnNext(_internalCurrentStep);
            _stepName.OnNext(name);
            MakeUpdate(Percent.Zero);
        }

        private Percent OverAllStatus(Percent sub_status)
        {
            var per_step = 1.0f / _internalMaxStep;
            var macro = _internalCurrentStep * per_step;
            return Percent.FactoryPutInRange(macro + (per_step * sub_status));
        }

        public void MakeUpdate(Percent progress)
        {
            _progress.OnNext(OverAllStatus(progress));
        }

        public void MakeUpdate(int max, int curr)
        {
            MakeUpdate(Percent.FactoryPutInRange(curr, max == 0 ? 1 : max));
        }
    }

}
