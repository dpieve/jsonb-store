# JSONB-STORE
A high-performance, single-file application data format using C#, SQLite (Microsoft.Data.Sqlite), and Dapper.

# Core Architecture

## Primary Format
A single SQLite .db file acting as an "Application File Format".

## Data Storage Strategy
Treat SQLite as a hybrid relational/document store. Small JSON metadata files are stored as binary JSON (JSONB) or TEXT blobs to avoid filesystem overhead. High-frequency binary signal data is stored as BLOB columns for maximum throughput.

## Data Access Layer
Use Dapper for next-to-zero mapping overhead.

## Custom Logic
Implement a SqlMapper.TypeHandler<T> for Dapper to automatically handle JSON serialization/deserialization of C# objects into SQLite text/blob columns.

## Performance Requirements
- Minimize System Calls: The design must utilize SQLite's ability to be up to 35% faster than raw file I/O for small blobs by reducing open() and close() operations.
- Transaction Batching: All writes must be grouped into transactions to maintain high write speed.
- Modern SQLite Features: Utilize JSONB (SQLite 3.45+) for binary-optimized JSON storage to eliminate repetitive parsing overhead.

## Configuration
The library should default to WAL (Write-Ahead Logging) mode and synchronous = NORMAL for optimal balance between safety and performance.

# Usage

## Installation

Build the project:
```bash
dotnet build
```

## Quick Start

### JSON Object Storage

```csharp
using JsonbStore;

// Create a model class
public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
    public string Email { get; set; }
}

// Open or create a database
using var repo = new Repository("mydata.db");

// Create a table (table name will be "Person")
repo.CreateJsonTable<Person>();

// Insert or update
repo.UpsertJson("person1", new Person 
{ 
    Name = "John Doe", 
    Age = 30, 
    Email = "john@example.com" 
});

// Retrieve
var person = repo.GetJson<Person>("person1");

// Get all
var allPeople = repo.GetAllJson<Person>();

// Delete
repo.DeleteJson<Person>("person1");
```

### Binary Signal Storage (EEG, EMG, EKG)

```csharp
using JsonbStore;

// Define a signal type
public class BiosignalRecording { }

// Open database
using var repo = new Repository("biosignals.db");

// Create signal table
repo.CreateSignalTable<BiosignalRecording>();

// Store binary signal data
byte[] eegData = /* your signal data */;
repo.UpsertSignal<BiosignalRecording>(
    id: "eeg_001",
    signalType: "EEG",
    data: eegData,
    sampleRate: 250.0,
    channels: 8,
    metadata: "{\"patientId\":\"P001\"}"
);

// Retrieve signal
var signal = repo.GetSignal<BiosignalRecording>("eeg_001");

// Get all signals of a type
var eegSignals = repo.GetSignals<BiosignalRecording>("EEG");
```

### Transaction Batching

```csharp
// Batch multiple operations in a transaction for performance
repo.ExecuteInTransaction(() =>
{
    for (int i = 0; i < 1000; i++)
    {
        repo.UpsertJson($"record_{i}", new MyData { /* ... */ });
    }
});
```

## Running Examples

See the [Examples](Examples/UsageExamples.cs) folder for comprehensive usage examples, or run the demo:

```bash
cd Tests
dotnet run
```

## Features

- ✅ **Generic Repository Pattern**: Type-safe CRUD operations with automatic table naming
- ✅ **JsonTypeHandler**: Automatic JSON serialization/deserialization for Dapper
- ✅ **Binary Signal Storage**: Optimized for high-frequency biosignal data (EEG, EMG, EKG)
- ✅ **Transaction Support**: Batch operations for high-performance writes
- ✅ **WAL Mode**: Automatically configured for optimal concurrency
- ✅ **Zero SQL Injection Risk**: Table names derived from types, not user input
- ✅ **Cross-Platform**: Works on Windows, Linux, and macOS
