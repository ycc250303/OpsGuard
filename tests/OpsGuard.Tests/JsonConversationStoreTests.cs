using OpsGuard.App.Services;
using OpsGuard.App.Services.Conversations;
using OpsGuard.Core.Configuration;

namespace OpsGuard.Tests;

public sealed class JsonConversationStoreTests : IDisposable
{
    private readonly string _directory;
    private readonly JsonConversationStore _store;

    public JsonConversationStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "opsguard-tests", Guid.NewGuid().ToString("N"));
        _store = new JsonConversationStore(new ConversationStoreOptions { Directory = _directory });
    }

    [Fact]
    public async Task Initialize_creates_default_session_when_empty()
    {
        await _store.InitializeAsync();

        Assert.True(_store.IsEnabled);
        Assert.NotNull(_store.ActiveSessionId);
        Assert.Single(_store.Sessions);

        var session = await _store.LoadSessionAsync(_store.ActiveSessionId!);
        Assert.Equal("新会话", session.Title);
        Assert.Empty(session.Messages);
    }

    [Fact]
    public async Task SaveSession_persists_messages_and_updates_title()
    {
        await _store.InitializeAsync();
        var session = await _store.LoadSessionAsync(_store.ActiveSessionId!);
        session.Title = ConversationTitleBuilder.FromUserMessage("backend 容器挂了");
        session.Messages.Add(new StoredMessage
        {
            Role = "user",
            Content = "backend 容器挂了",
            At = DateTimeOffset.UtcNow
        });
        session.Messages.Add(new StoredMessage
        {
            Role = "assistant",
            Content = "建议检查日志",
            At = DateTimeOffset.UtcNow
        });

        await _store.SaveSessionAsync(session);

        var reloaded = new JsonConversationStore(new ConversationStoreOptions { Directory = _directory });
        await reloaded.InitializeAsync();
        var loaded = await reloaded.LoadSessionAsync(session.Id);

        Assert.Equal(2, loaded.Messages.Count);
        Assert.Equal("backend 容器挂了", loaded.Title);
    }

    [Fact]
    public async Task CreateSession_switches_active_session()
    {
        await _store.InitializeAsync();
        var firstId = _store.ActiveSessionId;

        var created = await _store.CreateSessionAsync(LlmModelCatalog.Max);

        Assert.NotEqual(firstId, created.Id);
        Assert.Equal(created.Id, _store.ActiveSessionId);
        Assert.Equal(2, _store.Sessions.Count);
        Assert.Equal(LlmModelCatalog.Max, created.ModelId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}

public sealed class ConversationTitleBuilderTests
{
    [Fact]
    public void FromUserMessage_truncates_long_text()
    {
        var title = ConversationTitleBuilder.FromUserMessage(new string('检', 50));
        Assert.EndsWith("…", title);
        Assert.True(title.Length <= 41);
    }
}
