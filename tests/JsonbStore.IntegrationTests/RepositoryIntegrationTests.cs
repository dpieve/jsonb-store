using Dapper;
using FluentAssertions;
using Xunit;

namespace JsonbStore.IntegrationTests;

public class RepositoryIntegrationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly Repository _repository;

    public RepositoryIntegrationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_integration_{Guid.NewGuid()}.db");
        _repository = new Repository(_testDbPath);
    }

    public void Dispose()
    {
        _repository.Dispose();
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }

    [Fact]
    public async Task CreateTableAsync_CreatesTable()
    {
        // Act
        await _repository.CreateTableAsync<Person>();

        // Assert
        var checkSql = "SELECT name FROM sqlite_master WHERE type='table' AND name='Person'";
        var result = _repository.Connection.QueryFirstOrDefault<string>(checkSql);
        result.Should().Be("Person");
    }

    [Fact]
    public async Task UpsertAsync_InsertsNewRecord()
    {
        // Arrange
        await _repository.CreateTableAsync<Person>();
        var person = new Person { Name = "John Doe", Age = 30, Email = "john@example.com" };

        // Act
        await _repository.UpsertAsync("person1", person);

        // Assert
        var retrieved = await _repository.GetAsync<Person>("person1");
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("John Doe");
        retrieved.Age.Should().Be(30);
        retrieved.Email.Should().Be("john@example.com");
    }

    [Fact]
    public async Task UpsertAsync_UpdatesExistingRecord()
    {
        // Arrange
        await _repository.CreateTableAsync<Person>();
        var person1 = new Person { Name = "John Doe", Age = 30, Email = "john@example.com" };
        await _repository.UpsertAsync("person1", person1);

        // Act
        var person2 = new Person { Name = "John Doe", Age = 31, Email = "john.doe@example.com" };
        await _repository.UpsertAsync("person1", person2);

        // Assert
        var retrieved = await _repository.GetAsync<Person>("person1");
        retrieved.Should().NotBeNull();
        retrieved!.Age.Should().Be(31);
        retrieved.Email.Should().Be("john.doe@example.com");
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenRecordNotFound()
    {
        // Arrange
        await _repository.CreateTableAsync<Person>();

        // Act
        var result = await _repository.GetAsync<Person>("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllRecords()
    {
        // Arrange
        await _repository.CreateTableAsync<Person>();
        await _repository.UpsertAsync("person1", new Person { Name = "Alice", Age = 25, Email = "alice@example.com" });
        await _repository.UpsertAsync("person2", new Person { Name = "Bob", Age = 30, Email = "bob@example.com" });
        await _repository.UpsertAsync("person3", new Person { Name = "Charlie", Age = 35, Email = "charlie@example.com" });

        // Act
        var results = await _repository.GetAllAsync<Person>();

        // Assert
        results.Should().HaveCount(3);
        results.Should().Contain(p => p.Name == "Alice");
        results.Should().Contain(p => p.Name == "Bob");
        results.Should().Contain(p => p.Name == "Charlie");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyList_WhenNoRecords()
    {
        // Arrange
        await _repository.CreateTableAsync<Person>();

        // Act
        var results = await _repository.GetAllAsync<Person>();

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_DeletesRecord_ReturnsTrue()
    {
        // Arrange
        await _repository.CreateTableAsync<Person>();
        await _repository.UpsertAsync("person1", new Person { Name = "John Doe", Age = 30, Email = "john@example.com" });

        // Act
        var result = await _repository.DeleteAsync<Person>("person1");

        // Assert
        result.Should().BeTrue();
        var retrieved = await _repository.GetAsync<Person>("person1");
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenRecordNotFound()
    {
        // Arrange
        await _repository.CreateTableAsync<Person>();

        // Act
        var result = await _repository.DeleteAsync<Person>("nonexistent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_CommitsAllOperations()
    {
        // Arrange
        await _repository.CreateTableAsync<Person>();

        // Act
        await _repository.ExecuteInTransactionAsync(async () =>
        {
            await _repository.UpsertAsync("person1", new Person { Name = "Alice", Age = 25, Email = "alice@example.com" });
            await _repository.UpsertAsync("person2", new Person { Name = "Bob", Age = 30, Email = "bob@example.com" });
            await _repository.UpsertAsync("person3", new Person { Name = "Charlie", Age = 35, Email = "charlie@example.com" });
        });

        // Assert
        var results = await _repository.GetAllAsync<Person>();
        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_RollsBackOnException()
    {
        // Arrange
        await _repository.CreateTableAsync<Person>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _repository.ExecuteInTransactionAsync(async () =>
            {
                await _repository.UpsertAsync("person1", new Person { Name = "Alice", Age = 25, Email = "alice@example.com" });
                throw new InvalidOperationException("Test exception");
            });
        });

        // Verify rollback
        var results = await _repository.GetAllAsync<Person>();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task Repository_HandlesComplexObjects()
    {
        // Arrange
        await _repository.CreateTableAsync<ComplexData>();
        var complexData = new ComplexData
        {
            Id = 1,
            Name = "Test",
            Tags = new List<string> { "tag1", "tag2", "tag3" },
            Metadata = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } },
            Timestamp = DateTime.UtcNow
        };

        // Act
        await _repository.UpsertAsync("complex1", complexData);
        var retrieved = await _repository.GetAsync<ComplexData>("complex1");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(1);
        retrieved.Name.Should().Be("Test");
        retrieved.Tags.Should().HaveCount(3);
        retrieved.Metadata.Should().HaveCount(2);
    }

    // Test models
    public class Person
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Email { get; set; } = string.Empty;
    }

    public class ComplexData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }
}
