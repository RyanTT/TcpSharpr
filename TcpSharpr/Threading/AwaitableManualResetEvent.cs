using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TcpSharpr.Threading {
    public class AwaitableManualResetEvent {
        protected static readonly Task<bool> _completedTask = Task.FromResult(true);
        protected readonly ConcurrentQueue<TaskCompletionSource<bool>> _taskCompletionSources = new ConcurrentQueue<TaskCompletionSource<bool>>();
        protected bool _signaled = false;

        public Task<bool> WaitAsync() {
            if (_signaled) {
                return _completedTask;
            }

            var newTaskCompletionSource = new TaskCompletionSource<bool>();
            _taskCompletionSources.Enqueue(newTaskCompletionSource);

            if (_signaled) {
                newTaskCompletionSource.SetResult(true);
            }

            return newTaskCompletionSource.Task;
        }

        public void Set(bool andReset = false) {
            if (!andReset) {
                _signaled = true;
            } else {
                _signaled = false;
            }

            var taskCompletionSources = _taskCompletionSources.ToArray();
            while (_taskCompletionSources.TryDequeue(out var ignored)) ;

            foreach (var taskCompletionSource in taskCompletionSources) {
                try {
                    taskCompletionSource?.SetResult(true);
                } catch {

                }
            }
        }

        public void Cancel() {
            var taskCompletionSources = _taskCompletionSources.ToArray();
            while (_taskCompletionSources.TryDequeue(out var ignored)) ;

            foreach (var taskCompletionSource in taskCompletionSources) {
                try {
                    taskCompletionSource?.SetCanceled();
                } catch {

                }
            }
        }

        public void SetException(Exception ex) {
            var taskCompletionSources = _taskCompletionSources.ToArray();
            while (_taskCompletionSources.TryDequeue(out var ignored)) ;

            foreach (var taskCompletionSource in taskCompletionSources) {
                try {
                    taskCompletionSource?.SetException(ex);
                } catch {

                }
            }
        }
    }
}
