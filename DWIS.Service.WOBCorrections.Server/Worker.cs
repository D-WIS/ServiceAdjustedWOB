using DWIS.Client.ReferenceImplementation.OPCFoundation;
using DWIS.RigOS.Common.Worker;
using DWIS.Service.CorrectedSWOBCorrections.Model;
using DWIS.Service.WOBCorrections.Model;
using Microsoft.Extensions.Configuration;
using OSDC.DotnetLibraries.General.Common;
using System.ComponentModel;
using System.Reflection;

namespace DWIS.Service.WOBCorrections.Server
{
    public class Worker : DWISWorker<ConfigurationForWOBCorrection>
    {
        private TopSideMeasurementsData TopSideMeasurementsData { get; set; } = new TopSideMeasurementsData();
        private DownholeMeasurementsData DownholeMeasurementsData { get; set; } = new DownholeMeasurementsData();
        private ComposerRecommendationsData ComposerRecommendationsData { get; set; } = new ComposerRecommendationsData();
        private CorrectedMeasurementsData CorrectedMeasurementsData { get; set; } = new CorrectedMeasurementsData();
        private CorrectedRecommendationsData CorrectedRecommendationsData { get; set; } = new CorrectedRecommendationsData();


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
                await RegisterToBlackboard(CorrectedMeasurementsData);
                await RegisterToBlackboard(CorrectedRecommendationsData);
                await Loop(stoppingToken);
            }
        }

        protected override async Task Loop(CancellationToken stoppingToken)
        {
            PeriodicTimer timer = new PeriodicTimer(LoopSpan);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ReadBlackboardAsync(TopSideMeasurementsData, stoppingToken);
                    await ReadBlackboardAsync(DownholeMeasurementsData, stoppingToken);
                    await ReadBlackboardAsync(ComposerRecommendationsData, stoppingToken);
                    double sensorDistanceToBit = 2.0;
                    CalibratorCorrector.Process(Logger, DateTime.UtcNow, TopSideMeasurementsData, DownholeMeasurementsData, ComposerRecommendationsData, sensorDistanceToBit, Configuration, CorrectedMeasurementsData, CorrectedRecommendationsData);

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
                }
                catch (Exception e)
                {
                    Logger?.LogError(e.ToString());
                }
                ConfigurationUpdater<ConfigurationForWOBCorrection>.Instance.UpdateConfiguration(this);
            }
        }
    }
}
