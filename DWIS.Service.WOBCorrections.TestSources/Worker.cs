using DWIS.Client.ReferenceImplementation.OPCFoundation;
using DWIS.RigOS.Common.Worker;
using DWIS.Service.WOBCorrections.Model;
using OSDC.DotnetLibraries.General.Common;
using System.Reflection;

namespace DWIS.Service.WOBCorrections.TestSources
{
    public class Worker : DWISWorker<ConfigurationSources>
    {

        private TopSideMeasurementsData TopSideMeasurementsData { get; set; } = new TopSideMeasurementsData();
        private DownholeMeasurementsData DownholeMeasurementsData { get; set; } = new DownholeMeasurementsData();

        private TimeSpan LoopSpanDownholeTelemetry { get; set; } = TimeSpan.FromSeconds(10.0);

        public Worker(ILogger<IDWISWorker<ConfigurationSources>> logger, ILogger<DWISClientOPCF>? loggerDWISClient) : base(logger, loggerDWISClient)
        {
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ConnectToBlackboard();
            if (Configuration is not null && _DWISClient != null && _DWISClient.Connected)
            {
                LoopSpanDownholeTelemetry = Configuration.LoopDurationDownholeTelemetry;
                await RegisterToBlackboard(TopSideMeasurementsData);
                await RegisterToBlackboard(DownholeMeasurementsData);
                await Loop(stoppingToken);
            }
        }

        protected override async Task Loop(CancellationToken stoppingToken)
        {
            PeriodicTimer timer = new PeriodicTimer(LoopSpan);
            double downholeDuration = LoopSpanDownholeTelemetry.TotalSeconds;
            double mainDuration = LoopSpan.TotalSeconds;
            int count = 1;
            if (!Numeric.EQ(mainDuration, 0))
            {
                count = (int)(downholeDuration / mainDuration);
            }
            if (count <= 0)
            {
                count = 1;
            }
            int k = 0;
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    if (TopSideMeasurementsData.SurfaceWeightOnBit is not null)
                    {
                        TopSideMeasurementsData.SurfaceWeightOnBit.Value = _random.NextDouble();
                    }
                    lock (_lock)
                    {
                        if (Logger is not null && Logger.IsEnabled(LogLevel.Information) &&
                            TopSideMeasurementsData.SurfaceWeightOnBit is not null &&
                            TopSideMeasurementsData.SurfaceWeightOnBit.Value is not null)
                        {
                            Logger.LogInformation("Average Surface WOB: " + TopSideMeasurementsData.SurfaceWeightOnBit.Value.Value.ToString("F3"));
                        }
                    }
                    await PublishBlackboardAsync(TopSideMeasurementsData, stoppingToken);
                    k++;
                    if (k == count)
                    {
                        if (DownholeMeasurementsData.AverageRawWeight is not null)
                        {
                            DownholeMeasurementsData.AverageRawWeight.Value = _random.NextDouble();
                        }
                        await PublishBlackboardAsync(DownholeMeasurementsData, stoppingToken);
                        lock (_lock)
                        {
                            if (Logger is not null && Logger.IsEnabled(LogLevel.Information) &&
                                DownholeMeasurementsData.AverageRawWeight is not null &&
                                DownholeMeasurementsData.AverageRawWeight.Value is not null)
                            {
                                Logger.LogInformation("Average Raw Weight: " + DownholeMeasurementsData.AverageRawWeight.Value.Value.ToString("F3"));
                            }
                        }
                        k = 0;
                    }
                }
                catch (Exception e)
                {
                    Logger?.LogError(e.ToString());
                }
                ConfigurationUpdater<ConfigurationSources>.Instance.UpdateConfiguration(this);
            }
        }
    }
}
