using DWIS.Client.ReferenceImplementation.OPCFoundation;
using DWIS.RigOS.Common.Worker;
using DWIS.Service.WOBCorrections.Model;

namespace DWIS.Service.WOBCorrections.TestADCS
{
    public class Worker : DWISWorker<Configuration>
    {
        private ComposerRecommendationsData ComposerRecommendationsData { get; set; } = new ComposerRecommendationsData();
        private CorrectedRecommendationsData CorrectedRecommendationsData { get; set; } = new CorrectedRecommendationsData();

        public Worker(ILogger<IDWISWorker<Configuration>> logger, ILogger<DWISClientOPCF>? loggerDWISClient) : base(logger, loggerDWISClient)
        {
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ConnectToBlackboard();
            if (Configuration is not null && _DWISClient != null && _DWISClient.Connected)
            {
                await RegisterQueries(ComposerRecommendationsData);
                await RegisterQueries(CorrectedRecommendationsData);
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
                    await ReadBlackboardAsync(ComposerRecommendationsData, stoppingToken);
                    await ReadBlackboardAsync(CorrectedRecommendationsData, stoppingToken);

                    lock (_lock)
                    {
                        if (Logger is not null && Logger.IsEnabled(LogLevel.Information))
                        {
                            if (ComposerRecommendationsData.WOBRecommendedMaximum is not null &&
                                ComposerRecommendationsData.WOBRecommendedMaximum.Value is not null)
                            {
                                Logger.LogInformation("Composer Recommended Max WOB: " + ComposerRecommendationsData.WOBRecommendedMaximum.Value.Value.ToString("F3"));
                            }
                            if (CorrectedRecommendationsData.CorrectedWOBRecommendedMaximum is not null &&
                                CorrectedRecommendationsData.CorrectedWOBRecommendedMaximum.Value is not null)
                            {
                                Logger.LogInformation("Corrected Recommended Max WOB: " + CorrectedRecommendationsData.CorrectedWOBRecommendedMaximum.Value.Value.ToString("F3"));
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
