using JsonbStore;
using System.Text.Json;

namespace JsonbStore.Examples;

/// <summary>
/// Example usage of the JsonbStore library demonstrating JSON object storage
/// and binary signal storage capabilities.
/// </summary>
public static class UsageExamples
{
    /// <summary>
    /// Example person class for JSON storage demonstration.
    /// </summary>
    public class Person
    {
        /// <summary>Gets or sets the person's name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Gets or sets the person's age.</summary>
        public int Age { get; set; }
        /// <summary>Gets or sets the person's email address.</summary>
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>
    /// Example metadata class for biosignals.
    /// </summary>
    public class SignalMetadata
    {
        /// <summary>Gets or sets the patient identifier.</summary>
        public string PatientId { get; set; } = string.Empty;
        /// <summary>Gets or sets the recording date and time.</summary>
        public DateTime RecordingDate { get; set; }
        /// <summary>Gets or sets the device model used for recording.</summary>
        public string DeviceModel { get; set; } = string.Empty;
    }

    /// <summary>
    /// Demonstrates basic JSON object storage and retrieval.
    /// </summary>
    public static void JsonStorageExample()
    {
        using var repo = new Repository("example.db");

        // Create a table for storing person objects
        repo.CreateJsonTable("people");

        // Insert some people
        repo.UpsertJson("people", "person1", new Person
        {
            Name = "John Doe",
            Age = 30,
            Email = "john@example.com"
        });

        repo.UpsertJson("people", "person2", new Person
        {
            Name = "Jane Smith",
            Age = 25,
            Email = "jane@example.com"
        });

        // Retrieve a specific person
        var person = repo.GetJson<Person>("people", "person1");
        Console.WriteLine($"Retrieved: {person?.Name}, Age: {person?.Age}");

        // Update a person
        repo.UpsertJson("people", "person1", new Person
        {
            Name = "John Doe",
            Age = 31,  // Birthday!
            Email = "john@example.com"
        });

        // Get all people
        var allPeople = repo.GetAllJson<Person>("people");
        foreach (var p in allPeople)
        {
            Console.WriteLine($"- {p.Name}, {p.Age}");
        }

        // Delete a person
        repo.DeleteJson("people", "person2");
    }

    /// <summary>
    /// Demonstrates binary signal storage for biosignals like EEG, EMG, EKG.
    /// </summary>
    public static void BiosignalStorageExample()
    {
        using var repo = new Repository("biosignals.db");

        // Create a table for storing biosignals
        repo.CreateSignalTable("recordings");

        // Simulate EEG data (in practice, this would be real signal data)
        var eegData = GenerateSampleEEGData(sampleRate: 250.0, durationSeconds: 10, channels: 8);

        // Store EEG signal with metadata
        var metadata = new SignalMetadata
        {
            PatientId = "P001",
            RecordingDate = DateTime.UtcNow,
            DeviceModel = "NeuroScan-X"
        };

        repo.UpsertSignal(
            tableName: "recordings",
            id: "eeg_001",
            signalType: "EEG",
            data: eegData,
            sampleRate: 250.0,
            channels: 8,
            metadata: JsonSerializer.Serialize(metadata)
        );

        // Simulate EMG data
        var emgData = GenerateSampleEMGData(sampleRate: 1000.0, durationSeconds: 5, channels: 4);

        repo.UpsertSignal(
            tableName: "recordings",
            id: "emg_001",
            signalType: "EMG",
            data: emgData,
            sampleRate: 1000.0,
            channels: 4
        );

        // Retrieve a specific signal
        var eegSignal = repo.GetSignal("recordings", "eeg_001");
        if (eegSignal != null)
        {
            Console.WriteLine($"Retrieved EEG signal: {eegSignal.SignalType}, " +
                            $"Sample Rate: {eegSignal.SampleRate} Hz, " +
                            $"Channels: {eegSignal.Channels}, " +
                            $"Data Size: {eegSignal.Data.Length} bytes");

            if (!string.IsNullOrEmpty(eegSignal.Metadata))
            {
                var meta = JsonSerializer.Deserialize<SignalMetadata>(eegSignal.Metadata);
                Console.WriteLine($"Patient: {meta?.PatientId}, Device: {meta?.DeviceModel}");
            }
        }

        // Get all EEG signals
        var eegSignals = repo.GetSignals("recordings", "EEG");
        Console.WriteLine($"Total EEG recordings: {eegSignals.Count()}");

        // Get all signals
        var allSignals = repo.GetSignals("recordings");
        foreach (var signal in allSignals)
        {
            Console.WriteLine($"- {signal.Id}: {signal.SignalType}, {signal.Data.Length} bytes");
        }
    }

    /// <summary>
    /// Demonstrates transaction batching for high-performance writes.
    /// </summary>
    public static void TransactionBatchingExample()
    {
        using var repo = new Repository("batch.db");
        repo.CreateJsonTable("metrics");

        // Batch insert 1000 records in a single transaction
        repo.ExecuteInTransaction(() =>
        {
            for (int i = 0; i < 1000; i++)
            {
                repo.UpsertJson("metrics", $"metric_{i}", new
                {
                    Timestamp = DateTime.UtcNow,
                    Value = Random.Shared.NextDouble() * 100,
                    Sensor = $"sensor_{i % 10}"
                });
            }
        });

        Console.WriteLine("Batch insert completed successfully");
    }

    /// <summary>
    /// Generates sample EEG data for demonstration purposes.
    /// In a real application, this would be actual signal data from a device.
    /// </summary>
    private static byte[] GenerateSampleEEGData(double sampleRate, int durationSeconds, int channels)
    {
        int samplesPerChannel = (int)(sampleRate * durationSeconds);
        int totalSamples = samplesPerChannel * channels;

        // Each sample is a 4-byte float
        var data = new byte[totalSamples * sizeof(float)];
        var random = new Random();

        for (int i = 0; i < totalSamples; i++)
        {
            // Simulate EEG signal (typically in microvolts, range -100 to 100)
            float value = (float)((random.NextDouble() - 0.5) * 200);
            BitConverter.GetBytes(value).CopyTo(data, i * sizeof(float));
        }

        return data;
    }

    /// <summary>
    /// Generates sample EMG data for demonstration purposes.
    /// </summary>
    private static byte[] GenerateSampleEMGData(double sampleRate, int durationSeconds, int channels)
    {
        int samplesPerChannel = (int)(sampleRate * durationSeconds);
        int totalSamples = samplesPerChannel * channels;

        // Each sample is a 4-byte float
        var data = new byte[totalSamples * sizeof(float)];
        var random = new Random();

        for (int i = 0; i < totalSamples; i++)
        {
            // Simulate EMG signal (typically higher amplitude than EEG)
            float value = (float)((random.NextDouble() - 0.5) * 1000);
            BitConverter.GetBytes(value).CopyTo(data, i * sizeof(float));
        }

        return data;
    }
}
