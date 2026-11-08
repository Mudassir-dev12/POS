using System;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Apis.Util.Store;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Services;

/// <summary>
/// Bridge between Google APIs IDataStore and ShopPOS IDriveTokenStore.
/// Ensures Google credentials are persisted securely through DPAPI encryption.
/// </summary>
public class DpapiDriveDataStore : IDataStore
{
    private readonly IDriveTokenStore _tokenStore;

    public DpapiDriveDataStore(IDriveTokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    public async Task StoreAsync<T>(string key, T value)
    {
        string json = JsonSerializer.Serialize(value);
        await _tokenStore.SaveTokenAsync(json);
    }

    public async Task DeleteAsync<T>(string key)
    {
        await _tokenStore.DeleteTokenAsync();
    }

    public async Task<T> GetAsync<T>(string key)
    {
        string? json = await _tokenStore.LoadTokenAsync();
        if (string.IsNullOrWhiteSpace(json))
        {
            return default!;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json)!;
        }
        catch
        {
            return default!;
        }
    }

    public async Task ClearAsync()
    {
        await _tokenStore.DeleteTokenAsync();
    }
}

