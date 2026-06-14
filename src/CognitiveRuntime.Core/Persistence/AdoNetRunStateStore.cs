using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.Contracts;
using CognitiveRuntime.Core.Runtime;

namespace CognitiveRuntime.Core.Persistence;

public enum RelationalDatabaseDialect
{
    PostgreSql,
    SqlServer
}

public sealed record AdoNetRunStateStoreOptions(
    string ConnectionString,
    RelationalDatabaseDialect Dialect);

public sealed class RunStateStoreException : Exception
{
    public RunStateStoreException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public static class DbProviderFactoryResolver
{
    public static DbProviderFactory Resolve(string assemblyQualifiedTypeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyQualifiedTypeName);

        var factoryType = Type.GetType(
            assemblyQualifiedTypeName,
            throwOnError: true,
            ignoreCase: false)!;
        if (!typeof(DbProviderFactory).IsAssignableFrom(factoryType))
        {
            throw new InvalidOperationException(
                $"Type '{assemblyQualifiedTypeName}' is not a DbProviderFactory.");
        }

        var instance = factoryType
            .GetProperty(
                "Instance",
                BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null)
            ?? factoryType
                .GetField(
                    "Instance",
                    BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);

        return instance as DbProviderFactory
            ?? throw new InvalidOperationException(
                $"DbProviderFactory '{assemblyQualifiedTypeName}' must expose " +
                "a public static Instance member.");
    }
}

public sealed class AdoNetRunStateStore : IRunStateStore
{
    private const string TableName = "runtime_runs";

    private static readonly JsonSerializerOptions JsonOptions = new(
        JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    private readonly DbProviderFactory _providerFactory;
    private readonly AdoNetRunStateStoreOptions _options;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private bool _initialized;

    public AdoNetRunStateStore(
        DbProviderFactory providerFactory,
        AdoNetRunStateStoreOptions options)
    {
        _providerFactory = providerFactory
            ?? throw new ArgumentNullException(nameof(providerFactory));
        _options = options
            ?? throw new ArgumentNullException(nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ConnectionString);
    }

    public async Task UpsertRunAsync(
        RunCatalogEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            await EnsureInitializedAsync(cancellationToken);
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction =
                await connection.BeginTransactionAsync(cancellationToken);

            var updated = await UpdateAsync(
                connection,
                transaction,
                entry,
                cancellationToken);
            if (updated == 0 &&
                !await ExistsAsync(
                    connection,
                    transaction,
                    entry.RunId,
                    cancellationToken))
            {
                await InsertAsync(
                    connection,
                    transaction,
                    entry,
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException and
                  not RunStateStoreException)
        {
            throw new RunStateStoreException(
                $"Could not persist run '{entry.RunId}' in the relational " +
                "state store.",
                exception);
        }
    }

    public async Task<RunCatalogEntry?> GetRunAsync(
        string runId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        try
        {
            await EnsureInitializedAsync(cancellationToken);
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"SELECT schema_version, generation, run_id, mode_name, " +
                $"pattern_name, model_provider, output_directory, " +
                $"lifecycle_status, outcome, created_at_utc, updated_at_utc, " +
                $"payload_json FROM {TableName} WHERE run_id = @run_id";
            AddParameter(command, "@run_id", DbType.String, runId);

            await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SingleRow,
                cancellationToken);
            return await reader.ReadAsync(cancellationToken)
                ? ReadRun(reader)
                : null;
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException and
                  not RunStateStoreException)
        {
            throw new RunStateStoreException(
                $"Could not read run '{runId}' from the relational state store.",
                exception);
        }
    }

    public async Task<IReadOnlyList<RunCatalogEntry>> ListRunsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureInitializedAsync(cancellationToken);
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"SELECT schema_version, generation, run_id, mode_name, " +
                $"pattern_name, model_provider, output_directory, " +
                $"lifecycle_status, outcome, created_at_utc, updated_at_utc, " +
                $"payload_json FROM {TableName} " +
                "ORDER BY created_at_utc, run_id";

            var entries = new List<RunCatalogEntry>();
            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                entries.Add(ReadRun(reader));
            }

            return entries;
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException and
                  not RunStateStoreException)
        {
            throw new RunStateStoreException(
                "Could not list runs from the relational state store.",
                exception);
        }
    }

    private async Task EnsureInitializedAsync(
        CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationGate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = GetCreateTableSql(_options.Dialect);
            await command.ExecuteNonQueryAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    private DbConnection CreateConnection()
    {
        var connection = _providerFactory.CreateConnection()
            ?? throw new InvalidOperationException(
                "The configured database provider did not create a connection.");
        connection.ConnectionString = _options.ConnectionString;
        return connection;
    }

    private static async Task<int> UpdateAsync(
        DbConnection connection,
        DbTransaction transaction,
        RunCatalogEntry entry,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"UPDATE {TableName} SET schema_version = @schema_version, " +
            "generation = @generation, mode_name = @mode_name, " +
            "pattern_name = @pattern_name, model_provider = @model_provider, " +
            "output_directory = @output_directory, " +
            "lifecycle_status = @lifecycle_status, outcome = @outcome, " +
            "created_at_utc = @created_at_utc, " +
            "updated_at_utc = @updated_at_utc, payload_json = @payload_json " +
            "WHERE run_id = @run_id AND generation <= @generation";
        AddEntryParameters(command, entry);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> ExistsAsync(
        DbConnection connection,
        DbTransaction transaction,
        string runId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"SELECT generation FROM {TableName} WHERE run_id = @run_id";
        AddParameter(command, "@run_id", DbType.String, runId);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task InsertAsync(
        DbConnection connection,
        DbTransaction transaction,
        RunCatalogEntry entry,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"INSERT INTO {TableName} (" +
            "schema_version, generation, run_id, mode_name, pattern_name, " +
            "model_provider, output_directory, lifecycle_status, outcome, " +
            "created_at_utc, updated_at_utc, payload_json) VALUES (" +
            "@schema_version, @generation, @run_id, @mode_name, @pattern_name, " +
            "@model_provider, @output_directory, @lifecycle_status, @outcome, " +
            "@created_at_utc, @updated_at_utc, @payload_json)";
        AddEntryParameters(command, entry);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddEntryParameters(
        DbCommand command,
        RunCatalogEntry entry)
    {
        AddParameter(
            command,
            "@schema_version",
            DbType.Int32,
            entry.SchemaVersion);
        AddParameter(command, "@generation", DbType.Int64, entry.Generation);
        AddParameter(command, "@run_id", DbType.String, entry.RunId);
        AddParameter(command, "@mode_name", DbType.String, entry.ModeName);
        AddParameter(
            command,
            "@pattern_name",
            DbType.String,
            entry.PatternName);
        AddParameter(
            command,
            "@model_provider",
            DbType.String,
            entry.ModelProvider);
        AddParameter(
            command,
            "@output_directory",
            DbType.String,
            entry.OutputDirectory);
        AddParameter(
            command,
            "@lifecycle_status",
            DbType.String,
            entry.LifecycleStatus.ToString());
        AddParameter(
            command,
            "@outcome",
            DbType.String,
            entry.Outcome?.ToString());
        AddParameter(
            command,
            "@created_at_utc",
            DbType.String,
            FormatTimestamp(entry.CreatedAt));
        AddParameter(
            command,
            "@updated_at_utc",
            DbType.String,
            FormatTimestamp(entry.UpdatedAt));
        AddParameter(
            command,
            "@payload_json",
            DbType.String,
            JsonSerializer.Serialize(entry.Payload, JsonOptions));
    }

    private static RunCatalogEntry ReadRun(DbDataReader reader)
    {
        var runId = reader.GetString(2);
        var payload = JsonSerializer.Deserialize<RunCatalogPayload>(
            reader.GetString(11),
            JsonOptions)
            ?? throw new InvalidOperationException(
                $"Run '{runId}' has an empty catalog payload.");

        return new RunCatalogEntry(
            reader.GetInt32(0),
            reader.GetInt64(1),
            runId,
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            Enum.Parse<RunLifecycleStatus>(
                reader.GetString(7),
                ignoreCase: true),
            reader.IsDBNull(8)
                ? null
                : Enum.Parse<RunOutcome>(
                    reader.GetString(8),
                    ignoreCase: true),
            ParseTimestamp(reader.GetString(9)),
            ParseTimestamp(reader.GetString(10)),
            payload);
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        DbType dbType,
        object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string timestamp) =>
        DateTimeOffset.Parse(
            timestamp,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);

    private static string GetCreateTableSql(
        RelationalDatabaseDialect dialect) =>
        dialect switch
        {
            RelationalDatabaseDialect.PostgreSql => $"""
                CREATE TABLE IF NOT EXISTS {TableName} (
                    schema_version INTEGER NOT NULL,
                    generation BIGINT NOT NULL,
                    run_id TEXT NOT NULL PRIMARY KEY,
                    mode_name TEXT NOT NULL,
                    pattern_name TEXT NOT NULL,
                    model_provider TEXT NOT NULL,
                    output_directory TEXT NOT NULL,
                    lifecycle_status TEXT NOT NULL,
                    outcome TEXT NULL,
                    created_at_utc TEXT NOT NULL,
                    updated_at_utc TEXT NOT NULL,
                    payload_json TEXT NOT NULL
                );
                """,
            RelationalDatabaseDialect.SqlServer => $"""
                IF OBJECT_ID(N'{TableName}', N'U') IS NULL
                BEGIN
                    CREATE TABLE {TableName} (
                        schema_version INT NOT NULL,
                        generation BIGINT NOT NULL,
                        run_id NVARCHAR(64) NOT NULL PRIMARY KEY,
                        mode_name NVARCHAR(256) NOT NULL,
                        pattern_name NVARCHAR(256) NOT NULL,
                        model_provider NVARCHAR(256) NOT NULL,
                        output_directory NVARCHAR(2048) NOT NULL,
                        lifecycle_status NVARCHAR(64) NOT NULL,
                        outcome NVARCHAR(64) NULL,
                        created_at_utc NVARCHAR(64) NOT NULL,
                        updated_at_utc NVARCHAR(64) NOT NULL,
                        payload_json NVARCHAR(MAX) NOT NULL
                    );
                END;
                """,
            _ => throw new ArgumentOutOfRangeException(
                nameof(dialect),
                dialect,
                null)
        };
}
