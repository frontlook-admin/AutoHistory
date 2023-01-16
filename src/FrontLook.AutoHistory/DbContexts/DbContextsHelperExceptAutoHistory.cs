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
    public partial class DbContextsHelperExceptAutoHistory : IdentityDbContext
    {
        /*public DbContextHelper(DbContextOptions<MainDbContext> options) : base(options)
		{


        }*/

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S125:Sections of code should not be commented out", Justification = "<Pending>")]
        public DbContextsHelperExceptAutoHistory(DbContextOptions options) : base(options)
        {

        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S125:Sections of code should not be commented out", Justification = "<Pending>")]
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

        }


        //partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

        #region For Saving Data

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S125:Sections of code should not be commented out", Justification = "<Pending>")]
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S125:Sections of code should not be commented out", Justification = "<Pending>")]
        public virtual int SaveData<T>(T v, bool AcceptAllChanges = false)
        {
            this.Add(v);
            var k = 0;
            k = base.SaveChanges(true);
            return k;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S125:Sections of code should not be commented out", Justification = "<Pending>")]
        public virtual async Task<int> SaveDataAsync<T>(T v, bool AcceptAllChanges = false)
        {
            this.Add(v);
            var k = await base.SaveChangesAsync(AcceptAllChanges);
            return k;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S125:Sections of code should not be commented out", Justification = "<Pending>")]
        public virtual int SaveDataRange<T>(List<T> v, bool AcceptAllChanges = false) where T : class
        {
            this.AddRange(v);

            var k = base.SaveChanges(AcceptAllChanges);
            this.DetachAllEntities();
            return k;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S125:Sections of code should not be commented out", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0003:Remove qualification", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S3220:Method calls should not resolve ambiguously to overloads with \"params\"", Justification = "<Pending>")]
        public virtual async Task<int> SaveDataRangeAsync<T>(T v, bool AcceptAllChanges = false)
        {
            this.AddRange(v);

            var k = await base.SaveChangesAsync(AcceptAllChanges);
            return k;

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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S125:Sections of code should not be commented out", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S1481:Unused local variables should be removed", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Info Code Smell", "S1135:Track uses of \"TODO\" tags", Justification = "<Pending>")]
        public async Task<int> UpdateDataAsync<T>(T v, bool AcceptAllChanges = false)
        {
            var result = 0;
            AttachFix(v);

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

                            }

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
            }

            return result;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S125:Sections of code should not be commented out", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S1481:Unused local variables should be removed", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Info Code Smell", "S1135:Track uses of \"TODO\" tags", Justification = "<Pending>")]
        public async Task<List<int>> UpdateDataRangeAsync<T>(List<T> g, bool AcceptAllChanges = false)
        {
            var result = new List<int>();
            foreach (var v in g)
            {
                AttachFix(v);

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

                DetachAllEntities();
            }

            return result;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S1481:Unused local variables should be removed", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0059:Unnecessary assignment of a value", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S125:Sections of code should not be commented out", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Info Code Smell", "S1135:Track uses of \"TODO\" tags", Justification = "<Pending>")]
        public int UpdateData<T>(T v, bool AcceptAllChanges = false)
        {
            var result = 0;
            AttachFix(v);


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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S125:Sections of code should not be commented out", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S1481:Unused local variables should be removed", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Info Code Smell", "S1135:Track uses of \"TODO\" tags", Justification = "<Pending>")]
        public int UpdateDataRange<T>(IEnumerable<T> g, bool AcceptAllChanges = false)
        {
            var result = new List<int>();
            foreach (var v in g)
            {
                //var _r = this.UpdateData(v, EnableHistory, UserName, AcceptAllChanges);
                //result.Add(_r);
                AttachFix(v);

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

        public int RemoveData<T>(T g, bool AcceptAllChanges = false)
        {
            this.Remove(g);

            var j = this.SaveChanges(AcceptAllChanges);
            return j;
        }



        public int RemoveDataRange<T>(List<T> g, bool AcceptAllChanges = false) where T : class
        {
            base.RemoveRange(g);

            var j = this.SaveChanges(AcceptAllChanges);
            this.DetachAllEntities();
            return j;
        }


        public async Task<int> RemoveDataAsync<T>(T g, bool AcceptAllChanges = false)
        {
            this.Remove(g);

            var j = await this.SaveChangesAsync(AcceptAllChanges);
            return j;
        }



        public async Task<int> RemoveDataRangeAsync<T>(List<T> g, bool AcceptAllChanges = false)
        {
            this.RemoveRange(g);

            var j = await this.SaveChangesAsync(AcceptAllChanges);
            return j;
        }

        #endregion

    }

}
