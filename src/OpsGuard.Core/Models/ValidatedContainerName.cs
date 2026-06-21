namespace OpsGuard.Core.Models;

/// <summary>
/// 仅允许通过 ServiceCatalog 校验后构造，防止 LLM 直接传入任意容器名。
/// </summary>
public sealed class ValidatedContainerName
{
    public string Value { get; }

    internal ValidatedContainerName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Container name cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;
}
