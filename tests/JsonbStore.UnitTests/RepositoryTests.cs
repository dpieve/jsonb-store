using Dapper;
using FluentAssertions;
using Xunit;

namespace JsonbStore.UnitTests;

public class RepositoryTests
{
    private readonly string _testDbPath;

    public RepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
    }

    [Fact]
    public void Constructor_WithDatabasePath_CreatesRepository()
    {
        // Arrange & Act
        using var repo = new Repository(_testDbPath);

        // Assert
        repo.Should().NotBeNull();
        repo.Connection.Should().NotBeNull();
        repo.Connection.State.Should().Be(System.Data.ConnectionState.Open);
        
        // Cleanup
        File.Delete(_testDbPath);
    }

    [Fact]
    public async Task GetTableName_ReturnsTypeName()
    {
        // This tests the private method indirectly through CreateTableAsync
        // We'll verify table creation works with the correct type name
        var testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        
        using (var repo = new Repository(testDbPath))
        {
            // Act - create table should use type name
            await repo.CreateTableAsync<TestPerson>();
            
            // Assert - verify table exists with correct name
            var checkSql = "SELECT name FROM sqlite_master WHERE type='table' AND name='TestPerson'";
            var result = repo.Connection.QueryFirstOrDefault<string>(checkSql);
            result.Should().Be("TestPerson");
        }
        
        // Cleanup
        File.Delete(testDbPath);
    }

    [Fact]
    public void Dispose_DisposesConnection_WhenOwned()
    {
        // Arrange
        var repo = new Repository(_testDbPath);
        var connection = repo.Connection;

        // Act
        repo.Dispose();

        // Assert
        connection.State.Should().Be(System.Data.ConnectionState.Closed);
        
        // Cleanup
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }

    [Fact]
    public async Task DisposeAsync_DisposesConnection_WhenOwned()
    {
        // Arrange
        var repo = new Repository(_testDbPath);
        var connection = repo.Connection;

        // Act
        await repo.DisposeAsync();

        // Assert
        connection.State.Should().Be(System.Data.ConnectionState.Closed);
        
        // Cleanup
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }

    // Helper class for testing
    public class TestPerson
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }
}
