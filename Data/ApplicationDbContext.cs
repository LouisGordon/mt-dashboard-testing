using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MyApp.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.AddInboxStateEntity();
        builder.AddOutboxMessageEntity();
        builder.AddOutboxStateEntity();
        // Add Saga entities
        var sagaConfigurations = new List<ISagaClassMap>()
        {
            new JobSagaMap(optimistic: false),
            new JobTypeSagaMap(optimistic: false),
            new JobAttemptSagaMap(optimistic: false)
        };
        foreach (ISagaClassMap configuration in sagaConfigurations)
        {
            configuration.Configure(builder);
        }
    }
}
