using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TcpSharpr.Threading {
    public class AwaitableManualResetEvent {
        protected static readonly Task<bool> _completedTask = Task.FromResult(true);
        protected readonly Queue<TaskCompletionSource<bool>> _taskCompletionSources = new Queue<TaskCompletionSource<bool>>();
        protected bool _signaled = false;

        public Task<bool> WaitAsync() {
            lock (_taskCompletionSources) {
                if (_signaled) {
                    return _completedTask;
                }

                var newTaskCompletionSource = new TaskCompletionSource<bool>();
                _taskCompletionSources.Enqueue(newTaskCompletionSource);
                return newTaskCompletionSource.Task;
            }
        }

        public void Set() {
            lock (_taskCompletionSources) {
                _signaled = true;

                while (_taskCompletionSources.Count > 0) {
                    _taskCompletionSources.Dequeue()?.SetResult(true);
                }
            }
        }

        public void Cancel() {
            lock (_taskCompletionSources) {
                while (_taskCompletionSources.Count > 0) {
                    _taskCompletionSources.Dequeue()?.SetCanceled();
                }
            }
        }

        public void SetException(Exception ex) {
            lock (_taskCompletionSources) {
                while (_taskCompletionSources.Count > 0) {
                    _taskCompletionSources.Dequeue()?.SetException(ex);
                }
            }
        }
    }
}
