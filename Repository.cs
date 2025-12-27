using Dapper;
using Microsoft.Data.Sqlite;
using System.Data;

namespace JsonbStore;

/// <summary>
/// A high-performance repository for storing JSON objects and binary signals in a single SQLite file.
/// Uses Dapper for minimal mapping overhead and supports both JSON document storage and raw binary data.
/// </summary>
public class Repository : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly bool _ownsConnection;

    /// <summary>
    /// Initializes a new repository with the specified SQLite database file.
    /// Automatically configures WAL mode and synchronous=NORMAL for optimal performance.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite database file</param>
    public Repository(string databasePath)
    {
        _connection = new SqliteConnection($"Data Source={databasePath}");
        _connection.Open();
        _ownsConnection = true;
        ConfigureConnection();
    }

    /// <summary>
    /// Initializes a new repository with an existing SQLite connection.
    /// </summary>
    /// <param name="connection">An open SQLite connection</param>
    public Repository(SqliteConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _ownsConnection = false;
        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }
        ConfigureConnection();
    }

    /// <summary>
    /// Configures SQLite for optimal performance with WAL mode and synchronous=NORMAL.
    /// </summary>
    private void ConfigureConnection()
    {
        _connection.Execute("PRAGMA journal_mode = WAL;");
        _connection.Execute("PRAGMA synchronous = NORMAL;");
    }

    /// <summary>
    /// Creates a table for storing JSON objects with a generic schema.
    /// </summary>
    /// <param name="tableName">Name of the table to create</param>
    public void CreateJsonTable(string tableName)
    {
        var sql = $@"
            CREATE TABLE IF NOT EXISTS {tableName} (
                id TEXT PRIMARY KEY,
                data TEXT NOT NULL,
                created_at INTEGER NOT NULL DEFAULT (strftime('%s', 'now')),
                updated_at INTEGER NOT NULL DEFAULT (strftime('%s', 'now'))
            )";
        _connection.Execute(sql);
    }

    /// <summary>
    /// Creates a table for storing binary signals (e.g., EEG, EMG, EKG data).
    /// </summary>
    /// <param name="tableName">Name of the table to create</param>
    public void CreateSignalTable(string tableName)
    {
        var sql = $@"
            CREATE TABLE IF NOT EXISTS {tableName} (
                id TEXT PRIMARY KEY,
                signal_type TEXT NOT NULL,
                sample_rate REAL,
                channels INTEGER,
                data BLOB NOT NULL,
                metadata TEXT,
                created_at INTEGER NOT NULL DEFAULT (strftime('%s', 'now'))
            )";
        _connection.Execute(sql);
    }

    /// <summary>
    /// Inserts or updates a JSON object in the specified table.
    /// </summary>
    /// <typeparam name="T">Type of the object to store</typeparam>
    /// <param name="tableName">Name of the table</param>
    /// <param name="id">Unique identifier for the object</param>
    /// <param name="data">The object to store</param>
    public void UpsertJson<T>(string tableName, string id, T data)
    {
        var sql = $@"
            INSERT INTO {tableName} (id, data, updated_at)
            VALUES (@Id, @Data, strftime('%s', 'now'))
            ON CONFLICT(id) DO UPDATE SET
                data = excluded.data,
                updated_at = excluded.updated_at";

        _connection.Execute(sql, new
        {
            Id = id,
            Data = System.Text.Json.JsonSerializer.Serialize(data)
        });
    }

    /// <summary>
    /// Retrieves a JSON object by its ID from the specified table.
    /// </summary>
    /// <typeparam name="T">Type of the object to retrieve</typeparam>
    /// <param name="tableName">Name of the table</param>
    /// <param name="id">Unique identifier of the object</param>
    /// <returns>The deserialized object, or default if not found</returns>
    public T? GetJson<T>(string tableName, string id)
    {
        var sql = $"SELECT data FROM {tableName} WHERE id = @Id";
        var json = _connection.QueryFirstOrDefault<string>(sql, new { Id = id });

        if (string.IsNullOrEmpty(json))
        {
            return default;
        }

        return System.Text.Json.JsonSerializer.Deserialize<T>(json);
    }

    /// <summary>
    /// Retrieves all JSON objects from the specified table.
    /// </summary>
    /// <typeparam name="T">Type of the objects to retrieve</typeparam>
    /// <param name="tableName">Name of the table</param>
    /// <returns>An enumerable of deserialized objects</returns>
    public IEnumerable<T> GetAllJson<T>(string tableName)
    {
        var sql = $"SELECT data FROM {tableName}";
        var jsonResults = _connection.Query<string>(sql);

        foreach (var json in jsonResults)
        {
            yield return System.Text.Json.JsonSerializer.Deserialize<T>(json)!;
        }
    }

    /// <summary>
    /// Deletes a JSON object by its ID from the specified table.
    /// </summary>
    /// <param name="tableName">Name of the table</param>
    /// <param name="id">Unique identifier of the object to delete</param>
    /// <returns>True if the object was deleted, false if it didn't exist</returns>
    public bool DeleteJson(string tableName, string id)
    {
        var sql = $"DELETE FROM {tableName} WHERE id = @Id";
        var affectedRows = _connection.Execute(sql, new { Id = id });
        return affectedRows > 0;
    }

    /// <summary>
    /// Inserts or updates a binary signal in the specified table.
    /// </summary>
    /// <param name="tableName">Name of the table</param>
    /// <param name="id">Unique identifier for the signal</param>
    /// <param name="signalType">Type of signal (e.g., "EEG", "EMG", "EKG")</param>
    /// <param name="data">Raw binary signal data</param>
    /// <param name="sampleRate">Sample rate in Hz (optional)</param>
    /// <param name="channels">Number of channels (optional)</param>
    /// <param name="metadata">Additional metadata as JSON (optional)</param>
    public void UpsertSignal(string tableName, string id, string signalType, byte[] data,
        double? sampleRate = null, int? channels = null, string? metadata = null)
    {
        var sql = $@"
            INSERT INTO {tableName} (id, signal_type, sample_rate, channels, data, metadata)
            VALUES (@Id, @SignalType, @SampleRate, @Channels, @Data, @Metadata)
            ON CONFLICT(id) DO UPDATE SET
                signal_type = excluded.signal_type,
                sample_rate = excluded.sample_rate,
                channels = excluded.channels,
                data = excluded.data,
                metadata = excluded.metadata";

        _connection.Execute(sql, new
        {
            Id = id,
            SignalType = signalType,
            SampleRate = sampleRate,
            Channels = channels,
            Data = data,
            Metadata = metadata
        });
    }

    /// <summary>
    /// Retrieves a binary signal by its ID from the specified table.
    /// </summary>
    /// <param name="tableName">Name of the table</param>
    /// <param name="id">Unique identifier of the signal</param>
    /// <returns>The signal data and metadata, or null if not found</returns>
    public SignalData? GetSignal(string tableName, string id)
    {
        var sql = $@"
            SELECT id, signal_type, sample_rate, channels, data, metadata, created_at
            FROM {tableName}
            WHERE id = @Id";

        return _connection.QueryFirstOrDefault<SignalData>(sql, new { Id = id });
    }

    /// <summary>
    /// Retrieves all signals of a specific type from the specified table.
    /// </summary>
    /// <param name="tableName">Name of the table</param>
    /// <param name="signalType">Type of signal to filter by (optional)</param>
    /// <returns>An enumerable of signal data</returns>
    public IEnumerable<SignalData> GetSignals(string tableName, string? signalType = null)
    {
        var sql = string.IsNullOrEmpty(signalType)
            ? $"SELECT id, signal_type, sample_rate, channels, data, metadata, created_at FROM {tableName}"
            : $"SELECT id, signal_type, sample_rate, channels, data, metadata, created_at FROM {tableName} WHERE signal_type = @SignalType";

        return _connection.Query<SignalData>(sql, new { SignalType = signalType });
    }

    /// <summary>
    /// Deletes a signal by its ID from the specified table.
    /// </summary>
    /// <param name="tableName">Name of the table</param>
    /// <param name="id">Unique identifier of the signal to delete</param>
    /// <returns>True if the signal was deleted, false if it didn't exist</returns>
    public bool DeleteSignal(string tableName, string id)
    {
        var sql = $"DELETE FROM {tableName} WHERE id = @Id";
        var affectedRows = _connection.Execute(sql, new { Id = id });
        return affectedRows > 0;
    }

    /// <summary>
    /// Executes a batch of operations within a transaction for optimal performance.
    /// </summary>
    /// <param name="action">Action to execute within the transaction</param>
    public void ExecuteInTransaction(Action<IDbTransaction> action)
    {
        using var transaction = _connection.BeginTransaction();
        try
        {
            action(transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Executes a batch of operations within a transaction for optimal performance.
    /// </summary>
    /// <param name="action">Action to execute within the transaction</param>
    public void ExecuteInTransaction(Action action)
    {
        using var transaction = _connection.BeginTransaction();
        try
        {
            action();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Gets the underlying SQLite connection for advanced operations.
    /// </summary>
    public SqliteConnection Connection => _connection;

    /// <summary>
    /// Disposes the repository and closes the database connection if owned.
    /// </summary>
    public void Dispose()
    {
        if (_ownsConnection)
        {
            _connection?.Dispose();
        }
    }
}

/// <summary>
/// Represents binary signal data retrieved from the database.
/// </summary>
public class SignalData
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>Gets or sets the signal type (e.g., EEG, EMG, EKG).</summary>
    public string SignalType { get; set; } = string.Empty;
    /// <summary>Gets or sets the sample rate in Hz.</summary>
    public double? SampleRate { get; set; }
    /// <summary>Gets or sets the number of channels.</summary>
    public int? Channels { get; set; }
    /// <summary>Gets or sets the raw binary signal data.</summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();
    /// <summary>Gets or sets additional metadata as JSON.</summary>
    public string? Metadata { get; set; }
    /// <summary>Gets or sets the creation timestamp (Unix epoch).</summary>
    public long CreatedAt { get; set; }
}
