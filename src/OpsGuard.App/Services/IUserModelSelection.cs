namespace OpsGuard.App.Services;

public interface IUserModelSelection
{
    string ModelId { get; }

    void SetModelId(string modelId);

    Task InitializeAsync() => Task.CompletedTask;
}
