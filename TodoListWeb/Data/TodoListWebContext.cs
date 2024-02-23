using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TodoListWeb.Model;

namespace TodoListWeb.Data
{
    public class TodoListWebContext : DbContext
    {
        public TodoListWebContext (DbContextOptions<TodoListWebContext> options)
            : base(options)
        {
        }

        public DbSet<TodoListWeb.Model.Todo> Todo { get; set; } = default!;
        public DbSet<TodoListWeb.Model.Users> Users { get; set; } = default!;
        public DbSet<TodoListWeb.Model.BlacklistedToken> BlacklistedTokens { get; set; }
    }
}
