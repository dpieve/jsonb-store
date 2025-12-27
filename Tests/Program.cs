using JsonbStore;
using System.Text.Json;

namespace JsonbStore.Demo;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== JSONB-Store Library Demo ===\n");

        // Test 1: JSON Storage
        Console.WriteLine("Test 1: JSON Object Storage");
        Console.WriteLine("----------------------------");
        TestJsonStorage();

        Console.WriteLine("\nTest 2: Binary Signal Storage");
        Console.WriteLine("----------------------------");
        TestSignalStorage();

        Console.WriteLine("\nTest 3: Transaction Batching");
        Console.WriteLine("----------------------------");
        TestTransactionBatching();

        Console.WriteLine("\n=== All tests completed successfully! ===");
    }

    static void TestJsonStorage()
    {
        using var repo = new Repository("/tmp/test_json.db");
        repo.CreateJsonTable("people");

        // Insert test data
        repo.UpsertJson("people", "1", new Person
        {
            Name = "Alice Smith",
            Age = 30,
            Email = "alice@example.com"
        });

        repo.UpsertJson("people", "2", new Person
        {
            Name = "Bob Johnson",
            Age = 25,
            Email = "bob@example.com"
        });

        // Retrieve
        var person1 = repo.GetJson<Person>("people", "1");
        Console.WriteLine($"Retrieved: {person1?.Name}, Age: {person1?.Age}, Email: {person1?.Email}");

        // Update
        repo.UpsertJson("people", "1", new Person
        {
            Name = "Alice Smith",
            Age = 31,
            Email = "alice@example.com"
        });

        person1 = repo.GetJson<Person>("people", "1");
        Console.WriteLine($"After update: {person1?.Name}, Age: {person1?.Age}");

        // Get all
        var allPeople = repo.GetAllJson<Person>("people").ToList();
        Console.WriteLine($"Total people in database: {allPeople.Count}");

        // Delete
        var deleted = repo.DeleteJson("people", "2");
        Console.WriteLine($"Deleted person 2: {deleted}");

        allPeople = repo.GetAllJson<Person>("people").ToList();
        Console.WriteLine($"Remaining people: {allPeople.Count}");
    }

    static void TestSignalStorage()
    {
        using var repo = new Repository("/tmp/test_signals.db");
        repo.CreateSignalTable("biosignals");

        // Create sample EEG data
        var eegData = GenerateSampleSignal(250.0, 5, 8); // 5 seconds, 8 channels, 250 Hz

        var metadata = JsonSerializer.Serialize(new
        {
            PatientId = "P001",
            RecordingDate = DateTime.UtcNow,
            DeviceModel = "NeuroScan-X"
        });

        // Store signal
        repo.UpsertSignal(
            tableName: "biosignals",
            id: "eeg_001",
            signalType: "EEG",
            data: eegData,
            sampleRate: 250.0,
            channels: 8,
            metadata: metadata
        );

        // Store EMG signal
        var emgData = GenerateSampleSignal(1000.0, 3, 4); // 3 seconds, 4 channels, 1000 Hz
        repo.UpsertSignal(
            tableName: "biosignals",
            id: "emg_001",
            signalType: "EMG",
            data: emgData,
            sampleRate: 1000.0,
            channels: 4
        );

        // Retrieve
        var eegSignal = repo.GetSignal("biosignals", "eeg_001");
        if (eegSignal != null)
        {
            Console.WriteLine($"Retrieved {eegSignal.SignalType} signal:");
            Console.WriteLine($"  Sample Rate: {eegSignal.SampleRate} Hz");
            Console.WriteLine($"  Channels: {eegSignal.Channels}");
            Console.WriteLine($"  Data Size: {eegSignal.Data.Length} bytes");
            if (!string.IsNullOrEmpty(eegSignal.Metadata))
            {
                Console.WriteLine($"  Has metadata: Yes");
            }
        }

        // Get all signals
        var allSignals = repo.GetSignals("biosignals").ToList();
        Console.WriteLine($"Total signals: {allSignals.Count}");

        // Get filtered by type
        var eegSignals = repo.GetSignals("biosignals", "EEG").ToList();
        Console.WriteLine($"EEG signals only: {eegSignals.Count}");
    }

    static void TestTransactionBatching()
    {
        using var repo = new Repository("/tmp/test_batch.db");
        repo.CreateJsonTable("metrics");

        var startTime = DateTime.UtcNow;

        // Batch insert 100 records in a single transaction
        repo.ExecuteInTransaction(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                repo.UpsertJson("metrics", $"metric_{i}", new
                {
                    Timestamp = DateTime.UtcNow,
                    Value = Random.Shared.NextDouble() * 100,
                    Sensor = $"sensor_{i % 10}"
                });
            }
        });

        var elapsed = DateTime.UtcNow - startTime;
        Console.WriteLine($"Inserted 100 records in {elapsed.TotalMilliseconds:F2} ms");

        var count = repo.GetAllJson<dynamic>("metrics").Count();
        Console.WriteLine($"Verified: {count} records in database");
    }

    static byte[] GenerateSampleSignal(double sampleRate, int durationSeconds, int channels)
    {
        int samplesPerChannel = (int)(sampleRate * durationSeconds);
        int totalSamples = samplesPerChannel * channels;
        var data = new byte[totalSamples * sizeof(float)];
        var random = new Random();

        for (int i = 0; i < totalSamples; i++)
        {
            float value = (float)((random.NextDouble() - 0.5) * 200);
            BitConverter.GetBytes(value).CopyTo(data, i * sizeof(float));
        }

        return data;
    }
}

public class Person
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Email { get; set; } = string.Empty;
}
