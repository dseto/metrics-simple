using System.Text.Json;

namespace Metrics.Engine;

public sealed record SecretConfig(Dictionary<string, object> Connectors, Dictionary<string, object> Azure);

public interface ISecretsProvider
{
    SecretConfig LoadSecrets(string secretsPath);
    string? GetConnectorSecret(SecretConfig config, string connectorId, string secretKey);
    string? GetAzureSecret(SecretConfig config, string secretKey);
}

public sealed class SecretsProvider : ISecretsProvider
{
    public SecretConfig LoadSecrets(string secretsPath)
    {
        if (!File.Exists(secretsPath))
            throw new FileNotFoundException($"Secrets file not found: {secretsPath}");

        var json = File.ReadAllText(secretsPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var connectors = new Dictionary<string, object>();
        var azure = new Dictionary<string, object>();

        if (root.TryGetProperty("connectors", out var connectorsElement))
        {
            foreach (var prop in connectorsElement.EnumerateObject())
            {
                connectors[prop.Name] = prop.Value.GetRawText();
            }
        }

        if (root.TryGetProperty("azure", out var azureElement))
        {
            foreach (var prop in azureElement.EnumerateObject())
            {
                azure[prop.Name] = prop.Value.GetRawText();
            }
        }

        return new SecretConfig(connectors, azure);
    }

    public string? GetConnectorSecret(SecretConfig config, string connectorId, string secretKey)
    {
        if (!config.Connectors.TryGetValue(connectorId, out var connectorData))
            return null;

        var json = connectorData.ToString() ?? "{}";
        using var doc = JsonDocument.Parse(json);
        
        if (doc.RootElement.TryGetProperty(secretKey, out var value))
            return value.GetString();

        return null;
    }

    public string? GetAzureSecret(SecretConfig config, string secretKey)
    {
        if (!config.Azure.TryGetValue(secretKey, out var azureData))
            return null;

        var json = azureData.ToString() ?? "{}";
        using var doc = JsonDocument.Parse(json);
        
        if (doc.RootElement.ValueKind == JsonValueKind.String)
            return doc.RootElement.GetString();

        return doc.RootElement.GetRawText();
    }
}
