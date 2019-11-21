using System;
using System.Reactive.Subjects;

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

        private Subject<float> _progress = new Subject<float>();
        public IObservable<float> Progress => _progress;

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
            _step.OnNext(_internalCurrentStep);
            _stepName.OnNext(name);
            _progress.OnNext(0.0f);
        }

        public void MakeUpdate(double progress)
        {
            _progress.OnNext((float)0.0);
        }

        public void MakeUpdate(int max, int curr)
        {
            _progress.OnNext((float)curr / ((float) (max == 0 ? 1 : max)));
        }
    }

}
