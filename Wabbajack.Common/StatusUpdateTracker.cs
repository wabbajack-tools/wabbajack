using System;
using System.Reactive.Subjects;
using Wabbajack.Common.StatusFeed;

namespace Wabbajack.Common
{
    public class StatusUpdateTracker
    {
        private Subject<string> _stepName = new Subject<string>();
        public IObservable<string> StepName => _stepName.Debounce(TimeSpan.FromMilliseconds(100));

        private Subject<int> _step = new Subject<int>();
        public IObservable<int> Step => _step;

        private Subject<int> _maxStep = new Subject<int>();
        public IObservable<int> MaxStep => _maxStep;

        private Subject<Percent> _progress = new Subject<Percent>();
        public IObservable<Percent> Progress => _progress;

        private int _internalCurrentStep;
        private int _internalMaxStep;
        private string _currentStepName = "";

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
            _currentStepName = name;
            _internalCurrentStep += 1;
            Utils.Log(name);
            _step.OnNext(_internalCurrentStep);
            _stepName.OnNext($"({_internalCurrentStep}/{_internalMaxStep}) {_currentStepName}");
            MakeUpdate(Percent.Zero);
        }

        /// <summary>
        /// Converts a percent that's within the scope of a single step
        /// to the overall percent when all steps are considered
        /// </summary>
        /// <param name="singleStepPercent">Percent progress of the current single step</param>
        /// <returns>Overall progress in relation to all steps</returns>
        private Percent OverAllStatus(Percent singleStepPercent)
        {
            var per_step = 1.0f / _internalMaxStep;
            var macro = _internalCurrentStep * per_step;
            return Percent.FactoryPutInRange(macro + (per_step * singleStepPercent.Value));
        }

        public void MakeUpdate(Percent progress)
        {
            // Need to convert from single step progress to overall progress for output subject
            _progress.OnNext(OverAllStatus(progress));
        }

        public void MakeUpdate(int max, int curr)
        {
            MakeUpdate(Percent.FactoryPutInRange(curr, max == 0 ? 1 : max));
            _stepName.OnNext($"({_internalCurrentStep}/{_internalMaxStep}) {_currentStepName}, {curr} of {max}");
        }
    }

}
