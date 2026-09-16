using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;


namespace DeviceServices.Models
{
    public partial class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public virtual DbSet<tblDeviceCommand> DeviceCommands { get; set; }
        public virtual DbSet<tblDeviceMaster> DeviceMasters { get; set; }
        public DbSet<tblEmployeeMaster> EmployeeMasters { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<tblDeviceCommand>()
               .HasNoKey();
            modelBuilder.Entity<tblDeviceMaster>()
               .HasNoKey();

            modelBuilder.Entity<tblEmployeeMaster>()
                .HasNoKey();
        }
    }
}
