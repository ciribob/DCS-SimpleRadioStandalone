﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;

public class EventBus
{
    private static EventBus _instance;

    private static readonly object _lock = new();

    //Caliburn.Micro
    private readonly IEventAggregator _eventAggregator;

    private EventBus()
    {
        _eventAggregator = new EventAggregator();
    }

    public static EventBus Instance
    {
        get
        {
            if (_instance == null)
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new EventBus();
                }

            return _instance;
        }
    }

    public void Unsubcribe(object obj)
    {
        _eventAggregator.Unsubscribe(obj);
    }

    //FROM Caliburn.Micro.EventAggregatorExtensions

    /// <summary>
    ///     Subscribes an instance to all events declared through implementations of <see cref="IHandle{T}" />.
    /// </summary>
    /// <remarks>The subscription is invoked on the thread chosen by the publisher.</remarks>
    /// <param name="eventAggregator"></param>
    /// <param name="subscriber">The instance to subscribe for event publication.</param>
    public void SubscribeOnPublishedThread(object subscriber)
    {
        _eventAggregator.Subscribe(subscriber, f => f());
    }

    /// <summary>
    ///     Subscribes an instance to all events declared through implementations of <see cref="IHandle{T}" />.
    /// </summary>
    /// <remarks>The subscription is invoked on the thread chosen by the publisher.</remarks>
    /// <param name="eventAggregator"></param>
    /// <param name="subscriber">The instance to subscribe for event publication.</param>
    [Obsolete("Use SubscribeOnPublishedThread")]
    public void Subscribe(object subscriber)
    {
        _eventAggregator.SubscribeOnPublishedThread(subscriber);
    }

    /// <summary>
    ///     Subscribes an instance to all events declared through implementations of <see cref="IHandle{T}" />.
    /// </summary>
    /// <remarks>The subscription is invoked on a new background thread.</remarks>
    /// <param name="eventAggregator"></param>
    /// <param name="subscriber">The instance to subscribe for event publication.</param>
    public void SubscribeOnBackgroundThread(object subscriber)
    {
        _eventAggregator.Subscribe(subscriber,
            f => Task.Factory.StartNew(f, default, TaskCreationOptions.None, TaskScheduler.Default));
    }

    /// <summary>
    ///     Subscribes an instance to all events declared through implementations of <see cref="IHandle{T}" />.
    /// </summary>
    /// <remarks>The subscription is invoked on the UI thread.</remarks>
    /// <param name="eventAggregator"></param>
    /// <param name="subscriber">The instance to subscribe for event publication.</param>
    public void SubscribeOnUIThread(object subscriber)
    {
        _eventAggregator.Subscribe(subscriber, f =>
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();

            Execute.BeginOnUIThread(async () =>
            {
                try
                {
                    await f();

                    taskCompletionSource.SetResult(true);
                }
                catch (OperationCanceledException)
                {
                    taskCompletionSource.SetCanceled();
                }
                catch (Exception ex)
                {
                    taskCompletionSource.SetException(ex);
                }
            });

            return taskCompletionSource.Task;
        });
    }


    /// <summary>
    ///     Publishes a message on the current thread (synchrone).
    /// </summary>
    /// <param name="eventAggregator">The event aggregator.</param>
    /// <param name="message">The message instance.</param>
    /// <param name="cancellationToken">
    ///     A cancellation token that can be used by other objects or threads to receive notice of
    ///     cancellation.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task PublishOnCurrentThreadAsync(object message, CancellationToken cancellationToken)
    {
        return _eventAggregator.PublishAsync(message, f => f(), cancellationToken);
    }

    /// <summary>
    ///     Publishes a message on the current thread (synchrone).
    /// </summary>
    /// <param name="eventAggregator">The event aggregator.</param>
    /// <param name="message">The message instance.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task PublishOnCurrentThreadAsync(object message)
    {
        return _eventAggregator.PublishOnCurrentThreadAsync(message, default);
    }

    /// <summary>
    ///     Publishes a message on a background thread (async).
    /// </summary>
    /// <param name="eventAggregator">The event aggregator.</param>
    /// <param name="message">The message instance.</param>
    /// <param name="cancellationToken">
    ///     A cancellation token that can be used by other objects or threads to receive notice of
    ///     cancellation.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task PublishOnBackgroundThreadAsync(object message, CancellationToken cancellationToken)
    {
        return _eventAggregator.PublishAsync(message,
            f => Task.Factory.StartNew(f, default, TaskCreationOptions.None, TaskScheduler.Default),
            cancellationToken);
    }

    /// <summary>
    ///     Publishes a message on a background thread (async).
    /// </summary>
    /// <param name="eventAggregator">The event aggregator.</param>
    /// <param name="message">The message instance.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task PublishOnBackgroundThreadAsync(object message)
    {
        return _eventAggregator.PublishOnBackgroundThreadAsync(message, default);
    }

    /// <summary>
    ///     Publishes a message on the UI thread.
    /// </summary>
    /// <param name="eventAggregator">The event aggregator.</param>
    /// <param name="message">The message instance.</param>
    /// <param name="cancellationToken">
    ///     A cancellation token that can be used by other objects or threads to receive notice of
    ///     cancellation.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task PublishOnUIThreadAsync(object message, CancellationToken cancellationToken)
    {
        return _eventAggregator.PublishAsync(message, f =>
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();

            Execute.BeginOnUIThread(async () =>
            {
                try
                {
                    await f();

                    taskCompletionSource.SetResult(true);
                }
                catch (OperationCanceledException)
                {
                    taskCompletionSource.SetCanceled();
                }
                catch (Exception ex)
                {
                    taskCompletionSource.SetException(ex);
                }
            });

            return taskCompletionSource.Task;
        }, cancellationToken);
    }

    /// <summary>
    ///     Publishes a message on the UI thread.
    /// </summary>
    /// <param name="eventAggregator">The event aggregator.</param>
    /// <param name="message">The message instance.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task PublishOnUIThreadAsync(object message)
    {
        return _eventAggregator.PublishOnUIThreadAsync(message, default);
    }
}