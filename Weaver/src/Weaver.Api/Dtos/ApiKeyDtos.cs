using Weaver.Domain;

namespace Weaver.Api.Dtos;

public record ApiKeyDto(Guid Id, string Name, string KeyPrefix, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt, DateTimeOffset? ExpiresAt)
{
    public static ApiKeyDto FromEntity(ApiKey k) => new(k.Id, k.Name, k.KeyPrefix, k.CreatedAt, k.LastUsedAt, k.ExpiresAt);
}

/// <summary>Only ever returned once, from the Create endpoint -- Key is never retrievable again afterward.</summary>
public record CreatedApiKeyDto(Guid Id, string Name, string KeyPrefix, string Key, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);

public record CreateApiKeyRequest(string Name, DateTimeOffset? ExpiresAt);
