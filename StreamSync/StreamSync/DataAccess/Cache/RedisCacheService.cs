using StackExchange.Redis;
using StreamSync.DataAccess.Interfaces;
using System.Text.Json;

namespace StreamSync.DataAccess.Cache
{
    /// <summary>
    /// Redis implementation of ICacheService.
    /// Provides a clean abstraction over Redis operations.
    /// </summary>
    public class RedisCacheService : ICacheService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _db;
        private readonly ILogger<RedisCacheService> _logger;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
        {
            _redis = redis;
            _db = redis.GetDatabase();
            _logger = logger;
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                var value = await _db.StringGetAsync(key);
                if (value.IsNullOrEmpty)
                    return null;

                return JsonSerializer.Deserialize<T>(value!, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache key {Key}", key);
                return null;
            }
        }

        public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, TimeSpan expiration) where T : class
        {
            try
            {
                // Try to get from cache first
                var cached = await GetAsync<T>(key);
                if (cached != null)
                {
                    _logger.LogDebug("Cache hit for key {Key}", key);
                    return cached;
                }

                // Cache miss - get from factory
                _logger.LogDebug("Cache miss for key {Key}", key);
                var value = await factory();
                
                if (value != null)
                {
                    await SetAsync(key, value, expiration);
                }

                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrSet for key {Key}", key);
                // On cache error, still try to get from factory
                return await factory();
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
        {
            try
            {
                var json = JsonSerializer.Serialize(value, _jsonOptions);
                await _db.StringSetAsync(key, json, expiration);
                _logger.LogDebug("Cache set for key {Key} with expiration {Expiration}", key, expiration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache key {Key}", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                await _db.KeyDeleteAsync(key);
                _logger.LogDebug("Cache removed for key {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache key {Key}", key);
            }
        }

        public async Task RemoveByPatternAsync(string pattern)
        {
            try
            {
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                var keys = server.Keys(pattern: pattern).ToArray();
                
                if (keys.Length > 0)
                {
                    await _db.KeyDeleteAsync(keys);
                    _logger.LogDebug("Removed {Count} cache keys matching pattern {Pattern}", keys.Length, pattern);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache keys by pattern {Pattern}", pattern);
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                return await _db.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existence of key {Key}", key);
                return false;
            }
        }

        #region Hash Operations

        public async Task<T?> HashGetAsync<T>(string key, string field) where T : class
        {
            try
            {
                var value = await _db.HashGetAsync(key, field);
                if (value.IsNullOrEmpty)
                    return null;

                return JsonSerializer.Deserialize<T>(value!, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting hash field {Field} from key {Key}", field, key);
                return null;
            }
        }

        public async Task HashSetAsync<T>(string key, string field, T value, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var json = JsonSerializer.Serialize(value, _jsonOptions);
                await _db.HashSetAsync(key, field, json);
                
                if (expiration.HasValue)
                {
                    await _db.KeyExpireAsync(key, expiration);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting hash field {Field} in key {Key}", field, key);
            }
        }

        public async Task<Dictionary<string, T>> HashGetAllAsync<T>(string key) where T : class
        {
            try
            {
                var entries = await _db.HashGetAllAsync(key);
                var result = new Dictionary<string, T>();

                foreach (var entry in entries)
                {
                    var value = JsonSerializer.Deserialize<T>(entry.Value!, _jsonOptions);
                    if (value != null)
                    {
                        result[entry.Name!] = value;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all hash fields from key {Key}", key);
                return new Dictionary<string, T>();
            }
        }

        public async Task HashRemoveAsync(string key, string field)
        {
            try
            {
                await _db.HashDeleteAsync(key, field);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing hash field {Field} from key {Key}", field, key);
            }
        }

        public async Task<long> HashLengthAsync(string key)
        {
            try
            {
                return await _db.HashLengthAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting hash length for key {Key}", key);
                return 0;
            }
        }

        public async Task<bool> HashExistsAsync(string key, string field)
        {
            try
            {
                return await _db.HashExistsAsync(key, field);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking hash field existence {Field} in key {Key}", field, key);
                return false;
            }
        }

        #endregion

        public async Task SetExpirationAsync(string key, TimeSpan expiration)
        {
            try
            {
                await _db.KeyExpireAsync(key, expiration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting expiration for key {Key}", key);
            }
        }

        #region Set Operations

        public async Task SetAddAsync(string key, string value)
        {
            try
            {
                await _db.SetAddAsync(key, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding to set {Key}", key);
            }
        }

        public async Task SetRemoveAsync(string key, string value)
        {
            try
            {
                await _db.SetRemoveAsync(key, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing from set {Key}", key);
            }
        }

        public async Task<IEnumerable<string>> SetMembersAsync(string key)
        {
            try
            {
                var members = await _db.SetMembersAsync(key);
                return members.Select(m => m.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting set members for key {Key}", key);
                return Enumerable.Empty<string>();
            }
        }

        #endregion

        #region List Operations

        public async Task ListPushAsync<T>(string key, T value, int? maxLength = null, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var json = JsonSerializer.Serialize(value, _jsonOptions);
                await _db.ListRightPushAsync(key, json);

                if (maxLength.HasValue)
                {
                    await _db.ListTrimAsync(key, -maxLength.Value, -1);
                }

                if (expiration.HasValue)
                {
                    await _db.KeyExpireAsync(key, expiration);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pushing to list {Key}", key);
            }
        }

        public async Task<List<T>> ListRangeAsync<T>(string key, long start = 0, long stop = -1) where T : class
        {
            try
            {
                var values = await _db.ListRangeAsync(key, start, stop);
                var result = new List<T>();

                foreach (var value in values)
                {
                    var item = JsonSerializer.Deserialize<T>(value!, _jsonOptions);
                    if (item != null)
                    {
                        result.Add(item);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting list range from key {Key}", key);
                return new List<T>();
            }
        }

        #endregion
    }
}
