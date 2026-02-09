using DWIS.RigOS.Common.Worker;

namespace DWIS.Service.WOBCorrections.TestSources
{
    public class ConfigurationSources : Configuration
    {
        public TimeSpan LoopDurationDownholeTelemetry { get; set; } = TimeSpan.FromSeconds(10);
    }
}
