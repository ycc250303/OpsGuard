using System.Text.Json;
using System.Text.Json.Serialization;
using OpsGuard.Core.Configuration;

namespace OpsGuard.App.Services.Conversations;

public sealed class JsonConversationStore : IConversationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _directory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ConversationIndex _index = new();
    private bool _initialized;

    public JsonConversationStore(ConversationStoreOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Directory))
        {
            throw new InvalidOperationException("ConversationStore.Directory 未配置。");
        }

        _directory = Path.GetFullPath(options.Directory);
    }

    public bool IsEnabled => true;

    public string? ActiveSessionId => _index.ActiveSessionId;

    public IReadOnlyList<ConversationSummary> Sessions =>
        _index.Sessions
            .OrderByDescending(session => session.UpdatedAt)
            .ToList();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            Directory.CreateDirectory(_directory);
            _index = await ReadIndexAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(_index.ActiveSessionId)
                || !SessionFileExists(_index.ActiveSessionId))
            {
                if (_index.Sessions.Count > 0)
                {
                    _index.ActiveSessionId = _index.Sessions
                        .OrderByDescending(session => session.UpdatedAt)
                        .First()
                        .Id;
                }
                else
                {
                    var session = CreateSessionDocument(LlmModelCatalog.Default);
                    await WriteSessionAsync(session, cancellationToken);
                    _index.Sessions.Add(ToSummary(session));
                    _index.ActiveSessionId = session.Id;
                }

                await WriteIndexAsync(cancellationToken);
            }

            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<StoredConversation> LoadSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadSessionAsync(sessionId, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<StoredConversation> CreateSessionAsync(string modelId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var session = CreateSessionDocument(LlmModelCatalog.NormalizeOrDefault(modelId));
            await WriteSessionAsync(session, cancellationToken);
            _index.Sessions.RemoveAll(existing => existing.Id == session.Id);
            _index.Sessions.Add(ToSummary(session));
            _index.ActiveSessionId = session.Id;
            await WriteIndexAsync(cancellationToken);
            return session;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetActiveSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_index.Sessions.Any(session => session.Id == sessionId))
            {
                throw new InvalidOperationException($"会话不存在: {sessionId}");
            }

            _index.ActiveSessionId = sessionId;
            await WriteIndexAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveSessionAsync(StoredConversation session, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            session.UpdatedAt = DateTimeOffset.UtcNow;
            await WriteSessionAsync(session, cancellationToken);
            UpsertSummary(session);
            await WriteIndexAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var sessionPath = GetSessionPath(sessionId);
            if (File.Exists(sessionPath))
            {
                File.Delete(sessionPath);
            }

            _index.Sessions.RemoveAll(session => session.Id == sessionId);

            if (_index.ActiveSessionId == sessionId)
            {
                _index.ActiveSessionId = _index.Sessions
                    .OrderByDescending(session => session.UpdatedAt)
                    .FirstOrDefault()
                    ?.Id;
            }

            if (string.IsNullOrWhiteSpace(_index.ActiveSessionId))
            {
                var session = CreateSessionDocument(LlmModelCatalog.Default);
                await WriteSessionAsync(session, cancellationToken);
                _index.Sessions.Add(ToSummary(session));
                _index.ActiveSessionId = session.Id;
            }

            await WriteIndexAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (!_initialized)
        {
            await InitializeAsync(cancellationToken);
        }
    }

    private string GetIndexPath() => Path.Combine(_directory, "index.json");

    private string GetSessionPath(string sessionId) => Path.Combine(_directory, $"{sessionId}.json");

    private bool SessionFileExists(string sessionId) => File.Exists(GetSessionPath(sessionId));

    private async Task<ConversationIndex> ReadIndexAsync(CancellationToken cancellationToken)
    {
        var indexPath = GetIndexPath();
        if (!File.Exists(indexPath))
        {
            return new ConversationIndex();
        }

        await using var stream = File.OpenRead(indexPath);
        var index = await JsonSerializer.DeserializeAsync<ConversationIndex>(stream, JsonOptions, cancellationToken);
        return index ?? new ConversationIndex();
    }

    private async Task<StoredConversation> ReadSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        var sessionPath = GetSessionPath(sessionId);
        if (!File.Exists(sessionPath))
        {
            throw new InvalidOperationException($"会话文件不存在: {sessionId}");
        }

        await using var stream = File.OpenRead(sessionPath);
        var session = await JsonSerializer.DeserializeAsync<StoredConversation>(stream, JsonOptions, cancellationToken);
        if (session is null)
        {
            throw new InvalidOperationException($"会话文件损坏: {sessionId}");
        }

        return session;
    }

    private async Task WriteIndexAsync(CancellationToken cancellationToken)
    {
        _index.Version = 1;
        await WriteJsonAtomicAsync(GetIndexPath(), _index, cancellationToken);
    }

    private async Task WriteSessionAsync(StoredConversation session, CancellationToken cancellationToken) =>
        await WriteJsonAtomicAsync(GetSessionPath(session.Id), session, cancellationToken);

    private static async Task WriteJsonAtomicAsync<T>(string targetPath, T payload, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException($"无效路径: {targetPath}");
        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, payload, JsonOptions, cancellationToken);
        }

        File.Move(tempPath, targetPath, overwrite: true);
    }

    private void UpsertSummary(StoredConversation session)
    {
        var summary = ToSummary(session);
        var index = _index.Sessions.FindIndex(existing => existing.Id == session.Id);
        if (index >= 0)
        {
            _index.Sessions[index] = summary;
        }
        else
        {
            _index.Sessions.Add(summary);
        }
    }

    private static StoredConversation CreateSessionDocument(string modelId)
    {
        var now = DateTimeOffset.UtcNow;
        return new StoredConversation
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = "新会话",
            ModelId = modelId,
            CreatedAt = now,
            UpdatedAt = now,
            Messages = []
        };
    }

    private static ConversationSummary ToSummary(StoredConversation session) => new()
    {
        Id = session.Id,
        Title = session.Title,
        ModelId = session.ModelId,
        CreatedAt = session.CreatedAt,
        UpdatedAt = session.UpdatedAt
    };

    private sealed class ConversationIndex
    {
        public int Version { get; set; } = 1;

        public string? ActiveSessionId { get; set; }

        public List<ConversationSummary> Sessions { get; set; } = [];
    }
}
