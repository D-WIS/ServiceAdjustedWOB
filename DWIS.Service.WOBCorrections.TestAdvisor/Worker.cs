using DWIS.Client.ReferenceImplementation.OPCFoundation;
using DWIS.RigOS.Common.Worker;
using DWIS.Service.CorrectedSWOBCorrections.Model;
using DWIS.Service.WOBCorrections.Model;

namespace DWIS.Service.WOBCorrections.TestAdvisor
{
    public class Worker : DWISWorker<Configuration>
    {
        private TopSideMeasurementsAssignable TopSideMeasurementsData { get; set; } = new TopSideMeasurementsAssignable();
        private DownholeMeasurementsData DownholeMeasurementsData { get; set; } = new DownholeMeasurementsData();
        private CorrectedMeasurementsData CorrectedMeasurementsData { get; set; } = new CorrectedMeasurementsData();
        private AdvisorRecommendationsData AdvisorRecommendationsData { get; set; } = new AdvisorRecommendationsData();

        public Worker(ILogger<IDWISWorker<Configuration>> logger, ILogger<DWISClientOPCF>? loggerDWISClient) : base(logger, loggerDWISClient)
        {
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ConnectToBlackboard();
            if (Configuration is not null && _DWISClient != null && _DWISClient.Connected)
            {
                await RegisterQueries(TopSideMeasurementsData);
                await RegisterQueries(DownholeMeasurementsData);
                await RegisterQueries(CorrectedMeasurementsData);
                await RegisterToBlackboard(AdvisorRecommendationsData);
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
                    await ReadBlackboardAsync(CorrectedMeasurementsData, stoppingToken);
                    if (CorrectedMeasurementsData.CorrectedSurfaceWeightOnBit is not null && AdvisorRecommendationsData.WOBMaxLimit is not null)
                    {
                        AdvisorRecommendationsData.WOBMaxLimit.Value = 10.0 * CorrectedMeasurementsData.CorrectedSurfaceWeightOnBit.Value;
                    }
                    await PublishBlackboardAsync(AdvisorRecommendationsData, stoppingToken);
                    lock (_lock)
                    {
                        if (Logger is not null && Logger.IsEnabled(LogLevel.Information))
                        {
                            if (CorrectedMeasurementsData.CorrectedSurfaceWeightOnBit is not null &&
                                CorrectedMeasurementsData.CorrectedSurfaceWeightOnBit.Value is not null)
                            {
                                Logger.LogInformation("Corrected WOB: " + CorrectedMeasurementsData.CorrectedSurfaceWeightOnBit.Value.Value.ToString("F3"));
                            }
                            if (AdvisorRecommendationsData.WOBMaxLimit is not null &&
                                AdvisorRecommendationsData.WOBMaxLimit.Value is not null)
                            {
                                Logger.LogInformation("Advisor Recommended Max WOB: " + AdvisorRecommendationsData.WOBMaxLimit.Value.Value.ToString("F3"));
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger?.LogError(e.ToString());
                }
                ConfigurationUpdater<Configuration>.Instance.UpdateConfiguration(this);
            }
        }
    }
}
