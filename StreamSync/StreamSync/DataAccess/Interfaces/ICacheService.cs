namespace StreamSync.DataAccess.Interfaces
{
    /// <summary>
    /// Abstraction for distributed cache operations.
    /// Infrastructure service for caching - not domain data persistence.
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// Gets a cached value by key.
        /// </summary>
        /// <typeparam name="T">The type to deserialize to.</typeparam>
        /// <param name="key">The cache key.</param>
        /// <returns>The cached value or default if not found.</returns>
        Task<T?> GetAsync<T>(string key) where T : class;

        /// <summary>
        /// Gets a cached value or creates it using the factory if not found.
        /// </summary>
        /// <typeparam name="T">The type to cache.</typeparam>
        /// <param name="key">The cache key.</param>
        /// <param name="factory">Factory to create the value if not cached.</param>
        /// <param name="expiration">Cache expiration time.</param>
        /// <returns>The cached or newly created value.</returns>
        Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, TimeSpan expiration) where T : class;

        /// <summary>
        /// Sets a value in cache.
        /// </summary>
        /// <typeparam name="T">The type to cache.</typeparam>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The value to cache.</param>
        /// <param name="expiration">Cache expiration time.</param>
        Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class;

        /// <summary>
        /// Removes a value from cache.
        /// </summary>
        /// <param name="key">The cache key.</param>
        Task RemoveAsync(string key);

        /// <summary>
        /// Removes all cache entries matching a pattern.
        /// </summary>
        /// <param name="pattern">The pattern to match (e.g., "room:*").</param>
        Task RemoveByPatternAsync(string pattern);

        /// <summary>
        /// Checks if a key exists in cache.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <returns>True if the key exists.</returns>
        Task<bool> ExistsAsync(string key);

        /// <summary>
        /// Gets a hash field value.
        /// </summary>
        Task<T?> HashGetAsync<T>(string key, string field) where T : class;

        /// <summary>
        /// Sets a hash field value.
        /// </summary>
        Task HashSetAsync<T>(string key, string field, T value, TimeSpan? expiration = null) where T : class;

        /// <summary>
        /// Gets all hash field values.
        /// </summary>
        Task<Dictionary<string, T>> HashGetAllAsync<T>(string key) where T : class;

        /// <summary>
        /// Removes a hash field.
        /// </summary>
        Task HashRemoveAsync(string key, string field);

        /// <summary>
        /// Gets the count of fields in a hash.
        /// </summary>
        Task<long> HashLengthAsync(string key);

        /// <summary>
        /// Checks if a hash field exists.
        /// </summary>
        Task<bool> HashExistsAsync(string key, string field);

        /// <summary>
        /// Sets expiration on a key.
        /// </summary>
        Task SetExpirationAsync(string key, TimeSpan expiration);

        /// <summary>
        /// Adds a value to a set.
        /// </summary>
        Task SetAddAsync(string key, string value);

        /// <summary>
        /// Removes a value from a set.
        /// </summary>
        Task SetRemoveAsync(string key, string value);

        /// <summary>
        /// Gets all values in a set.
        /// </summary>
        Task<IEnumerable<string>> SetMembersAsync(string key);

        /// <summary>
        /// Adds a value to the end of a list with optional trimming.
        /// </summary>
        Task ListPushAsync<T>(string key, T value, int? maxLength = null, TimeSpan? expiration = null) where T : class;

        /// <summary>
        /// Gets a range of values from a list.
        /// </summary>
        Task<List<T>> ListRangeAsync<T>(string key, long start = 0, long stop = -1) where T : class;
    }
}
