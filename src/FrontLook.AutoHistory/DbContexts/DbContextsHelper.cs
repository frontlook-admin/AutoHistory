using FrontLook.IAutoHistory;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontLook.IAutoHistory.DbContexts
{

    public partial class DbContextHelper : IdentityDbContext
    {

        public DbContextHelper(DbContextOptions options) : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            //OnModelCreatingPartial(modelBuilder);
            base.OnModelCreating(builder);
            // enable auto history functionality.
            builder.EnableAutoHistory<AutoHistory>(o =>
            {
                o.JsonSerializerSettings = new Newtonsoft.Json.JsonSerializerSettings()
                {
                    MaxDepth = 2
                };
                o.ChangedMaxLength = int.MaxValue;
                //o.RowIdMaxLength = int.MaxValue;
                //o.TableMaxLength = int.MaxValue;
                o.LimitChangedLength = false;
            });

            //modelBuilder.EnableAutoHistory(2048);

        }

        //partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

        #region For Saving Data
        /*
         public virtual int SaveChanges(string UserName = null, bool AcceptAllChanges = false)
         {
             var addedEntities = this.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToArray();
             // remember added entries,
             // before EF Core is assigning valid Ids (it does on save changes,
             // when ids equal zero) and setting their state to
             // Unchanged (it does on every save changes)

             this.EnsureAutoHistory(UserName:UserName);
             var k = base.SaveChanges(AcceptAllChanges);


             // after "SaveChanges" added enties now have gotten valid ids (if it was necessary)
             // and the history for them can be ensured and be saved with another "SaveChanges"
             //this.EnsureAddedHistory(addedEntities);

             if (addedEntities.Any())
             {

                 this.EnsureAutoHistory(addedEntities, UserName);
                 var l = base.SaveChanges(AcceptAllChanges);
                 DetachAllEntities();
                 return l + k;
             }
             else
             {
                 DetachAllEntities();
                 return k;
             }

         }
         public virtual async Task<int> SaveChangesAsync(string UserName = null, bool AcceptAllChanges = false)
         {
             var addedEntities = this.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToArray();
             // remember added entries,
             // before EF Core is assigning valid Ids (it does on save changes,
             // when ids equal zero) and setting their state to
             // Unchanged (it does on every save changes)

             this.EnsureAutoHistory(UserName:UserName);
             var k = await base.SaveChangesAsync(AcceptAllChanges);


             // after "SaveChanges" added enties now have gotten valid ids (if it was necessary)
             // and the history for them can be ensured and be saved with another "SaveChanges"
             //this.EnsureAddedHistory(addedEntities);

             if (addedEntities.Any())
             {

                 this.EnsureAutoHistory(addedEntities, UserName);
                 var l = await base.SaveChangesAsync(AcceptAllChanges);
                 DetachAllEntities();
                 return l + k;
             }
             else
             {
                 DetachAllEntities();
                 return k;
             }

         }
        */

        public void DetachAllEntities()
        {
            var changedEntriesCopy = this.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added ||
                            e.State == EntityState.Modified ||
                            e.State == EntityState.Deleted)
                .ToList();

            foreach (var entry in changedEntriesCopy)
                entry.State = EntityState.Detached;
        }
        public void DetachAllEntities(EntityEntry[] changedEntriesCopy)
        {
            foreach (var entry in changedEntriesCopy)
                entry.State = EntityState.Detached;
        }
        public void DetachAllEntities(EntityEntry entry)
        {
            entry.State = EntityState.Detached;
        }

        public virtual int SaveData<T>(T v, bool EnableHistory = false, string UserName = null, bool AcceptAllChanges = false) where T : class
        {
            this.Set<T>().Add(v);
            var k = 0;

            if (EnableHistory) this.EnsureAutoHistory(true, UserName: UserName);
            k = base.SaveChanges(true);

            // after "SaveChanges" added enties now have gotten valid ids (if it was necessary)
            // and the history for them can be ensured and be saved with another "SaveChanges"
            //this.EnsureAddedHistory(addedEntities);

            DetachAllEntities();
            return k;
        }


        public virtual async Task<int> SaveDataAsync<T>(T v, bool EnableHistory = false, string UserName = null, bool AcceptAllChanges = false) where T : class
        {
            this.Set<T>().Add(v);
            //var addedEntities = this.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToArray();
            // remember added entries,
            // before EF Core is assigning valid Ids (it does on save changes,
            // when ids equal zero) and setting their state to
            // Unchanged (it does on every save changes)

            if (EnableHistory) this.EnsureAutoHistory(true, UserName: UserName);

            var k = await base.SaveChangesAsync(AcceptAllChanges);
            return k;

            // after "SaveChanges" added enties now have gotten valid ids (if it was necessary)
            // and the history for them can be ensured and be saved with another "SaveChanges"
            //this.EnsureAddedHistory(addedEntities);
            /*
                        if (addedEntities.Any() && EnableHistory)
                        {

                            this.EnsureAutoHistory(addedEntities, UserName);
                            var l = await base.SaveChangesAsync(AcceptAllChanges);
                            DetachAllEntities();
                            return l + k;
                        }
                        else
                        {
                            DetachAllEntities();
                            return k;
                        }*/
            //this.EnsureAutoHistory();
        }

        public virtual int SaveDataRange<T>(List<T> v, bool EnableHistory = false, string UserName = null, bool AcceptAllChanges = false) where T : class
        {
            this.Set<T>().AddRange(v);
            //var addedEntities = this.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToArray();
            // remember added entries,
            // before EF Core is assigning valid Ids (it does on save changes,
            // when ids equal zero) and setting their state to
            // Unchanged (it does on every save changes)

            if (EnableHistory) this.EnsureAutoHistory(true, UserName: UserName);

            var k = base.SaveChanges(AcceptAllChanges);
            this.DetachAllEntities();
            return k;
        }

        public virtual async Task<int> SaveDataRangeAsync<T>(T v, bool EnableHistory = false, string UserName = null, bool AcceptAllChanges = false) where T : class
        {
            await Set<T>().AddRangeAsync(v);

            // remember added entries,
            // before EF Core is assigning valid Ids (it does on save changes,
            // when ids equal zero) and setting their state to
            // Unchanged (it does on every save changes)

            if (EnableHistory) this.EnsureAutoHistory(true, UserName: UserName);

            var k = await base.SaveChangesAsync(AcceptAllChanges);

            DetachAllEntities();
            return k;

            // after "SaveChanges" added enties now have gotten valid ids (if it was necessary)
            // and the history for them can be ensured and be saved with another "SaveChanges"
            //this.EnsureAddedHistory(addedEntities);
            /*
            if (addedEntities.Any() && EnableHistory)
            {

                this.EnsureAutoHistory(addedEntities, UserName);
                var l = await base.SaveChangesAsync(AcceptAllChanges);
                DetachAllEntities();
                return l + k;
            }
            else
            {
                DetachAllEntities();
                return k;
            }*/
            //this.EnsureAutoHistory();
        }

        #endregion For Saving Data

        #region For Updating Data

        private void AttachFix<T>(T v)
        {
            Exception exp = new Exception();
            var ct = 0;
            while (ct < 2 && exp != null)
            {
                try
                {
                    this.Attach(v).State = EntityState.Modified;
                    exp = null;
                }
                catch (Exception ex)
                {
                    ct++;

                    exp = ex;
                }
            }

            if (exp != null)
            {
                throw exp;
            }
        }

        private void AddFix<T>(T v)
        {
            Exception exp = new Exception();
            var ct = 0;
            while (ct < 2 && exp != null)
            {
                try
                {
                    this.Add(v).State = EntityState.Added;
                    exp = null;
                }
                catch (Exception ex)
                {
                    ct++;

                    exp = ex;
                }
            }

            if (exp != null)
            {
                throw exp;
            }
        }


        public async Task<int> UpdateDataAsync<T>(T v, bool EnableHistory = false, string UserName = null, bool AcceptAllChanges = false) where T : class
        {
            var result = 0;
            //this.Set<T>().Update(v);
            AttachFix(v);

            if (EnableHistory) this.EnsureAutoHistory(true, UserName: UserName);

            //this.EnsureAutoHistory(UserName: UserName);
            var saved = false;
            while (!saved)
            {
                try
                {
                    result = await this.SaveChangesAsync(AcceptAllChanges);
                    saved = true;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    foreach (var entry in ex.Entries)
                    {
                        if (entry.Entity is T)
                        {
                            var proposedValues = entry.CurrentValues;
                            var databaseValues = await entry.GetDatabaseValuesAsync();

                            foreach (var property in proposedValues.Properties)
                            {
                                //var name = property.Name;
                                var type = typeof(T);
                                if (property.Name == "Discriminator" && entry.CurrentValues[property] == null)
                                {
                                    entry.CurrentValues[property] = typeof(T).Name;
                                }
                                //var proposedValue = proposedValues[property];
                                //var databaseValue = databaseValues[property];

                                // TODO: decide which value should be written to database
                                // proposedValues[property] = <value to be saved>;
                            }

                            // Refresh original values to bypass next concurrency check
                            entry.OriginalValues.SetValues(databaseValues);
                        }
                        else
                        {
                            throw new NotSupportedException(
                                "Don't know how to handle concurrency conflicts for "
                                + entry.Metadata.Name);
                        }
                    }
                }

                //await this.SaveChangesAsync();
                DetachAllEntities();
            }

            return result;
        }

        public async Task<List<int>> UpdateDataRangeAsync<T>(List<T> g, bool EnableHistory = false, string UserName = null, bool AcceptAllChanges = false)
        {
            var result = new List<int>();
            foreach (var v in g)
            {
                AttachFix(v);

                if (EnableHistory) this.EnsureAutoHistory(true, UserName: UserName);

                //this.EnsureAutoHistory(UserName: UserName);
                var saved = false;
                while (!saved)
                {
                    try
                    {
                        result.Add(await this.SaveChangesAsync());
                        saved = true;
                    }
                    catch (DbUpdateConcurrencyException ex)
                    {
                        foreach (var entry in ex.Entries)
                        {
                            if (entry.Entity is T)
                            {
                                var proposedValues = entry.CurrentValues;
                                var databaseValues = await entry.GetDatabaseValuesAsync();

                                foreach (var property in proposedValues.Properties)
                                {
                                    var proposedValue = proposedValues[property];
                                    var databaseValue = databaseValues[property];

                                    // TODO: decide which value should be written to database
                                    // proposedValues[property] = <value to be saved>;
                                }

                                // Refresh original values to bypass next concurrency check
                                entry.OriginalValues.SetValues(databaseValues);
                            }
                            else
                            {
                                throw new NotSupportedException(
                                    "Don't know how to handle concurrency conflicts for "
                                    + entry.Metadata.Name);
                            }
                        }
                    }

                }

                //await this.SaveChangesAsync();
                DetachAllEntities();
            }

            return result;
        }


        public int UpdateData<T>(T v, bool EnableHistory = false, string UserName = null, bool AcceptAllChanges = false)
        {
            var result = 0;
            AttachFix(v);

            if (EnableHistory) this.EnsureAutoHistory(true, UserName: UserName);

            var saved = false;
            while (!saved)
            {
                try
                {
                    result = this.SaveChanges(AcceptAllChanges);
                    saved = true;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    foreach (var entry in ex.Entries)
                    {
                        if (entry.Entity is T)
                        {
                            var proposedValues = entry.CurrentValues;
                            var databaseValues = entry.OriginalValues;

                            foreach (var property in proposedValues.Properties)
                            {
                                //var name = property.Name;
                                var type = typeof(T);
                                if (property.Name == "Discriminator" && entry.CurrentValues[property] == null)
                                {
                                    entry.CurrentValues[property] = typeof(T).Name;
                                }
                                //var proposedValue = proposedValues[property];
                                //var databaseValue = databaseValues[property];

                                // TODO: decide which value should be written to database
                                // proposedValues[property] = <value to be saved>;
                            }

                            // Refresh original values to bypass next concurrency check
                            entry.OriginalValues.SetValues(databaseValues);
                        }
                        else
                        {
                            throw new NotSupportedException(
                                "Don't know how to handle concurrency conflicts for "
                                + entry.Metadata.Name);
                        }
                    }
                }
                DetachAllEntities();
                //this.SaveChanges();
            }
            return result;
        }


        public int UpdateDataRange<T>(IEnumerable<T> g, bool EnableHistory = false, string UserName = null, bool AcceptAllChanges = false)
        {
            var result = new List<int>();
            foreach (var v in g)
            {
                //var _r = this.UpdateData(v, EnableHistory, UserName, AcceptAllChanges);
                //result.Add(_r);
                AttachFix(v);

                if (EnableHistory) this.EnsureAutoHistory(true, UserName: UserName);

                var saved = false;
                while (!saved)
                {
                    try
                    {
                        //this.Update(v);
                        result.Add(this.SaveChanges());
                        DetachAllEntities();
                        saved = true;
                    }
                    catch (DbUpdateConcurrencyException ex)
                    {
                        foreach (var entry in ex.Entries)
                        {
                            if (entry.Entity is T)
                            {
                                var proposedValues = entry.CurrentValues;
                                var databaseValues = entry.GetDatabaseValues();

                                foreach (var property in proposedValues.Properties)
                                {
                                    var proposedValue = proposedValues[property];
                                    var databaseValue = databaseValues[property];

                                    // TODO: decide which value should be written to database
                                    // proposedValues[property] = <value to be saved>;
                                }

                                // Refresh original values to bypass next concurrency check
                                entry.OriginalValues.SetValues(databaseValues);
                            }
                            else
                            {
                                throw new NotSupportedException(
                                    "Don't know how to handle concurrency conflicts for "
                                    + entry.Metadata.Name);
                            }
                        }
                    }
                }

                //this.SaveChanges();
                DetachAllEntities();
            }

            return result.Sum(e => e);
        }

        #endregion For Updating Data

        #region For Deleting Data

        private void DeleteFix<T>(T v)
        {
            Exception exp = new Exception();
            var ct = 0;
            while (ct < 2 && exp != null)
            {
                try
                {
                    this.Attach(v).State = EntityState.Deleted;
                    exp = null;
                }
                catch (Exception ex)
                {
                    ct++;

                    exp = ex;
                }
            }

            if (exp != null)
            {
                throw exp;
            }
        }

        public int RemoveData<T>(T g, bool EnableHistory = false, string UserName = null, bool AcceptAllChanges = false)
        {
            this.Remove(g);

            if (EnableHistory) this.EnsureAutoHistory(true, UserName: UserName);

            var j = this.SaveChanges(AcceptAllChanges);
            return j;
        }



        public int RemoveDataRange<T>(List<T> g, bool EnableHistory = false, string UserName = null, bool AcceptAllChanges = false) where T : class
        {
            //var j = g.Select(e => RemoveData(e)).Sum();
            base.RemoveRange(g);

            if (EnableHistory) this.EnsureAutoHistory(true, UserName: UserName);

            var j = this.SaveChanges(AcceptAllChanges);
            this.DetachAllEntities();
            return j;
        }


        public async Task<int> RemoveDataAsync<T>(T g, bool EnableHistory = false, string UserName = null, bool AcceptAllChanges = false)
        {
            this.Remove(g);

            if (EnableHistory) this.EnsureAutoHistory(true, UserName: UserName);

            var j = await this.SaveChangesAsync(AcceptAllChanges);
            return j;
        }



        public async Task<int> RemoveDataRangeAsync<T>(List<T> g, bool EnableHistory = false, string UserName = null, bool AcceptAllChanges = false)
        {
            this.RemoveRange(g);

            if (EnableHistory) this.EnsureAutoHistory(true, UserName: UserName);

            var j = await this.SaveChangesAsync(AcceptAllChanges);
            return j;
        }

        #endregion

        //Histories
        public virtual DbSet<AutoHistory> AutoHistories { get; set; }
    }

}
