using DWIS.Client.ReferenceImplementation.OPCFoundation;
using DWIS.RigOS.Common.Worker;
using DWIS.Service.WOBCorrections.Model;

namespace DWIS.Service.WOBCorrections.TestComposer
{
    public class Worker : DWISWorker<Configuration>
    {
        private AdvisorRecommendationsData AdvisorRecommendationsData { get; set; } = new AdvisorRecommendationsData();
        private ComposerRecommendationsData ComposerRecommendationsData { get; set; } = new ComposerRecommendationsData();

        public Worker(ILogger<IDWISWorker<Configuration>> logger, ILogger<DWISClientOPCF>? loggerDWISClient) : base(logger, loggerDWISClient)
        {
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ConnectToBlackboard();
            if (Configuration is not null && _DWISClient != null && _DWISClient.Connected)
            {
                await RegisterQueries(AdvisorRecommendationsData);
                await RegisterToBlackboard(ComposerRecommendationsData);
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
                    await ReadBlackboardAsync(AdvisorRecommendationsData, stoppingToken);
                    if (AdvisorRecommendationsData.WOBMaxLimit is not null && ComposerRecommendationsData.WOBRecommendedMaximum is not null)
                    {
                        ComposerRecommendationsData.WOBRecommendedMaximum.Value = AdvisorRecommendationsData.WOBMaxLimit.Value;
                    }
                    await PublishBlackboardAsync(ComposerRecommendationsData, stoppingToken);

                    lock (_lock)
                    {
                        if (Logger is not null && Logger.IsEnabled(LogLevel.Information))
                        {
                            if (AdvisorRecommendationsData.WOBMaxLimit is not null &&
                                AdvisorRecommendationsData.WOBMaxLimit.Value is not null)
                            {
                                Logger.LogInformation("Advisor Recommended Max WOB: " + AdvisorRecommendationsData.WOBMaxLimit.Value.Value.ToString("F3"));
                            }
                            if (ComposerRecommendationsData.WOBRecommendedMaximum is not null &&
                                ComposerRecommendationsData.WOBRecommendedMaximum.Value is not null)
                            {
                                Logger.LogInformation("Composer Recommended Max WOB: " + ComposerRecommendationsData.WOBRecommendedMaximum.Value.Value.ToString("F3"));
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
