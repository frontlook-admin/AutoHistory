// Copyright (c) Arch team. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Internal;

using Newtonsoft.Json.Linq;

namespace Microsoft.EntityFrameworkCore
{
    /// <summary>
    /// Represents a plugin for Microsoft.EntityFrameworkCore to support automatically recording data changes history.
    /// </summary>
    public static class DbContextExtensions
    {
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
        /// <param name="entries">EntityEntry</param>
        /// <param name="UserName"></param>
        public static void EnsureAutoHistory(this DbContext context, EntityEntry[] entries, string UserName = null)
        {
            EnsureAutoHistory<AutoHistory>(context, () => new AutoHistory(), entries, UserName);
        }

        public static void EnsureAutoHistory<TAutoHistory>(this DbContext context, Func<TAutoHistory> createHistoryFactory, bool EnableAddedEntries = false, string UserName = null)
            where TAutoHistory : AutoHistory
        {
            // Must ToArray() here for excluding the AutoHistory model.
            // Currently, only support Modified and Deleted entity.
            var entries = new List<EntityEntry>();
           if (EnableAddedEntries)
            {
                entries = context.ChangeTracker.Entries().Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted).ToList();
            }
            else
            {

                entries = context.ChangeTracker.Entries().Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted).ToList();
            }
            foreach (var entry in entries)
            {
                context.Add<TAutoHistory>(entry.AutoHistory(createHistoryFactory,UserName));
            }
        }

        public static void EnsureAutoHistory<TAutoHistory>(this DbContext context, Func<TAutoHistory> createHistoryFactory, EntityEntry[] entries, string UserName = null)
            where TAutoHistory : AutoHistory
        {
            // Must ToArray() here for excluding the AutoHistory model.
            //entries = context.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged && e.State != EntityState.Detached).ToArray();
            foreach (var entry in entries)
            {
                context.Add(entry.AutoHistory(createHistoryFactory,UserName));
            }
        }

        public static TAutoHistory AutoHistory<TAutoHistory>(this EntityEntry entry, Func<TAutoHistory> createHistoryFactory, string UserName = null)
            where TAutoHistory : AutoHistory
        {
            var history = createHistoryFactory();
            history.TableName = entry.Metadata.GetTableName();
            history.UserName = UserName;
            // Get the mapped properties for the entity type.
            // (include shadow properties, not include navigations & references)
            var properties = entry.Properties;

            var formatting = AutoHistoryOptions.Instance.JsonSerializerSettings.Formatting;
            var jsonSerializer = AutoHistoryOptions.Instance.JsonSerializer;
            var json = new JObject();
            switch (entry.State)
            {
                case EntityState.Added:
                    foreach (var prop in properties)
                    {
                        if (prop.Metadata.IsKey() || prop.Metadata.IsForeignKey())
                        {
                            continue;
                        }
                        json[prop.Metadata.Name] = prop.CurrentValue != null
                            ? JToken.FromObject(prop.CurrentValue, jsonSerializer)
                            : JValue.CreateNull();
                    }

                    // REVIEW: what's the best way to set the RowId?
                    history.RowId = entry.PrimaryKey();
                    history.Kind = EntityState.Added;
                    history.Changed = json.ToString(formatting);
                    break;
                case EntityState.Modified:
                    var bef = new JObject();
                    var aft = new JObject();

                    PropertyValues databaseValues = null;
                    foreach (var prop in properties)
                    {
                        if (prop.IsModified)
                        {
                            if (prop.OriginalValue != null)
                            {
                                if (!prop.OriginalValue.Equals(prop.CurrentValue))
                                {
                                    bef[prop.Metadata.Name] = JToken.FromObject(prop.OriginalValue, jsonSerializer);
                                }
                                else
                                {
                                    databaseValues = databaseValues ?? entry.GetDatabaseValues();
                                    var originalValue = databaseValues.GetValue<object>(prop.Metadata.Name);
                                    bef[prop.Metadata.Name] = originalValue != null
                                        ? JToken.FromObject(originalValue, jsonSerializer)
                                        : JValue.CreateNull();
                                }
                            }
                            else
                            {
                                bef[prop.Metadata.Name] = JValue.CreateNull();
                            }

                            aft[prop.Metadata.Name] = prop.CurrentValue != null
                            ? JToken.FromObject(prop.CurrentValue, jsonSerializer)
                            : JValue.CreateNull();
                        }
                    }

                    json["before"] = bef;
                    json["after"] = aft;

                    history.RowId = entry.PrimaryKey();
                    history.Kind = EntityState.Modified;
                    history.Changed = json.ToString(formatting);
                    break;
                case EntityState.Deleted:
                    foreach (var prop in properties)
                    {
                        json[prop.Metadata.Name] = prop.OriginalValue != null
                            ? JToken.FromObject(prop.OriginalValue, jsonSerializer)
                            : JValue.CreateNull();
                    }
                    history.RowId = entry.PrimaryKey();
                    history.Kind = EntityState.Deleted;
                    history.Changed = json.ToString(formatting);
                    break;
                case EntityState.Detached:
                case EntityState.Unchanged:
                default:
                    throw new NotSupportedException("AutoHistory only support Added, Deleted and Modified entity.");
            }

            return history;
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
    }
}
