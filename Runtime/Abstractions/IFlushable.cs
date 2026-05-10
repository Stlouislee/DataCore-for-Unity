namespace AroAro.DataCore
{
    /// <summary>
    /// Interface for datasets that support deferred metadata flushing.
    /// Used to decouple import operations from specific storage backends.
    /// </summary>
    public interface IFlushable
    {
        /// <summary>
        /// Flush any pending metadata updates to the underlying storage.
        /// </summary>
        void FlushMetadata();
    }
}
