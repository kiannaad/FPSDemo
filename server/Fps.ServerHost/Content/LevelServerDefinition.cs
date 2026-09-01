using System.Text.Json;

namespace Fps.ServerHost.Content;

public sealed record LevelServerDefinition(string ContentVersion, IReadOnlyList<ServerSpawnPointDefinition> SpawnPoints)
{
    public static async Task<LevelServerDefinition> LoadAsync(string contentPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(contentPath))
        {
            throw new ArgumentException("Content path is required.", nameof(contentPath));
        }

        await using FileStream stream = File.OpenRead(contentPath);
        LevelServerDefinition? definition = await JsonSerializer.DeserializeAsync<LevelServerDefinition>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);
        if (definition is null || string.IsNullOrWhiteSpace(definition.ContentVersion) || definition.SpawnPoints is null || definition.SpawnPoints.Count == 0)
        {
            throw new InvalidDataException("Level server content must contain a version and at least one spawn point.");
        }

        var spawnIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (ServerSpawnPointDefinition spawnPoint in definition.SpawnPoints)
        {
            if (string.IsNullOrWhiteSpace(spawnPoint.Id) || !spawnIds.Add(spawnPoint.Id))
            {
                throw new InvalidDataException($"Level server content contains a duplicate spawn id: {spawnPoint.Id}.");
            }

            ValidateTransformComponent(spawnPoint.Id, "position", spawnPoint.Position);
            ValidateTransformComponent(spawnPoint.Id, "rotation", spawnPoint.Rotation);
        }

        return definition;
    }

    private static void ValidateTransformComponent(string spawnId, string componentName, float[] values)
    {
        if (values is null || values.Length != 3 || values.Any(value => !float.IsFinite(value)))
        {
            throw new InvalidDataException($"Spawn point {spawnId} has an invalid {componentName}.");
        }
    }
}

public sealed record ServerSpawnPointDefinition(string Id, float[] Position, float[] Rotation);
