using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Api.Infrastructure.Persistence;

/// <summary>
/// Factory usada em tempo de design (ex.: <c>dotnet ef migrations add</c>).
/// Não abre conexão real — só precisa do provider Npgsql configurado para gerar
/// as migrations. A connection string pode vir de variável de ambiente e, na
/// ausência, cai num default local.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=freighthub;Username=freighthub;Password=freighthub_dev_password";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
