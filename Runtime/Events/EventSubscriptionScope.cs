using System;
using System.Collections.Generic;

namespace AroAro.DataCore.Events
{
    /// <summary>
    /// 带作用域的事件订阅管理器。
    /// 在 <see cref="Dispose"/> 时自动取消所有已注册的订阅，
    /// 避免调用 <see cref="DataCoreEventManager.ClearAllSubscriptions"/> 影响全局事件系统。
    /// </summary>
    /// <example>
    /// <code>
    /// using (var scope = new EventSubscriptionScope())
    /// {
    ///     scope.Subscribe&lt;DatasetCreatedEventArgs&gt;(
    ///         handler => DataCoreEventManager.SubscribeDatasetCreated(handler),
    ///         handler => DataCoreEventManager.UnsubscribeDatasetCreated(handler),
    ///         OnDatasetCreated);
    ///     // ... scope.Dispose() 自动取消订阅
    /// }
    /// </code>
    /// </example>
    public sealed class EventSubscriptionScope : IDisposable
    {
        private readonly List<Action> _unsubscribers = new();
        private bool _disposed;

        /// <summary>
        /// 注册一个事件订阅，并在 scope Dispose 时自动取消。
        /// </summary>
        /// <typeparam name="TArgs">事件参数类型</typeparam>
        /// <param name="subscribe">
        /// 订阅委托，例如 <c>handler => DataCoreEventManager.SubscribeDatasetCreated(handler)</c>
        /// </param>
        /// <param name="unsubscribe">
        /// 取消订阅委托，例如 <c>handler => DataCoreEventManager.UnsubscribeDatasetCreated(handler)</c>
        /// </param>
        /// <param name="handler">事件处理程序</param>
        public void Subscribe<TArgs>(
            Action<EventHandler<TArgs>> subscribe,
            Action<EventHandler<TArgs>> unsubscribe,
            EventHandler<TArgs> handler) where TArgs : EventArgs
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EventSubscriptionScope));

            subscribe(handler);
            _unsubscribers.Add(() => unsubscribe(handler));
        }

        /// <summary>
        /// 取消所有通过此 scope 注册的事件订阅。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // 反序取消订阅（LIFO），模拟嵌套 using 的行为
            for (int i = _unsubscribers.Count - 1; i >= 0; i--)
            {
                _unsubscribers[i]();
            }

            _unsubscribers.Clear();
        }
    }
}
