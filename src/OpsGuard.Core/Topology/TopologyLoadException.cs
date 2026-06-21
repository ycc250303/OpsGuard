namespace OpsGuard.Core.Topology;

public sealed class TopologyLoadException : Exception
{
    public TopologyLoadException(string message) : base(message)
    {
    }

    public TopologyLoadException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
