using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using WardMate.Services.ApplicationWorkflow.Infrastructure;
using Xunit;

namespace WardMate.Services.ApplicationWorkflow.Tests;

public sealed class ReviewMigrationTests(WorkflowFixture fixture) : IClassFixture<WorkflowFixture>
{
    [Fact]
    public async Task UpgradePreservesOldSubmittedSnapshotsAndAllocatesDraftCodes()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var source = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        var cs = new NpgsqlConnectionStringBuilder(source.Database.GetConnectionString()) { Pooling = false };
        var database = "upgrade_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(cs.ConnectionString); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            cs.Database = database;
            await using var db = new WorkflowDbContext(new DbContextOptionsBuilder<WorkflowDbContext>()
                .UseNpgsql(cs.ConnectionString).UseSnakeCaseNamingConvention().Options);
            await db.GetService<IMigrator>().MigrateAsync("20261005085908_InitialApplicationWorkflow");
            var owner = Guid.NewGuid(); var draft = Guid.NewGuid(); var submitted = Guid.NewGuid(); var procedure = Guid.NewGuid();
            var emptyJson = "{}"; var formJson = "{\"name\":\"Cũ\"}";
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO applications(id,user_id,procedure_id,procedure_title,status,form_data,created_at,updated_at)
                VALUES ({draft},{owner},{procedure},'Cũ','DRAFT',CAST({emptyJson} AS jsonb),now(),now());
                INSERT INTO applications(id,application_code,user_id,procedure_id,procedure_title,status,form_data,submitted_at,created_at,updated_at)
                VALUES ({submitted},'HS-EXISTING',{owner},{procedure},'Cũ','SUBMITTED',CAST({formJson} AS jsonb),now(),now(),now());
                """);
            await db.Database.MigrateAsync();
            var saved = await db.Applications.SingleAsync(x => x.Id == draft);
            Assert.False(string.IsNullOrWhiteSpace(saved.ApplicationCode));
            var version = await db.Versions.SingleAsync(x => x.ApplicationId == submitted);
            Assert.Equal(1, version.VersionNumber); Assert.Contains("HS-EXISTING", version.SnapshotData);
            Assert.Empty(await db.Versions.Where(x => x.ApplicationId == draft).ToArrayAsync());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        }
        finally { await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync(); }
    }
}

