using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FrontLook.IAutoHistory.Internal
{
    /// <summary>
    /// This class provides options for setting up auto history.
    /// </summary>
    public sealed class AutoHistoryOptions
    {
        // Static serializer cache to avoid creating new instances on each serialization
        private static JsonSerializer _cachedSerializer;
        private static readonly object _serializerLock = new object();

        /// <summary>
        /// The shared instance of the AutoHistoryOptions.
        /// </summary>
        internal static AutoHistoryOptions Instance { get; } = new AutoHistoryOptions();

        /// <summary>
        /// Prevent constructor from being called eternally.
        /// </summary>
        private AutoHistoryOptions()
        {
        }

        /// <summary>
        /// The maximum length of the 'Changed' column. <c>null</c> will use default setting 2048 unless ChangedVarcharMax is true
        /// in which case the column will be varchar(max). Default: null.
        /// </summary>
        public int? ChangedMaxLength { get; set; }

        /// <summary>
        /// Set this to true to enforce ChangedMaxLength. If this is false, ChangedMaxLength will be ignored.
        /// Default: true.
        /// </summary>
        public bool LimitChangedLength { get; set; } = true;

        /// <summary>
        /// The max length for the row id column. Default: 255.
        /// </summary>
        public int RowIdMaxLength { get; set; } = 255;

        /// <summary>
        /// The max length for the table column. Default: 128.
        /// </summary>
        public int TableMaxLength { get; set; } = 128;

        /// <summary>
        /// The max length for the username column. Default: 255.
        /// </summary>
        public int UserNameMaxLength { get; set; } = 255;

        /// <summary>
        /// Gets or sets whether to enable compression for large JSON payloads.
        /// When enabled, JSON payloads larger than CompressionThreshold will be GZip compressed.
        /// Default: false.
        /// </summary>
        public bool EnableCompression { get; set; } = false;

        /// <summary>
        /// The threshold size in characters above which JSON payloads will be compressed.
        /// Only applies when EnableCompression is true. Default: 10000.
        /// </summary>
        public int CompressionThreshold { get; set; } = 10000;

        /// <summary>
        /// Gets or sets whether to skip serializing large property values.
        /// When enabled, property values larger than LargePropertyThreshold will be replaced with a placeholder.
        /// Default: false.
        /// </summary>
        public bool SkipLargeProperties { get; set; } = false;

        /// <summary>
        /// The threshold size in characters above which a property value is considered "large".
        /// Only applies when SkipLargeProperties is true. Default: 1000.
        /// </summary>
        public int LargePropertyThreshold { get; set; } = 1000;

        /// <summary>
        /// Gets or sets whether to automatically exclude navigation properties from change tracking.
        /// This can significantly reduce the size of history records. Default: true.
        /// </summary>
        public bool ExcludeNavigationProperties { get; set; } = true;

        /// <summary>
        /// Gets or sets whether history records should be created in batches.
        /// When enabled, history records are saved in smaller transactions to reduce memory usage.
        /// Default: true for large change sets.
        /// </summary>
        public bool EnableBatchProcessing { get; set; } = true;

        /// <summary>
        /// The batch size for saving history records in batches.
        /// Only applies when EnableBatchProcessing is true. Default: 100.
        /// </summary>
        public int BatchSize { get; set; } = 100;

        /// <summary>
        /// The JsonSerializerSettings for the changed column.
        /// </summary>
        public JsonSerializerSettings JsonSerializerSettings { get; set; } = new JsonSerializerSettings()
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>
        /// Gets a cached JsonSerializer instance to avoid creating new instances for each serialization operation.
        /// </summary>
        internal JsonSerializer JsonSerializer 
        { 
            get 
            {
                if (_cachedSerializer == null)
                {
                    lock (_serializerLock)
                    {
                        if (_cachedSerializer == null)
                        {
                            _cachedSerializer = JsonSerializer.Create(JsonSerializerSettings);
                        }
                    }
                }
                return _cachedSerializer;
            }
        }
    }
}
