﻿using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.IAutoHistory;

namespace Microsoft.EntityFrameworkCore.AutoHistory.Test
{

    public class BloggingContext : DbContext
    {
        public DbSet<Blog> Blogs { get; set; }
        public DbSet<Post> Posts { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase("test");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.EnableAutoHistory(changedMaxLength: null);
        }
    }

    class CustomAutoHistory : AutoHistoryTestHandle
    {
        public String CustomField { get; set; }
    }

    public class GenericBloggingContext : DbContext
    {
        public DbSet<Blog> Blogs { get; set; }
        public DbSet<Post> Posts { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase("test");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.EnableAutoHistory<CustomAutoHistory>(o => { });
        }
    }

    public class Blog
    {
        public int BlogId { get; set; }
        public string Url { get; set; }

        public List<Post> Posts { get; set; }
    }

    public class Post
    {
        public int PostId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public int? NumViews { get; set; } = null;
        public int BlogId { get; set; }
        public Blog Blog { get; set; }
    }
}
