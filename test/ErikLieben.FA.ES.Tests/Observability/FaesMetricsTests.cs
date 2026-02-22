using System.Diagnostics;
using System.Diagnostics.Metrics;
using ErikLieben.FA.ES.Observability;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Observability;

public class FaesMetricsTests
{
    public class RecordEventsAppendedMethod
    {
        [Fact]
        public void Should_record_events_appended_with_correct_tags()
        {
            // Arrange
            var measurements = new List<(long Value, KeyValuePair<string, object?>[] Tags)>();
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == "faes.events.appended")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            {
                measurements.Add((measurement, tags.ToArray()));
            });
            meterListener.Start();

            // Act
            FaesMetrics.RecordEventsAppended(5, "order", "blob");

            // Assert
            Assert.NotEmpty(measurements);
            var last = measurements[^1];
            Assert.Equal(5, last.Value);
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.ObjectName && (string)t.Value! == "order");
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.StorageProvider && (string)t.Value! == "blob");
        }
    }

    public class RecordEventsReadMethod
    {
        [Fact]
        public void Should_record_events_read_with_correct_tags()
        {
            // Arrange
            var measurements = new List<(long Value, KeyValuePair<string, object?>[] Tags)>();
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == "faes.events.read")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            {
                measurements.Add((measurement, tags.ToArray()));
            });
            meterListener.Start();

            // Act
            FaesMetrics.RecordEventsRead(3, "workitem", "table");

            // Assert
            Assert.NotEmpty(measurements);
            var last = measurements[^1];
            Assert.Equal(3, last.Value);
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.ObjectName && (string)t.Value! == "workitem");
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.StorageProvider && (string)t.Value! == "table");
        }
    }

    public class RecordCommitMethod
    {
        [Fact]
        public void Should_record_commit_with_success_tag()
        {
            // Arrange
            var measurements = new List<(long Value, KeyValuePair<string, object?>[] Tags)>();
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == "faes.commits.total")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            {
                measurements.Add((measurement, tags.ToArray()));
            });
            meterListener.Start();

            // Act
            FaesMetrics.RecordCommit("order", "blob", success: true);

            // Assert
            Assert.NotEmpty(measurements);
            var last = measurements[^1];
            Assert.Equal(1, last.Value);
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.ObjectName && (string)t.Value! == "order");
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.StorageProvider && (string)t.Value! == "blob");
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.Success && (bool)t.Value! == true);
        }

        [Fact]
        public void Should_record_commit_with_failure_tag()
        {
            // Arrange
            var measurements = new List<(long Value, KeyValuePair<string, object?>[] Tags)>();
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == "faes.commits.total")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            {
                measurements.Add((measurement, tags.ToArray()));
            });
            meterListener.Start();

            // Act
            FaesMetrics.RecordCommit("order", "blob", success: false);

            // Assert
            Assert.NotEmpty(measurements);
            var last = measurements[^1];
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.Success && (bool)t.Value! == false);
        }
    }

    public class RecordProjectionUpdateMethod
    {
        [Fact]
        public void Should_record_projection_update_with_correct_tags()
        {
            // Arrange
            var measurements = new List<(long Value, KeyValuePair<string, object?>[] Tags)>();
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == "faes.projections.updates")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            {
                measurements.Add((measurement, tags.ToArray()));
            });
            meterListener.Start();

            // Act
            FaesMetrics.RecordProjectionUpdate("OrderDashboard", "blob");

            // Assert
            Assert.NotEmpty(measurements);
            var last = measurements[^1];
            Assert.Equal(1, last.Value);
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.ProjectionType && (string)t.Value! == "OrderDashboard");
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.StorageProvider && (string)t.Value! == "blob");
        }
    }

    public class RecordSnapshotCreatedMethod
    {
        [Fact]
        public void Should_record_snapshot_created_with_correct_tags()
        {
            // Arrange
            var measurements = new List<(long Value, KeyValuePair<string, object?>[] Tags)>();
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == "faes.snapshots.created")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            {
                measurements.Add((measurement, tags.ToArray()));
            });
            meterListener.Start();

            // Act
            FaesMetrics.RecordSnapshotCreated("order");

            // Assert
            Assert.NotEmpty(measurements);
            var last = measurements[^1];
            Assert.Equal(1, last.Value);
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.ObjectName && (string)t.Value! == "order");
        }
    }

    public class RecordUpcastMethod
    {
        [Fact]
        public void Should_record_upcast_with_correct_tags()
        {
            // Arrange
            var measurements = new List<(long Value, KeyValuePair<string, object?>[] Tags)>();
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == "faes.upcasts.performed")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            {
                measurements.Add((measurement, tags.ToArray()));
            });
            meterListener.Start();

            // Act
            FaesMetrics.RecordUpcast("OrderCreated", 1, 2);

            // Assert
            Assert.NotEmpty(measurements);
            var last = measurements[^1];
            Assert.Equal(1, last.Value);
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.EventType && (string)t.Value! == "OrderCreated");
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.UpcastFromVersion && (int)t.Value! == 1);
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.UpcastToVersion && (int)t.Value! == 2);
        }
    }

    public class RecordCatchUpItemProcessedMethod
    {
        [Fact]
        public void Should_record_catch_up_item_processed_with_correct_tags()
        {
            // Arrange
            var measurements = new List<(long Value, KeyValuePair<string, object?>[] Tags)>();
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == "faes.catchup.items_processed")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            {
                measurements.Add((measurement, tags.ToArray()));
            });
            meterListener.Start();

            // Act
            FaesMetrics.RecordCatchUpItemProcessed("project");

            // Assert
            Assert.NotEmpty(measurements);
            var last = measurements[^1];
            Assert.Equal(1, last.Value);
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.ObjectName && (string)t.Value! == "project");
        }
    }

    public class RecordCommitDurationMethod
    {
        [Fact]
        public void Should_record_commit_duration_with_correct_tags()
        {
            // Arrange
            var measurements = new List<(double Value, KeyValuePair<string, object?>[] Tags)>();
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == "faes.commit.duration")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
            {
                measurements.Add((measurement, tags.ToArray()));
            });
            meterListener.Start();

            // Act
            FaesMetrics.RecordCommitDuration(123.45, "order", "blob");

            // Assert
            Assert.NotEmpty(measurements);
            var last = measurements[^1];
            Assert.Equal(123.45, last.Value);
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.ObjectName && (string)t.Value! == "order");
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.StorageProvider && (string)t.Value! == "blob");
        }
    }

    public class RecordProjectionUpdateDurationMethod
    {
        [Fact]
        public void Should_record_projection_update_duration_with_correct_tags()
        {
            // Arrange
            var measurements = new List<(double Value, KeyValuePair<string, object?>[] Tags)>();
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == "faes.projection.update.duration")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
            {
                measurements.Add((measurement, tags.ToArray()));
            });
            meterListener.Start();

            // Act
            FaesMetrics.RecordProjectionUpdateDuration(45.67, "OrderDashboard");

            // Assert
            Assert.NotEmpty(measurements);
            var last = measurements[^1];
            Assert.Equal(45.67, last.Value);
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.ProjectionType && (string)t.Value! == "OrderDashboard");
        }
    }

    public class RecordStorageReadDurationMethod
    {
        [Fact]
        public void Should_record_storage_read_duration_with_correct_tags()
        {
            // Arrange
            var measurements = new List<(double Value, KeyValuePair<string, object?>[] Tags)>();
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == "faes.storage.read.duration")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
            {
                measurements.Add((measurement, tags.ToArray()));
            });
            meterListener.Start();

            // Act
            FaesMetrics.RecordStorageReadDuration(10.5, "blob", "order");

            // Assert
            Assert.NotEmpty(measurements);
            var last = measurements[^1];
            Assert.Equal(10.5, last.Value);
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.StorageProvider && (string)t.Value! == "blob");
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.ObjectName && (string)t.Value! == "order");
        }
    }

    public class RecordStorageWriteDurationMethod
    {
        [Fact]
        public void Should_record_storage_write_duration_with_correct_tags()
        {
            // Arrange
            var measurements = new List<(double Value, KeyValuePair<string, object?>[] Tags)>();
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == "faes.storage.write.duration")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
            {
                measurements.Add((measurement, tags.ToArray()));
            });
            meterListener.Start();

            // Act
            FaesMetrics.RecordStorageWriteDuration(20.3, "table", "workitem");

            // Assert
            Assert.NotEmpty(measurements);
            var last = measurements[^1];
            Assert.Equal(20.3, last.Value);
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.StorageProvider && (string)t.Value! == "table");
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.ObjectName && (string)t.Value! == "workitem");
        }
    }

    public class RecordEventsPerCommitMethod
    {
        [Fact]
        public void Should_record_events_per_commit_with_correct_tags()
        {
            // Arrange
            var measurements = new List<(long Value, KeyValuePair<string, object?>[] Tags)>();
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == "faes.events_per_commit")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            {
                measurements.Add((measurement, tags.ToArray()));
            });
            meterListener.Start();

            // Act
            FaesMetrics.RecordEventsPerCommit(7, "order");

            // Assert
            Assert.NotEmpty(measurements);
            var last = measurements[^1];
            Assert.Equal(7, last.Value);
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.ObjectName && (string)t.Value! == "order");
        }
    }

    public class RecordProjectionEventsFoldedMethod
    {
        [Fact]
        public void Should_record_projection_events_folded_with_correct_tags()
        {
            // Arrange
            var measurements = new List<(long Value, KeyValuePair<string, object?>[] Tags)>();
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == "faes.projection.events_folded")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            {
                measurements.Add((measurement, tags.ToArray()));
            });
            meterListener.Start();

            // Act
            FaesMetrics.RecordProjectionEventsFolded(12, "OrderDashboard");

            // Assert
            Assert.NotEmpty(measurements);
            var last = measurements[^1];
            Assert.Equal(12, last.Value);
            Assert.Contains(last.Tags, t => t.Key == FaesSemanticConventions.ProjectionType && (string)t.Value! == "OrderDashboard");
        }
    }

    public class StartTimerMethod
    {
        [Fact]
        public void Should_return_started_stopwatch()
        {
            // Act
            var stopwatch = FaesMetrics.StartTimer();

            // Assert
            Assert.NotNull(stopwatch);
            Assert.True(stopwatch.IsRunning);
        }
    }

    public class StopAndGetElapsedMsMethod
    {
        [Fact]
        public void Should_stop_stopwatch_and_return_elapsed_ms()
        {
            // Arrange
            var stopwatch = Stopwatch.StartNew();

            // Act
            var elapsedMs = FaesMetrics.StopAndGetElapsedMs(stopwatch);

            // Assert
            Assert.False(stopwatch.IsRunning);
            Assert.True(elapsedMs >= 0);
        }

        [Fact]
        public void Should_return_positive_duration_for_running_stopwatch()
        {
            // Arrange
            var stopwatch = Stopwatch.StartNew();
            // Allow a brief period so elapsed is non-zero
            System.Threading.Thread.SpinWait(1000);

            // Act
            var elapsedMs = FaesMetrics.StopAndGetElapsedMs(stopwatch);

            // Assert
            Assert.False(stopwatch.IsRunning);
            Assert.True(elapsedMs >= 0);
        }
    }
}
