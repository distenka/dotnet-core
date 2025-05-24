namespace Distenka.Client
{
    public enum InstanceState
    {
        NotStarted,
        Initializing,
        GettingItemsToProcess,
        Processing,
        Finalizing,
        Successful,
        Failed,
        Cancelled,
        TimedOut,
        Error
    }
}
