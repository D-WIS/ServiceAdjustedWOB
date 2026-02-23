using DWIS.Client.ReferenceImplementation.OPCFoundation;
using DWIS.RigOS.Common.Worker;
using DWIS.Service.CorrectedSWOBCorrections.Model;
using DWIS.Service.WOBCorrections.Model;
using DWIS.Service.WOBCorrections.ModelShared;
using Microsoft.Extensions.Configuration;
using OSDC.DotnetLibraries.General.Common;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace DWIS.Service.WOBCorrections.Server
{
    public class Worker : DWISWorker<ConfigurationForWOBCorrection>
    {
        private TopSideMeasurementsData TopSideMeasurementsData { get; set; } = new TopSideMeasurementsData();
        private DownholeMeasurementsData DownholeMeasurementsData { get; set; } = new DownholeMeasurementsData();
        private ComposerRecommendationsData ComposerRecommendationsData { get; set; } = new ComposerRecommendationsData();
        private CorrectedMeasurementsData CorrectedMeasurementsData { get; set; } = new CorrectedMeasurementsData();
        private CorrectedRecommendationsData CorrectedRecommendationsData { get; set; } = new CorrectedRecommendationsData();
        private BHADrillStringData BHADrillStringData { get; set; } = new BHADrillStringData();
        private readonly List<RealtimeDataSample> _processLog = new();
        private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true };
        private DateTimeOffset? _nextDumpUtc;

        public Worker(ILogger<IDWISWorker<ConfigurationForWOBCorrection>> logger, ILogger<DWISClientOPCF>? loggerDWISClient) : base(logger, loggerDWISClient)
        {
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ConnectToBlackboard();
            if (Configuration is not null && _DWISClient != null && _DWISClient.Connected)
            {
                await RegisterQueries(ComposerRecommendationsData);
                await RegisterQueries(DownholeMeasurementsData);
                await RegisterQueries(TopSideMeasurementsData);
                await RegisterQueries(BHADrillStringData);
                await RegisterToBlackboard(CorrectedMeasurementsData);
                await RegisterToBlackboard(CorrectedRecommendationsData);
                await Loop(stoppingToken);
            }
        }

        protected override async Task Loop(CancellationToken stoppingToken)
        {
            using PeriodicTimer timer = new PeriodicTimer(LoopSpan);
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    try
                    {
                        await ReadBlackboardAsync(BHADrillStringData, stoppingToken);
                        await ReadBlackboardAsync(TopSideMeasurementsData, stoppingToken);
                        await ReadBlackboardAsync(DownholeMeasurementsData, stoppingToken);
                        await ReadBlackboardAsync(ComposerRecommendationsData, stoppingToken);
                        double? sensorDistanceToBit = null;
                        if (BHADrillStringData.BHADrillString is not null &&
                            BHADrillStringData.BHADrillString.BHADrillString is not null)
                        {
                            var drillString = BHADrillStringData.BHADrillString.BHADrillString;
                            // search for the sensor in the drill string and get the distance to bit, if not found use default value
                            if (drillString.SensorsList is not null)
                            {
                                foreach (var sensor in drillString.SensorsList)
                                {
                                    if (sensor is not null && sensor.DistanceFromBit is not null && (sensor.SensorType & DrillStringSensorTypes.Tension) != 0)
                                    {
                                        sensorDistanceToBit = sensor.DistanceFromBit;
                                        break;
                                    }
                                }
                            }
                            if (sensorDistanceToBit is null && drillString.DrillStringSectionList is not null)
                            {
                                double dist = 0;
                                foreach (var section in drillString.DrillStringSectionList)
                                {
                                    if (section is not null && section.SectionComponentList is not null)
                                    {
                                        double dist2 = 0;
                                        foreach (var component in section.SectionComponentList)
                                        {
                                            if (component is not null && component.PartList is not null)
                                            {
                                                double dist3 = 0;
                                                foreach (var part in component.PartList)
                                                {
                                                    if (part is not null)
                                                    {
                                                        if (false)
                                                        {
                                                            dist += dist2 + dist3 + 0;
                                                            sensorDistanceToBit = dist;
                                                            break;
                                                        }
                                                        dist3 += part.TotalLength;
                                                    }
                                                }
                                                if (sensorDistanceToBit is not null)
                                                {
                                                    break;
                                                }
                                                dist2 += dist3;
                                            }
                                        }
                                        if (sensorDistanceToBit is not null)
                                        {
                                            break;
                                        }
                                        dist += section.Count * dist2;
                                    }
                                }
                            }
                        }
                        if (sensorDistanceToBit is null)
                        {
                            sensorDistanceToBit = 2.0;
                        }
                        CalibratorCorrector.Process(Logger, DateTime.UtcNow, TopSideMeasurementsData, DownholeMeasurementsData, ComposerRecommendationsData, sensorDistanceToBit.Value, Configuration, CorrectedMeasurementsData, CorrectedRecommendationsData);

                        await PublishBlackboardAsync(CorrectedMeasurementsData, stoppingToken);
                        await PublishBlackboardAsync(CorrectedRecommendationsData, stoppingToken);

                        lock (_lock)
                        {
                            if (Logger is not null && Logger.IsEnabled(LogLevel.Information))
                            {
                                if (TopSideMeasurementsData.SurfaceWeightOnBit is not null &&
                                    TopSideMeasurementsData.SurfaceWeightOnBit.Value is not null)
                                {
                                    Logger.LogInformation("Average Surface WOB: " + TopSideMeasurementsData.SurfaceWeightOnBit.Value.Value.ToString("F3"));
                                }
                                if (DownholeMeasurementsData.AverageRawWeight is not null &&
                                    DownholeMeasurementsData.AverageRawWeight.Value is not null)
                                {
                                    Logger.LogInformation("Average Downhole WOB: " + DownholeMeasurementsData.AverageRawWeight.Value.Value.ToString("F3"));

                                }
                                if (ComposerRecommendationsData.WOBRecommendedMaximum is not null &&
                                    ComposerRecommendationsData.WOBRecommendedMaximum.Value is not null)
                                {
                                    Logger.LogInformation("Composer Max WOB: " + ComposerRecommendationsData.WOBRecommendedMaximum.Value.Value.ToString("F3"));
                                }
                                if (CorrectedMeasurementsData.CorrectedSurfaceWeightOnBit is not null &&
                                    CorrectedMeasurementsData.CorrectedSurfaceWeightOnBit.Value is not null)
                                {
                                    Logger.LogInformation("Corrected WOB: " + CorrectedMeasurementsData.CorrectedSurfaceWeightOnBit.Value.Value.ToString("F3"));
                                }
                                if (CorrectedRecommendationsData.CorrectedWOBRecommendedMaximum is not null &&
                                    CorrectedRecommendationsData.CorrectedWOBRecommendedMaximum.Value is not null)
                                {
                                    Logger.LogInformation("Corrected Max WOB: " + CorrectedRecommendationsData.CorrectedWOBRecommendedMaximum.Value.Value.ToString("F3"));
                                }
                            }
                        }

                        await TryDumpProcessLogIfDueAsync(stoppingToken);
                    }
                    catch (Exception e)
                    {
                        Logger?.LogError(e.ToString());
                    }
                    ConfigurationUpdater<ConfigurationForWOBCorrection>.Instance.UpdateConfiguration(this);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }

            await ForceDumpProcessLogAsync();
        }

        private async Task TryDumpProcessLogIfDueAsync(CancellationToken cancellationToken)
        {
            if (Configuration is null || !Configuration.EnableRealtimeDataDump)
            {
                return;
            }

            TimeSpan interval = GetValidatedDumpInterval(Configuration.RealtimeDataDumpInterval);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            _processLog.Add(CreateSample(now));

            if (_nextDumpUtc is null)
            {
                _nextDumpUtc = GetNextBoundary(now, interval);
            }

            if (now < _nextDumpUtc)
            {
                return;
            }

            await DumpProcessLogAsync(interval, _nextDumpUtc.Value, cancellationToken);
            _processLog.Clear();
            _nextDumpUtc = GetNextBoundary(now, interval);
        }

        private async Task ForceDumpProcessLogAsync()
        {
            if (_processLog.Count == 0 || Configuration is null || !Configuration.EnableRealtimeDataDump)
            {
                return;
            }

            TimeSpan interval = GetValidatedDumpInterval(Configuration.RealtimeDataDumpInterval);
            DateTimeOffset dumpBoundary = _nextDumpUtc ?? DateTimeOffset.UtcNow;
            await DumpProcessLogAsync(interval, dumpBoundary, CancellationToken.None);
            _processLog.Clear();
        }

        private async Task DumpProcessLogAsync(TimeSpan interval, DateTimeOffset dumpBoundary, CancellationToken cancellationToken)
        {
            if (Configuration is null)
            {
                return;
            }

            string dumpDirectory = string.IsNullOrWhiteSpace(Configuration.RealtimeDataDumpDirectory) ? "/home" : Configuration.RealtimeDataDumpDirectory;
            Directory.CreateDirectory(dumpDirectory);

            var payload = new RealtimeDataDumpPayload
            {
                DumpTimestampUtc = DateTimeOffset.UtcNow,
                DumpInterval = interval,
                Samples = _processLog.ToArray()
            };

            string fileName = $"wobcorrections-realtime-{dumpBoundary:yyyyMMddTHHmmssZ}.json";
            string filePath = Path.Combine(dumpDirectory, fileName);
            string jsonPayload = JsonSerializer.Serialize(payload, _jsonSerializerOptions);
            await File.WriteAllTextAsync(filePath, jsonPayload, cancellationToken);

            Logger?.LogInformation("Realtime WOB process samples dumped to {FilePath} ({Count} samples).", filePath, payload.Samples.Length);
        }

        private static TimeSpan GetValidatedDumpInterval(TimeSpan interval)
        {
            if (interval <= TimeSpan.Zero)
            {
                return TimeSpan.FromHours(1);
            }

            return interval;
        }

        private static DateTimeOffset GetNextBoundary(DateTimeOffset now, TimeSpan interval)
        {
            long ticks = interval.Ticks;
            long nextTicks = ((now.UtcTicks / ticks) + 1) * ticks;
            return new DateTimeOffset(nextTicks, TimeSpan.Zero);
        }

        private RealtimeDataSample CreateSample(DateTimeOffset timestampUtc)
        {
            return new RealtimeDataSample
            {
                TimestampUtc = timestampUtc,
                TopSideMeasurementsData = JsonSerializer.SerializeToElement(TopSideMeasurementsData),
                DownholeMeasurementsData = JsonSerializer.SerializeToElement(DownholeMeasurementsData),
                ComposerRecommendationsData = JsonSerializer.SerializeToElement(ComposerRecommendationsData),
                CorrectedMeasurementsData = JsonSerializer.SerializeToElement(CorrectedMeasurementsData),
                CorrectedRecommendationsData = JsonSerializer.SerializeToElement(CorrectedRecommendationsData)
            };
        }

        private sealed class RealtimeDataDumpPayload
        {
            public DateTimeOffset DumpTimestampUtc { get; set; }
            public TimeSpan DumpInterval { get; set; }
            public RealtimeDataSample[] Samples { get; set; } = Array.Empty<RealtimeDataSample>();
        }

        private sealed class RealtimeDataSample
        {
            public DateTimeOffset TimestampUtc { get; set; }
            public JsonElement TopSideMeasurementsData { get; set; }
            public JsonElement DownholeMeasurementsData { get; set; }
            public JsonElement ComposerRecommendationsData { get; set; }
            public JsonElement CorrectedMeasurementsData { get; set; }
            public JsonElement CorrectedRecommendationsData { get; set; }
        }
    }
}
