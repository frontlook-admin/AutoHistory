// Copyright (c) Arch team. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using FrontLook.IAutoHistory.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Internal;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace FrontLook.IAutoHistory
{
    /// <summary>
    /// Represents a plugin for Microsoft.EntityFrameworkCore to support automatically recording data changes history.
    /// </summary>
    public static class DbContextExtensions
    {
        // Minimum number of entries to enable parallel processing
        private const int ParallelProcessingThreshold = 50;

        // Maximum degree of parallelism for processing history entries
        private static readonly int MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2);

        // Cache to store entity primary key property names to avoid repeated reflection
        private static readonly ConcurrentDictionary<Type, string[]> _primaryKeyCache =
            new ConcurrentDictionary<Type, string[]>();

        // Cache to store entity table names to avoid repeated reflection
        private static readonly ConcurrentDictionary<Type, string> _tableNameCache =
            new ConcurrentDictionary<Type, string>();

        // Cache to store property exclusion info by entity type
        private static readonly ConcurrentDictionary<Type, HashSet<string>> _propertyExclusionCache =
            new ConcurrentDictionary<Type, HashSet<string>>();

        // Entity types to completely ignore for performance
        private static readonly HashSet<Type> _ignoredEntityTypes = new HashSet<Type>();

        /// <summary>
        /// Registers entity types to be completely ignored by the auto history tracking.
        /// </summary>
        /// <param name="entityTypes">Array of entity types to ignore.</param>
        public static void IgnoreEntityTypes(params Type[] entityTypes)
        {
            if (entityTypes == null || entityTypes.Length == 0)
                return;

            foreach (var type in entityTypes)
            {
                if (type != null)
                {
                    _ignoredEntityTypes.Add(type);
                }
            }
        }

        /// <summary>
        /// Registers specific properties of an entity type to exclude from change tracking.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <param name="propertyNames">Names of properties to exclude.</param>
        public static void ExcludeProperties(Type entityType, params string[] propertyNames)
        {
            if (entityType == null || propertyNames == null || propertyNames.Length == 0)
                return;

            var propertiesToExclude = _propertyExclusionCache.GetOrAdd(
                entityType,
                _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            );

            foreach (var prop in propertyNames)
            {
                if (!string.IsNullOrWhiteSpace(prop))
                {
                    propertiesToExclude.Add(prop);
                }
            }
        }

        /// <summary>
        /// Ensures the automatic history.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="EnableAddedEntries">If Enable Added Entities</param>
        /// <param name="UserName"></param>
        public static void EnsureAutoHistory(this DbContext context, bool EnableAddedEntries = false, string UserName = null)
        {
            EnsureAutoHistory<AutoHistory>(context, () => new AutoHistory(), EnableAddedEntries, UserName);
        }

        /// <summary>
        /// Ensures the automatic history.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="autoHistories"></param>
        /// <param name="EnableAddedEntries">If Enable Added Entities</param>
        /// <param name="UserName"></param>
        public static void EnsureAutoHistory(this DbContext context, out List<AutoHistory> autoHistories, bool EnableAddedEntries = false, string UserName = null)
        {
            EnsureAutoHistory<AutoHistory>(context, () => new AutoHistory(), out autoHistories, EnableAddedEntries, UserName);
        }

        /// <summary>
        /// Ensures the automatic history.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="entries">EntityEntry</param>
        /// <param name="UserName"></param>
        public static void EnsureAutoHistory(this DbContext context, EntityEntry[] entries, string UserName = null)
        {
            EnsureAutoHistory<AutoHistory>(context, () => new AutoHistory(), entries, UserName);
        }

        /// <summary>
        /// Ensures the automatic history.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="entries">EntityEntry</param>
        /// <param name="autoHistories"></param>
        /// <param name="UserName"></param>
        public static void EnsureAutoHistory(this DbContext context, EntityEntry[] entries, out List<AutoHistory> autoHistories, string UserName = null)
        {
            EnsureAutoHistory<AutoHistory>(context, () => new AutoHistory(), entries, out autoHistories, UserName);
        }

        /// <summary>
        /// Ensures automatic history with async support.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="EnableAddedEntries">If Enable Added Entities</param>
        /// <param name="UserName">Username for attribution</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>SaveChanges result</returns>
        public static async Task<int> EnsureAutoHistoryAsync(this DbContext context, bool EnableAddedEntries = false, string UserName = null, CancellationToken cancellationToken = default)
        {
            return await EnsureAutoHistoryAsync<AutoHistory>(context, () => new AutoHistory(), EnableAddedEntries, UserName, cancellationToken);
        }

        public static void EnsureAutoHistory<TAutoHistory>(this DbContext context, Func<TAutoHistory> createHistoryFactory, bool EnableAddedEntries = false, string UserName = null)
            where TAutoHistory : AutoHistory
        {
            // Get entries more efficiently with a single query and filter
            var entries = EnableAddedEntries
                ? context.ChangeTracker.Entries().Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted).ToList()
                : context.ChangeTracker.Entries().Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted).ToList();

            if (entries.Count == 0)
            {
                return;
            }

            // Preallocate collection with exact capacity
            var historyEntries = new List<TAutoHistory>(entries.Count);

            // Process entries in batch
            foreach (var entry in entries)
            {
                var history = AutoHistory(entry, createHistoryFactory, UserName);
                if (history != null)
                {
                    historyEntries.Add(history);
                }
            }

            // Add history records in bulk if any exist
            if (historyEntries.Count > 0)
            {
                context.AddRange(historyEntries);
            }
        }

        public static void EnsureAutoHistory<TAutoHistory>(this DbContext context, Func<TAutoHistory> createHistoryFactory, out List<TAutoHistory> autoHistories, bool EnableAddedEntries = false, string UserName = null)
            where TAutoHistory : AutoHistory
        {
            // Get entries more efficiently with a single query and filter
            var entries = EnableAddedEntries
                ? context.ChangeTracker.Entries().Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted).ToList()
                : context.ChangeTracker.Entries().Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted).ToList();

            // Preallocate collection with exact capacity
            var historyEntries = new List<TAutoHistory>(entries.Count);

            // Process entries in batch
            foreach (var entry in entries)
            {
                var history = AutoHistory(entry, createHistoryFactory, UserName);
                if (history != null)
                {
                    historyEntries.Add(history);
                }
            }

            // Add history records in bulk if any exist
            if (historyEntries.Count > 0)
            {
                context.AddRange(historyEntries);
            }

            autoHistories = historyEntries;
        }

        public static void EnsureAutoHistory<TAutoHistory>(this DbContext context, Func<TAutoHistory> createHistoryFactory, EntityEntry[] entries, string UserName = null)
            where TAutoHistory : AutoHistory
        {
            if (entries == null || entries.Length == 0)
            {
                return;
            }

            // Preallocate collection with exact capacity
            var historyEntries = new List<TAutoHistory>(entries.Length);

            // Process entries in batch
            foreach (var entry in entries)
            {
                var history = AutoHistory(entry, createHistoryFactory, UserName);
                if (history != null)
                {
                    historyEntries.Add(history);
                }
            }

            // Add history records in bulk if any exist
            if (historyEntries.Count > 0)
            {
                context.AddRange(historyEntries);
            }
        }

        public static void EnsureAutoHistory<TAutoHistory>(this DbContext context, Func<TAutoHistory> createHistoryFactory, EntityEntry[] entries, out List<TAutoHistory> autoHistories, string UserName = null)
            where TAutoHistory : AutoHistory
        {
            if (entries == null || entries.Length == 0)
            {
                autoHistories = new List<TAutoHistory>();
                return;
            }

            // Preallocate collection with exact capacity
            var historyEntries = new List<TAutoHistory>(entries.Length);

            // Process entries in batch
            foreach (var entry in entries)
            {
                var history = AutoHistory(entry, createHistoryFactory, UserName);
                if (history != null)
                {
                    historyEntries.Add(history);
                }
            }

            // Add history records in bulk if any exist
            if (historyEntries.Count > 0)
            {
                context.AddRange(historyEntries);
            }

            autoHistories = historyEntries;
        }

        /// <summary>
        /// Ensures automatic history with async support.
        /// </summary>
        public static async Task<int> EnsureAutoHistoryAsync<TAutoHistory>(
            this DbContext context,
            Func<TAutoHistory> createHistoryFactory,
            bool EnableAddedEntries = false,
            string UserName = null,
            CancellationToken cancellationToken = default)
            where TAutoHistory : AutoHistory
        {
            // Get entries more efficiently with a single query and filter
            var entries = EnableAddedEntries
                ? context.ChangeTracker.Entries().Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted).ToList()
                : context.ChangeTracker.Entries().Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted).ToList();

            if (entries.Count == 0)
            {
                return 0;
            }

            // Preallocate collection with exact capacity
            var historyEntries = new List<TAutoHistory>(entries.Count);

            // Process entries in batch
            foreach (var entry in entries)
            {
                var history = AutoHistory(entry, createHistoryFactory, UserName);
                if (history != null)
                {
                    historyEntries.Add(history);
                }
            }

            // Add history records in bulk if any exist
            if (historyEntries.Count > 0)
            {
                context.AddRange(historyEntries);
            }

            return 0;
        }

        /// <summary>
        /// Ensures automatic history with async support and parallel processing for large batches.
        /// </summary>
        public static async Task<int> EnsureAutoHistoryParallelAsync<TAutoHistory>(
            this DbContext context,
            Func<TAutoHistory> createHistoryFactory,
            bool enableAddedEntries = false,
            string userName = null,
            CancellationToken cancellationToken = default)
            where TAutoHistory : AutoHistory
        {
            // Get entries more efficiently with a single query and filter
            var entries = enableAddedEntries
                ? context.ChangeTracker.Entries()
                    .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
                    .Where(e => !_ignoredEntityTypes.Contains(e.Entity.GetType()))
                    .ToList()
                : context.ChangeTracker.Entries()
                    .Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted)
                    .Where(e => !_ignoredEntityTypes.Contains(e.Entity.GetType()))
                    .ToList();

            if (entries.Count == 0)
            {
                return 0;
            }

            // Process entries in parallel or sequentially based on batch size
            var historyEntries = new ConcurrentBag<TAutoHistory>();

            if (entries.Count >= ParallelProcessingThreshold)
            {
                // Process large batches in parallel for better performance
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = MaxDegreeOfParallelism,
                    CancellationToken = cancellationToken
                };

                await Task.Run(() =>
                {
                    Parallel.ForEach(entries, parallelOptions, entry =>
                    {
                        var history = AutoHistory(entry, createHistoryFactory, userName);
                        if (history != null)
                        {
                            historyEntries.Add(history);
                        }
                    });
                }, cancellationToken);
            }
            else
            {
                // Process small batches sequentially for less overhead
                foreach (var entry in entries)
                {
                    var history = AutoHistory(entry, createHistoryFactory, userName);
                    if (history != null)
                    {
                        historyEntries.Add(history);
                    }
                }
            }

            // Add history records in bulk if any exist
            if (historyEntries.Count > 0)
            {
                // Convert concurrent bag to list for AddRange operation
                var historyList = historyEntries.ToList();

                // Add history records in batches to avoid excessive memory usage
                const int batchSize = 100;
                for (int i = 0; i < historyList.Count; i += batchSize)
                {
                    var batch = historyList.Skip(i).Take(batchSize).ToList();
                    context.AddRange(batch);
                }
            }

            return 0;
        }

        public static TAutoHistory AutoHistory<TAutoHistory>(this EntityEntry entry, Func<TAutoHistory> createHistoryFactory, string UserName = null)
            where TAutoHistory : AutoHistory
        {
            if (entry == null)
                return null;

            // Skip ignored entity types
            if (_ignoredEntityTypes.Contains(entry.Entity.GetType()))
                return null;

            var history = createHistoryFactory();
            history.Id = Guid.NewGuid().ToString();

            // Use cached table name instead of reflection each time
            var entityType = entry.Entity.GetType();
            var entityName = entityType?.Name ?? "";
            history.TableName = GetTableName(entry, entityType);

            // Truncate username if needed to avoid DB errors
            if (!string.IsNullOrWhiteSpace(UserName) && UserName.Length > AutoHistoryOptions.Instance.UserNameMaxLength)
            {
                UserName = UserName.Substring(0, AutoHistoryOptions.Instance.UserNameMaxLength);
            }
            history.UserName = UserName;

            // Get properties more efficiently
            var properties = entry.Properties;
            var jsonSerializer = AutoHistoryOptions.Instance.JsonSerializer;
            var formatting = AutoHistoryOptions.Instance.JsonSerializerSettings.Formatting;

            // Get excluded properties for this entity type
            HashSet<string> excludedProperties = null;
            _propertyExclusionCache.TryGetValue(entityType, out excludedProperties);

            // Use pooled JSON objects
            JObject json = null;
            JObject bef = null;
            JObject aft = null;

            try
            {
                json = JsonObjectPool.GetObject();

                switch (entry.State)
                {
                    case EntityState.Added:
                        // Skip processing if entity is not tracked as added
                        foreach (var prop in properties)
                        {
                            // Skip key, foreign key, and explicitly excluded properties
                            if (prop.Metadata.IsKey() ||
                                prop.Metadata.IsForeignKey() ||
                                (excludedProperties != null && excludedProperties.Contains(prop.Metadata.Name)))
                            {
                                continue;
                            }

                            // Skip properties with null values
                            if (prop.CurrentValue == null)
                            {
                                json[prop.Metadata.Name] = JValue.CreateNull();
                                continue;
                            }

                            // Skip serializing very large properties if configured
                            var currentValueString = prop.CurrentValue.ToString();
                            if (AutoHistoryOptions.Instance.SkipLargeProperties &&
                                currentValueString.Length > AutoHistoryOptions.Instance.LargePropertyThreshold)
                            {
                                json[prop.Metadata.Name] = $"[Large value: {currentValueString.Length} chars]";
                                continue;
                            }

                            json[prop.Metadata.Name] = JToken.FromObject(prop.CurrentValue, jsonSerializer);
                        }

                        history.RowId = GetPrimaryKeyValueFast(entry);
                        history.Kind = EntityState.Added;
                        history.Changed = SerializeChanges(json, formatting);
                        break;

                    case EntityState.Modified:
                        var beforeAfterPair = JsonObjectPool.GetBeforeAfterPair();
                        bef = beforeAfterPair.Before;
                        aft = beforeAfterPair.After;

                        // Variable to track if there were actual changes
                        bool hasChanges = false;

                        // Cache database values retrieval for performance
                        PropertyValues databaseValues = null;

                        foreach (var prop in properties)
                        {
                            // Skip excluded properties
                            if (excludedProperties != null && excludedProperties.Contains(prop.Metadata.Name))
                                continue;

                            if (prop.IsModified)
                            {
                                // Only process properties that actually changed
                                var originalValue = prop.OriginalValue;
                                var currentValue = prop.CurrentValue;

#if DEBUG

                                // Add debug logging
                                Debug.WriteLine($"Property: {prop.Metadata.Name}");
                                Debug.WriteLine($"Original Value: {originalValue?.ToString() ?? "null"}");
                                Debug.WriteLine($"Current Value: {currentValue?.ToString() ?? "null"}");
                                Debug.WriteLine($"Are References Equal: {ReferenceEquals(originalValue, currentValue)}");
#endif

                                if (!Equals(originalValue, currentValue))
                                {
                                    hasChanges = true;

                                    // Skip serializing very large values if configured
                                    if (AutoHistoryOptions.Instance.SkipLargeProperties)
                                    {
                                        // Check original value size
                                        if (originalValue != null)
                                        {
                                            var originalValueString = originalValue.ToString();
                                            if (originalValueString.Length > AutoHistoryOptions.Instance.LargePropertyThreshold)
                                            {
                                                bef[prop.Metadata.Name] = $"[Large value: {originalValueString.Length} chars]";
                                                goto ProcessCurrentValue;
                                            }
                                        }

                                        // Handle null values
                                        if (originalValue == null)
                                        {
                                            bef[prop.Metadata.Name] = JValue.CreateNull();
                                        }
                                        else
                                        {
                                            try
                                            {
                                                bef[prop.Metadata.Name] = JToken.FromObject(originalValue, jsonSerializer);
                                            }
                                            catch
                                            {
                                                // Fallback to database values if serialization fails
                                                databaseValues = databaseValues ?? entry.GetDatabaseValues();
                                                var dbValue = databaseValues?.GetValue<object>(prop.Metadata.Name);
                                                bef[prop.Metadata.Name] = dbValue != null
                                                    ? JToken.FromObject(dbValue, jsonSerializer)
                                                    : JValue.CreateNull();
                                            }
                                        }

                                    ProcessCurrentValue:
                                        // Check current value size
                                        if (currentValue != null)
                                        {
                                            var currentValueString = currentValue.ToString();
                                            if (currentValueString.Length > AutoHistoryOptions.Instance.LargePropertyThreshold)
                                            {
                                                aft[prop.Metadata.Name] = $"[Large value: {currentValueString.Length} chars]";
                                                continue;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Handle null values for original value
                                        if (originalValue == null)
                                        {
                                            bef[prop.Metadata.Name] = JValue.CreateNull();
                                        }
                                        else
                                        {
                                            try
                                            {
                                                bef[prop.Metadata.Name] = JToken.FromObject(originalValue, jsonSerializer);
                                            }
                                            catch
                                            {
                                                // Fallback to database values if serialization fails
                                                databaseValues = databaseValues ?? entry.GetDatabaseValues();
                                                var dbValue = databaseValues?.GetValue<object>(prop.Metadata.Name);
                                                bef[prop.Metadata.Name] = dbValue != null
                                                    ? JToken.FromObject(dbValue, jsonSerializer)
                                                    : JValue.CreateNull();
                                            }
                                        }
                                    }

                                    // Handle null values for current value
                                    if (currentValue == null)
                                    {
                                        aft[prop.Metadata.Name] = JValue.CreateNull();
                                    }
                                    else
                                    {
                                        aft[prop.Metadata.Name] = JToken.FromObject(currentValue, jsonSerializer);
                                    }
                                }
                            }
                        }

                        // Skip if no actual changes were detected
                        if (!hasChanges)
                        {
                            return null;
                        }

                        json["before"] = bef;
                        json["after"] = aft;

                        history.RowId = GetPrimaryKeyValueFast(entry);
                        history.Kind = EntityState.Modified;
                        history.Changed = SerializeChanges(json, formatting);
                        break;

                    case EntityState.Deleted:
                        foreach (var prop in properties)
                        {
                            // Skip key, foreign key, and explicitly excluded properties
                            if (prop.Metadata.IsKey() ||
                                prop.Metadata.IsForeignKey() ||
                                (excludedProperties != null && excludedProperties.Contains(prop.Metadata.Name)))
                            {
                                continue;
                            }

                            // Skip properties with null values
                            if (prop.OriginalValue == null)
                            {
                                json[prop.Metadata.Name] = JValue.CreateNull();
                                continue;
                            }

                            // Skip serializing very large properties if configured
                            if (AutoHistoryOptions.Instance.SkipLargeProperties)
                            {
                                var originalValueString = prop.OriginalValue.ToString();
                                if (originalValueString.Length > AutoHistoryOptions.Instance.LargePropertyThreshold)
                                {
                                    json[prop.Metadata.Name] = $"[Large value: {originalValueString.Length} chars]";
                                    continue;
                                }
                            }

                            json[prop.Metadata.Name] = JToken.FromObject(prop.OriginalValue, jsonSerializer);
                        }

                        history.RowId = GetPrimaryKeyValueFast(entry);
                        history.Kind = EntityState.Deleted;
                        history.Changed = SerializeChanges(json, formatting);
                        break;

                    case EntityState.Detached:
                    case EntityState.Unchanged:
                    default:
                        throw new NotSupportedException("AutoHistory only support Added, Deleted and Modified entity.");
                }

                return history;
            }
            finally
            {
                // Return JObjects to the pool
                if (json != null) JsonObjectPool.ReturnObject(json);
                if (bef != null && aft != null) JsonObjectPool.ReturnBeforeAfterPair(bef, aft);
            }
        }

        /// <summary>
        /// Gets the primary key value for an entity entry using a fast cached approach.
        /// </summary>
        private static string GetPrimaryKeyValueFast(EntityEntry entry)
        {
            var entityType = entry.Entity.GetType();

            // Get primary key properties from cache or compute them
            var keyPropertyNames = _primaryKeyCache.GetOrAdd(entityType, type =>
            {
                var key = entry.Metadata.FindPrimaryKey();
                return key.Properties.Select(p => p.Name).ToArray();
            });

            // If no key properties found, fall back to traditional method
            if (keyPropertyNames.Length == 0)
            {
                return PrimaryKey(entry);
            }

            // Collect key values
            var values = new List<object>(keyPropertyNames.Length);
            foreach (var propName in keyPropertyNames)
            {
                var value = entry.Property(propName).CurrentValue;
                if (value != null)
                {
                    values.Add(value);
                }
            }

            return string.Join(",", values);
        }

        /// <summary>
        /// Gets table name with caching for performance.
        /// </summary>
        private static string GetTableName(EntityEntry entry, Type entityType)
        {
            return _tableNameCache.GetOrAdd(entityType, type =>
            {
                var tableName = entry.Metadata.GetTableName();
                // Truncate table name if needed to avoid DB errors
                if (tableName.Length > AutoHistoryOptions.Instance.TableMaxLength)
                {
                    tableName = tableName.Substring(0, AutoHistoryOptions.Instance.TableMaxLength);
                }
                return tableName;
            });
        }

        /// <summary>
        /// Optimized serialization with length limiting and optional compression.
        /// </summary>
        private static string SerializeChanges(JObject json, Newtonsoft.Json.Formatting formatting)
        {
            var changesJson = json.ToString(formatting);

            // Apply compression if configured and the payload is large enough
            if (AutoHistoryOptions.Instance.EnableCompression &&
                changesJson.Length > AutoHistoryOptions.Instance.CompressionThreshold)
            {
                return CompressJsonPayload(changesJson);
            }

            // Apply length limit if configured
            if (AutoHistoryOptions.Instance.LimitChangedLength &&
                AutoHistoryOptions.Instance.ChangedMaxLength.HasValue &&
                changesJson.Length > AutoHistoryOptions.Instance.ChangedMaxLength.Value)
            {
                changesJson = changesJson.Substring(0, AutoHistoryOptions.Instance.ChangedMaxLength.Value);
            }

            return changesJson;
        }

        /// <summary>
        /// Compresses a JSON string using GZip compression.
        /// </summary>
        private static string CompressJsonPayload(string json)
        {
            try
            {
                byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);

                using (var memoryStream = new System.IO.MemoryStream())
                {
                    using (var gzipStream = new System.IO.Compression.GZipStream(
                        memoryStream, System.IO.Compression.CompressionLevel.Optimal))
                    {
                        gzipStream.Write(jsonBytes, 0, jsonBytes.Length);
                    }

                    byte[] compressedBytes = memoryStream.ToArray();
                    string compressedBase64 = Convert.ToBase64String(compressedBytes);

                    // Add a prefix to indicate this is compressed data
                    return $"COMPRESSED:{compressedBase64}";
                }
            }
            catch
            {
                // Fall back to uncompressed if compression fails
                return json;
            }
        }

        private static string PrimaryKey(this EntityEntry entry)
        {
            var key = entry.Metadata.FindPrimaryKey();

            var values = new List<object>();
            foreach (var property in key.Properties)
            {
                var value = entry.Property(property.Name).CurrentValue;
                if (value != null)
                {
                    values.Add(value);
                }
            }

            return string.Join(",", values);
        }

        public static IEnumerable<AutoHistory> GetAutoHistory(this DbContext context, bool EnableAddedEntries = false, string UserName = null)
        {
            return GetAutoHistory<AutoHistory>(context, () => new AutoHistory(), EnableAddedEntries, UserName);
        }

        public static IEnumerable<AutoHistory> GetAutoHistory(this DbContext context, EntityEntry[] entries, string UserName = null)
        {
            return GetAutoHistory<AutoHistory>(context, () => new AutoHistory(), entries, UserName);
        }

        public static IEnumerable<TAutoHistory> GetAutoHistory<TAutoHistory>(this DbContext context, Func<TAutoHistory> createHistoryFactory, EntityEntry[] entries, string UserName = null)
            where TAutoHistory : AutoHistory
        {
            if (entries == null || entries.Length == 0)
            {
                return Enumerable.Empty<TAutoHistory>();
            }

            // Preallocate with exact capacity
            var histories = new List<TAutoHistory>(entries.Length);

            foreach (var entry in entries)
            {
                var history = AutoHistory(entry, createHistoryFactory, UserName);
                if (history != null)
                {
                    histories.Add(history);
                }
            }

            return histories;
        }

        public static List<TAutoHistory> GetAutoHistory<TAutoHistory>(this DbContext context, Func<TAutoHistory> createHistoryFactory, bool EnableAddedEntries = false, string UserName = null)
            where TAutoHistory : AutoHistory
        {
            // Get entries efficiently with a single query
            var entries = EnableAddedEntries
                ? context.ChangeTracker.Entries().Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted).ToList()
                : context.ChangeTracker.Entries().Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted).ToList();

            if (entries.Count == 0)
            {
                return new List<TAutoHistory>();
            }

            // Preallocate with exact capacity
            var histories = new List<TAutoHistory>(entries.Count);

            foreach (var entry in entries)
            {
                var history = AutoHistory(entry, createHistoryFactory, UserName);
                if (history != null)
                {
                    histories.Add(history);
                }
            }

            return histories;
        }
    }
}
