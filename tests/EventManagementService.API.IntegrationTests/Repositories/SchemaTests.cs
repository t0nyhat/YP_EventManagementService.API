using EventManagementService.API.DataAccess;
using EventManagementService.API.IntegrationTests.Infrastructure;
using EventManagementService.API.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventManagementService.API.IntegrationTests.Repositories;

[Collection(IntegrationTestCollection.Name)]
public class SchemaTests
{
    private readonly PostgreSqlTestcontainerFixture _fixture;

    public SchemaTests(PostgreSqlTestcontainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TablesAndPrimaryKeys_WhenDatabaseIsMigrated_HaveExpectedShape()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        await using var context = _fixture.CreateDbContext();

        var hasEventsTable = await TableExistsAsync(context, "events", cancellationToken);
        var hasBookingsTable = await TableExistsAsync(context, "bookings", cancellationToken);

        hasEventsTable.Should().BeTrue();
        hasBookingsTable.Should().BeTrue();

        var eventColumns = await GetColumnNamesAsync(context, "events", cancellationToken);
        var bookingColumns = await GetColumnNamesAsync(context, "bookings", cancellationToken);

        eventColumns.Should().BeEquivalentTo(new[]
        {
            "id",
            "title",
            "description",
            "start_at",
            "end_at",
            "total_seats",
            "available_seats"
        });

        bookingColumns.Should().BeEquivalentTo(new[]
        {
            "id",
            "event_id",
            "status",
            "created_at",
            "processed_at"
        });

        var eventPrimaryKey = await GetPrimaryKeyColumnsAsync(context, "events", cancellationToken);
        var bookingPrimaryKey = await GetPrimaryKeyColumnsAsync(context, "bookings", cancellationToken);

        eventPrimaryKey.Should().BeEquivalentTo(["id"]);
        bookingPrimaryKey.Should().BeEquivalentTo(["id"]);

        var requiredColumns = await GetNotNullColumnsAsync(context, "events", cancellationToken);
        requiredColumns.Should().Contain(new[]
        {
            "id",
            "title",
            "start_at",
            "end_at",
            "total_seats",
            "available_seats"
        });
    }

    [Fact]
    public async Task BookingForeignKey_WhenDatabaseIsMigrated_UsesCascadeDelete()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        await using var context = _fixture.CreateDbContext();
        var foreignKey = await GetForeignKeyAsync(context, "bookings", "events", cancellationToken);

        foreignKey.Should().NotBeNull();
        foreignKey!.SourceColumn.Should().Be("event_id");
        foreignKey.TargetColumn.Should().Be("id");
        foreignKey.DeleteAction.Should().Be("c");
    }

    [Fact]
    public async Task BookingForeignKey_WhenEventDoesNotExist_ThrowsDbUpdateException()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        await using var context = _fixture.CreateDbContext();
        context.Bookings.Add(Booking.CreatePending(Guid.NewGuid(), Utc(2026, 6, 20, 10, 0, 0)));

        var act = async () => await context.SaveChangesAsync(cancellationToken);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task EventTitle_WhenValueExceedsMaxLength_ThrowsDbUpdateException()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        await using var context = _fixture.CreateDbContext();
        var eventItem = Event.Create(
            title: new string('A', 200),
            startAt: Utc(2026, 6, 21, 10, 0, 0),
            endAt: Utc(2026, 6, 21, 12, 0, 0),
            totalSeats: 10);

        context.Events.Add(eventItem);
        await context.SaveChangesAsync(cancellationToken);

        await using var actContext = _fixture.CreateDbContext();
        var storedEvent = await actContext.Events.FirstAsync(item => item.Id == eventItem.Id, cancellationToken);
        storedEvent.Update(new string('B', 201), storedEvent.StartAt, storedEvent.EndAt, storedEvent.Description);

        var act = async () => await actContext.SaveChangesAsync(cancellationToken);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // Для проверки метаданных PostgreSQL используем прямые запросы к системным каталогам через Npgsql.
    // EF-only подход здесь менее удобен, потому что нам нужны information_schema, pg_constraint и pg_index.
    private static async Task<bool> TableExistsAsync(AppDbContext context, string tableName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = @table_name
            )
            """;

        await using var connection = new NpgsqlConnection(context.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("table_name", tableName);

        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<string[]> GetColumnNamesAsync(AppDbContext context, string tableName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @table_name
            ORDER BY ordinal_position
            """;

        return await QueryStringsAsync(context, sql, cancellationToken, [new NpgsqlParameter("table_name", tableName)]);
    }

    private static async Task<string[]> GetNotNullColumnsAsync(AppDbContext context, string tableName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = @table_name
              AND is_nullable = 'NO'
            ORDER BY ordinal_position
            """;

        return await QueryStringsAsync(context, sql, cancellationToken, [new NpgsqlParameter("table_name", tableName)]);
    }

    private static async Task<IReadOnlyCollection<string>> GetPrimaryKeyColumnsAsync(AppDbContext context, string tableName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT a.attname
            FROM pg_index i
            JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = ANY(i.indkey)
            JOIN pg_class c ON c.oid = i.indrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE i.indisprimary
              AND n.nspname = 'public'
              AND c.relname = @table_name
            ORDER BY a.attnum
            """;

        return await QueryStringsAsync(context, sql, cancellationToken, [new NpgsqlParameter("table_name", tableName)]);
    }

    private static async Task<ForeignKeyInfo?> GetForeignKeyAsync(AppDbContext context, string sourceTable, string targetTable, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                con.conname,
                src.attname,
                tgt.attname,
                con.confdeltype
            FROM pg_constraint con
            JOIN pg_class src_table ON src_table.oid = con.conrelid
            JOIN pg_class tgt_table ON tgt_table.oid = con.confrelid
            JOIN pg_namespace src_ns ON src_ns.oid = src_table.relnamespace
            JOIN pg_namespace tgt_ns ON tgt_ns.oid = tgt_table.relnamespace
            JOIN LATERAL unnest(con.conkey) WITH ORDINALITY AS src_keys(attnum, ord) ON TRUE
            JOIN pg_attribute src ON src.attrelid = con.conrelid AND src.attnum = src_keys.attnum
            JOIN LATERAL unnest(con.confkey) WITH ORDINALITY AS tgt_keys(attnum, ord) ON tgt_keys.ord = src_keys.ord
            JOIN pg_attribute tgt ON tgt.attrelid = con.confrelid AND tgt.attnum = tgt_keys.attnum
            WHERE con.contype = 'f'
              AND src_ns.nspname = 'public'
              AND tgt_ns.nspname = 'public'
              AND src_table.relname = @source_table
              AND tgt_table.relname = @target_table
            LIMIT 1
            """;

        await using var connection = new NpgsqlConnection(context.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("source_table", sourceTable);
        command.Parameters.AddWithValue("target_table", targetTable);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ForeignKeyInfo
        {
            Name = reader.GetString(0),
            SourceColumn = reader.GetString(1),
            TargetColumn = reader.GetString(2),
            DeleteAction = reader.GetFieldValue<char>(3).ToString()
        };
    }

    private static async Task<string[]> QueryStringsAsync(
        AppDbContext context,
        string sql,
        CancellationToken cancellationToken,
        params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(context.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);

        var results = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(reader.GetString(0));
        }

        return results.ToArray();
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute, int second)
    {
        return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
    }

    private sealed class ForeignKeyInfo
    {
        public string Name { get; set; } = string.Empty;

        public string SourceColumn { get; set; } = string.Empty;

        public string TargetColumn { get; set; } = string.Empty;

        public string DeleteAction { get; set; } = string.Empty;
    }
}
