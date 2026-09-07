using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SampleAnalysisTracking.Models;

namespace SampleAnalysisTracking.Data.Configurations;

public sealed class LocationConfiguration
    : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("locations");

        builder.HasKey(location => location.Id);

        builder.HasIndex(location => location.Name)
            .IsUnique();

        builder.Property(location => location.Id)
            .HasColumnName("id");

        builder.Property(location => location.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(location => location.Description)
            .HasColumnName("description")
            .HasMaxLength(500);
    }
}