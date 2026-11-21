using Deterpos.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Deterpos.Infrastructure.Data;

public class DeterposDbContextFactory : IDbContextFactory<DeterposDbContext>
{
    private readonly IDatabaseConfigurationService _configService;

    public DeterposDbContextFactory(IDatabaseConfigurationService configService)
    {
        _configService = configService;
    }

    public DeterposDbContext CreateDbContext()
    {
        var settings = _configService.LoadSettings();
        var optionsBuilder = new DbContextOptionsBuilder<DeterposDbContext>();
        optionsBuilder.UseNpgsql(settings.BuildConnectionString());
        return new DeterposDbContext(optionsBuilder.Options);
    }
}
