﻿// ********************************************************************************************************************
// <author>Stephen Cleary</author>
// <date>08-2016</date>
// <version>v1.0.0-eta-02</version>
// <web>https://github.com/StephenCleary/Mvvm.Async/blob/master/src/Nito.Mvvm.Async/NotifyTask.cs</web>
// ********************************************************************************************************************

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;

using Nito.Mvvm;

namespace Sharpnado.Infrastructure.Tasks
{
    /// <summary>
    /// Watches a task and raises property-changed notifications when the task completes.
    /// </summary>
    public interface INotifyTask
    {
        /// <summary>
        /// Gets the task being watched. This property never changes and is never <c>null</c>.
        /// </summary>
        Task Task { get; }

        /// <summary>
        /// Gets a task that completes successfully when <see cref="Task"/> completes (successfully, faulted, or canceled). This property never changes and is never <c>null</c>.
        /// </summary>
        Task TaskCompleted { get; }

        /// <summary>
        /// Gets the current task status. This property raises a notification when the task completes.
        /// </summary>
        TaskStatus Status { get; }

        /// <summary>
        /// Gets whether the task has started.
        /// </summary>
        bool IsNotStarted { get; }

        /// <summary>
        /// Gets whether the task has completed. This property raises a notification when the value changes to <c>true</c>.
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// Gets whether the task is busy (not completed). This property raises a notification when the value changes to <c>false</c>.
        /// </summary>
        bool IsNotCompleted { get; }

        /// <summary>
        /// Gets whether the task has completed successfully. This property raises a notification when the value changes to <c>true</c>.
        /// </summary>
        bool IsSuccessfullyCompleted { get; }

        /// <summary>
        /// Gets whether the task has been canceled. This property raises a notification only if the task is canceled (i.e., if the value changes to <c>true</c>).
        /// </summary>
        bool IsCanceled { get; }

        /// <summary>
        /// Gets whether the task has faulted. This property raises a notification only if the task faults (i.e., if the value changes to <c>true</c>).
        /// </summary>
        bool IsFaulted { get; }

        /// <summary>
        /// Gets the wrapped faulting exception for the task. Returns <c>null</c> if the task is not faulted. This property raises a notification only if the task faults (i.e., if the value changes to non-<c>null</c>).
        /// </summary>
        AggregateException Exception { get; }

        /// <summary>
        /// Gets the original faulting exception for the task. Returns <c>null</c> if the task is not faulted. This property raises a notification only if the task faults (i.e., if the value changes to non-<c>null</c>).
        /// </summary>
        Exception InnerException { get; }

        /// <summary>
        /// Gets the error message for the original faulting exception for the task. Returns <c>null</c> if the task is not faulted. This property raises a notification only if the task faults (i.e., if the value changes to non-<c>null</c>).
        /// </summary>
        string ErrorMessage { get; }

        /// <summary>
        /// In case of a cold task, we start it manually.
        /// </summary>
        void Start();

        /// <summary>
        /// Cancels the callbacks: the task will execute till the end, but none of the callbacks or npc will be invoked.
        /// </summary>
        void CancelCallbacks();
    }

    /// <summary>
    /// Watches a task returning a result and raises property-changed notifications when the task completes.
    /// </summary>
    public interface INotifyTask<TResult> : INotifyTask
    {
        /// <summary>
        /// Gets the result of the task. Returns the "default result" value specified in the constructor if the task has not yet completed successfully. This property raises a notification when the task completes successfully.
        /// </summary>
        TResult Result { get; }
    }

    /// <summary>
    /// Watches a task and raises property-changed notifications when the task completes.
    /// </summary>
    public abstract partial class NotifyTaskBase : INotifyTask, INotifyPropertyChanged
    {
        /// <summary>
        /// If true we monitor the task in the constructor to start it
        /// </summary>
        private readonly bool _isHot;

        /// <summary>
        /// If true wrap the task in a new Task.
        /// </summary>
        private readonly bool _inNewTask;

        /// <summary>
        /// Callback called when the task has been canceled.
        /// </summary>
        private readonly Action<INotifyTask> _whenCanceled;

        /// <summary>
        /// Callback called when the task is faulted.
        /// </summary>
        private readonly Action<INotifyTask> _whenFaulted;

        /// <summary>
        /// Callback called when the task completed (successfully or not).
        /// </summary>
        private readonly Action<INotifyTask> _whenCompleted;

        private bool _areCallbacksCancelled;

        /// <summary>
        /// Instance logger.
        /// </summary>
        protected readonly Action<string, Exception> _errorHandler;

        /// <summary>
        /// Initializes a task notifier watching the specified task.
        /// </summary>
        protected NotifyTaskBase(
            Task task,
            Action<INotifyTask> whenCanceled = null,
            Action<INotifyTask> whenFaulted = null,
            Action<INotifyTask> whenCompleted = null,
            bool inNewTask = false,
            bool isHot = false,
            Action<string, Exception> errorHandler = null)
        {
            Task = task;
            _whenCanceled = whenCanceled;
            _whenFaulted = whenFaulted;
            _whenCompleted = whenCompleted;
            _inNewTask = inNewTask;
            _isHot = isHot;
            _errorHandler = errorHandler ?? DefaultErrorHandler;
        }

        /// <summary>
        /// Event that notifies listeners of property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <inheritdoc />
        public Task Task { get; }

        /// <inheritdoc />
        public Task TaskCompleted { get; protected set; }

        /// <inheritdoc />
        public TaskStatus Status => Task.Status;

        /// <inheritdoc />
        public bool IsCompleted => Task.IsCompleted;

        /// <inheritdoc />
        public bool IsNotStarted => Task.Status == TaskStatus.Created;

        /// <inheritdoc />
        public bool IsNotCompleted => !Task.IsCompleted;

        /// <inheritdoc />
        public bool IsSuccessfullyCompleted => Task.Status == TaskStatus.RanToCompletion;

        /// <inheritdoc />
        public bool IsCanceled => Task.IsCanceled;

        /// <inheritdoc />
        public bool IsFaulted => Task.IsFaulted;

        /// <inheritdoc />
        public AggregateException Exception => Task.Exception;

        /// <inheritdoc />
        public Exception InnerException => Exception?.InnerException;

        /// <inheritdoc />
        public string ErrorMessage => InnerException?.Message;

        protected virtual bool HasCallbacks => _whenCanceled != null || _whenCompleted != null || _whenFaulted != null;

        /// <inheritdoc />
        public void Start()
        {
            if (!_isHot)
            {
                TaskCompleted = MonitorTaskAsync(Task);
            }
        }

        public void CancelCallbacks()
        {
            _areCallbacksCancelled = true;
        }

        protected static void DefaultErrorHandler(string message, Exception exception)
        {
            Trace.WriteLine($"NotifyTask|ERROR|{message}, Exception:{Environment.NewLine}{exception}");
        }

        protected async Task MonitorTaskAsync(Task task)
        {
            try
            {
                if (_inNewTask)
                {
                    await Task.Run(async () => await task);
                }
                else
                {
                    await task;
                }
            }
            catch (TaskCanceledException canceledException)
            {
                _errorHandler?.Invoke("Task has been canceled", canceledException);
            }
            catch (Exception exception)
            {
                _errorHandler?.Invoke("Error in wrapped task", exception);
            }
            finally
            {
                InvokeCallbacks(task);
            }
        }

        protected virtual void OnSuccessfullyCompleted(PropertyChangedEventHandler propertyChanged)
        {
            propertyChanged?.Invoke(this, PropertyChangedEventArgsCache.Instance.Get("Status"));
            propertyChanged?.Invoke(this, PropertyChangedEventArgsCache.Instance.Get("IsSuccessfullyCompleted"));
        }

        private void InvokeCallbacks(Task task)
        {
            var propertyChanged = PropertyChanged;
            if (_areCallbacksCancelled || (propertyChanged == null && !HasCallbacks))
            {
                return;
            }

            propertyChanged?.Invoke(this, PropertyChangedEventArgsCache.Instance.Get("IsCompleted"));
            propertyChanged?.Invoke(this, PropertyChangedEventArgsCache.Instance.Get("IsNotCompleted"));

            try
            {
                _whenCompleted?.Invoke(this);
            }
            catch (Exception exception)
            {
                _errorHandler?.Invoke("Error while calling when completed callback", exception);
            }

            if (task.IsCanceled)
            {
                propertyChanged?.Invoke(this, PropertyChangedEventArgsCache.Instance.Get("Status"));
                propertyChanged?.Invoke(this, PropertyChangedEventArgsCache.Instance.Get("IsCanceled"));

                try
                {
                    _whenCanceled?.Invoke(this);
                }
                catch (Exception exception)
                {
                    _errorHandler?.Invoke("Error while calling when canceled callback", exception);
                }
            }
            else if (task.IsFaulted)
            {
                propertyChanged?.Invoke(this, PropertyChangedEventArgsCache.Instance.Get("Exception"));
                propertyChanged?.Invoke(this, PropertyChangedEventArgsCache.Instance.Get("InnerException"));
                propertyChanged?.Invoke(this, PropertyChangedEventArgsCache.Instance.Get("ErrorMessage"));
                propertyChanged?.Invoke(this, PropertyChangedEventArgsCache.Instance.Get("Status"));
                propertyChanged?.Invoke(this, PropertyChangedEventArgsCache.Instance.Get("IsFaulted"));

                try
                {
                    _whenFaulted?.Invoke(this);
                }
                catch (Exception exception)
                {
                    _errorHandler?.Invoke("Error while calling when faulted callback", exception);
                }
            }
            else
            {
                OnSuccessfullyCompleted(propertyChanged);
            }
        }
    }
}