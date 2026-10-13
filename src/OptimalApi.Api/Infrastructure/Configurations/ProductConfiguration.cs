using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OptimalApi.Api.Domain;

namespace OptimalApi.Api.Infrastructure.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", table =>
            table.HasCheckConstraint("ck_products_price_non_negative", "price >= 0"));

        builder.HasKey(p => p.Id);

        // Ids are generated in the domain (UUIDv7), not by the database.
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name)
            .HasMaxLength(Product.NameMaxLength)
            .IsRequired();

        builder.Property(p => p.Price)
            .HasPrecision(18, 2);

        builder.Property(p => p.CreatedAt).IsRequired();

        // xmin is a system column Postgres bumps on every update, so no extra column is needed.
        builder.Property(p => p.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(p => p.Name);
    }
}
