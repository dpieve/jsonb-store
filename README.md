# JSONB-STORE
A high-performance, single-file application data format using C#, SQLite (Microsoft.Data.Sqlite), and Dapper.

# Core Architecture

## Primary Format
A single SQLite .db file acting as an "Application File Format".

## Data Storage Strategy
Treat SQLite as a hybrid relational/document store. Small JSON metadata files are stored as TEXT to avoid filesystem overhead.

## Data Access Layer
Use Dapper for next-to-zero mapping overhead.

## Custom Logic
Automatic JSON serialization/deserialization of C# objects into SQLite TEXT columns using System.Text.Json.

## Performance Requirements
- Minimize System Calls: The design must utilize SQLite's ability to be up to 35% faster than raw file I/O for small blobs by reducing open() and close() operations.
- Transaction Batching: All writes must be grouped into transactions to maintain high write speed.
- Async Operations: All database operations are async for optimal performance and scalability.

## Configuration
The library defaults to WAL (Write-Ahead Logging) mode and synchronous = NORMAL for optimal balance between safety and performance.

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
await using var repo = new Repository("mydata.db");

// Create a table (table name will be "Person")
await repo.CreateTableAsync<Person>();

// Insert or update
await repo.UpsertAsync("person1", new Person 
{ 
    Name = "John Doe", 
    Age = 30, 
    Email = "john@example.com" 
});

// Retrieve
var person = await repo.GetAsync<Person>("person1");

// Get all
var allPeople = await repo.GetAllAsync<Person>();

// Delete
await repo.DeleteAsync<Person>("person1");
```

### Transaction Batching

```csharp
// Batch multiple operations in a transaction for performance
await repo.ExecuteInTransactionAsync(async () =>
{
    for (int i = 0; i < 1000; i++)
    {
        await repo.UpsertAsync($"record_{i}", new MyData { /* ... */ });
    }
});
```

## Running Tests

The project includes comprehensive unit and integration tests using xUnit.

Run unit tests:
```bash
cd tests/JsonbStore.UnitTests
dotnet test
```

Run integration tests:
```bash
cd tests/JsonbStore.IntegrationTests
dotnet test
```

Run all tests:
```bash
dotnet test
```

## Features

- ✅ **Generic Repository Pattern**: Type-safe CRUD operations with automatic table naming
- ✅ **Async/Await**: All database operations are fully async
- ✅ **Transaction Support**: Batch operations for high-performance writes
- ✅ **WAL Mode**: Automatically configured for optimal concurrency
- ✅ **Zero SQL Injection Risk**: Table names derived from types, not user input
- ✅ **Cross-Platform**: Works on Windows, Linux, and macOS
- ✅ **.NET 10**: Built on the latest .NET platform
- ✅ **Comprehensive Tests**: Unit and integration tests with xUnit

## Dependencies

- .NET 10
- Dapper 2.1.66
- Microsoft.Data.Sqlite 10.0.0
