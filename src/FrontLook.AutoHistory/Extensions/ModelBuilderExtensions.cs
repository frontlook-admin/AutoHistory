// Copyright (c) Arch team. All rights reserved.

using System;
using FrontLook.IAutoHistory.Internal;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FrontLook.IAutoHistory
{
    /// <summary>
    /// Represents a plugin for Microsoft.EntityFrameworkCore to support automatically recording data changes history.
    /// </summary>
    public static class ModelBuilderExtensions
    {
        private const int DefaultChangedMaxLength = 2048;

        // Cache JsonSerializer creation for multiple context registrations
        private static readonly object _serializerLock = new object();

        /// <summary>
        /// Enables the automatic recording change history.
        /// </summary>
        /// <param name="modelBuilder">The <see cref="ModelBuilder"/> to enable auto history feature.</param>
        /// <param name="changedMaxLength">The maximum length of the 'Changed' column. <c>null</c> will use default setting 2048.</param>
        /// <returns>The <see cref="ModelBuilder"/> had enabled auto history feature.</returns>
        public static ModelBuilder EnableAutoHistory(this ModelBuilder modelBuilder, int? changedMaxLength)
        {
            return ModelBuilderExtensions.EnableAutoHistory<AutoHistory>(modelBuilder, o =>
            {
                o.ChangedMaxLength = changedMaxLength;
                o.LimitChangedLength = false;
            });
        }

        /// <summary>
        /// Enables the automatic recording change history with more configuration options.
        /// </summary>
        /// <typeparam name="TAutoHistory">The auto history entity type.</typeparam>
        /// <param name="modelBuilder">The model builder.</param>
        /// <param name="configure">The options configuration action.</param>
        /// <returns>The configured model builder</returns>
        public static ModelBuilder EnableAutoHistory<TAutoHistory>(this ModelBuilder modelBuilder, Action<AutoHistoryOptions> configure)
            where TAutoHistory : AutoHistory
        {
            var options = AutoHistoryOptions.Instance;
            configure?.Invoke(options);

            // We don't need to create a new JsonSerializer instance every time
            // the property getter in AutoHistoryOptions will handle creating it lazily
            // and caching it for future use

            modelBuilder.Entity<TAutoHistory>(b =>
            {
                // Configure entity properties
                b.Property(c => c.RowId).IsRequired().HasMaxLength(options.RowIdMaxLength);
                b.Property(c => c.TableName).IsRequired().HasMaxLength(options.TableMaxLength);

                // Configure username property with max length
                b.Property(c => c.UserName).HasMaxLength(options.UserNameMaxLength);

                // Configure Changed column
                if (options.LimitChangedLength)
                {
                    var max = options.ChangedMaxLength ?? DefaultChangedMaxLength;
                    if (max <= 0)
                    {
                        max = DefaultChangedMaxLength;
                    }
                    b.Property(c => c.Changed).HasMaxLength(max);
                }

                // Add an index on Created date for better query performance
                b.HasIndex(c => c.Created);

                // Add a composite index on TableName and RowId for better query performance
                b.HasIndex(c => new { c.TableName, c.RowId });
            });

            return modelBuilder;
        }
    }
}
