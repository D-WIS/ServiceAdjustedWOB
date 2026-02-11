using DWIS.Client.ReferenceImplementation.OPCFoundation;
using DWIS.RigOS.Common.Worker;
using DWIS.Service.CorrectedSWOBCorrections.Model;
using DWIS.Service.WOBCorrections.MeasurementGeneration;
using DWIS.Service.WOBCorrections.Model;
using OSDC.DotnetLibraries.General.Common;

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
            int run = 1;
            var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Base physical model in SI units:
            // rho in kg/m3, Q in m3/s, omega in rad/s.
            const double odM = 0.1778; // 7 in
            const double idM = 0.0762; // 3 in
            double annularAreaM2 = Math.PI / 4.0 * (Math.Pow(odM, 2) - Math.Pow(idM, 2));

            // Hydrostatic-end-pressure term uses hydrostatic component only:
            // -(p_go*A_o - p_gi*A_i) with p_go ~= p_gi = rho*g*hp -> -rho*g*hp*(Ao-Ai)
            var baseModel = new PhysicalModel(Alpha: new AlphaModel(A0: 0.0,
                                                                    A1: 0.0,
                                                                    A2: -9.81 * annularAreaM2,
                                                                    A3: annularAreaM2, // Aeff from OD 7in / ID 3in
                                                                    A4: 4300.0), // jet reaction around 5kN at Q~2000 L/min
                                              Beta: new BetaModel(B0: 0.0,
                                                                  B1: 0.0,
                                                                  B2: annularAreaM2,
                                                                  B3: 4300.0,
                                                                  B4: 0.0),
                                              Hydraulics: new HydraulicConstants(DeltaPCoeff: 4.0e7));
            var sensotrArtifacts = new SensorModels(Bd: 0.0,
                                                C0: 250000.0, C1: 20000.0 / 30.0, C2: 0.0, C3: 0.0, C4: 0.0, C5: 0.0,
                                                D0: 300000.0, D1: -5000.0 / 30.0, D2: -10000.0);
            var scenario = new SimulationScenario("scenario", SensorToBitDistanceM: 0.0, baseModel, sensotrArtifacts);

            var random = new Random(20260209 + run * 100 + Math.Abs(scenario.Name.GetHashCode() % 1000));
            var state = new SimulatorState
            {
                Time = t0,
                ElapsedSeconds = 0.0,
                NextDownholeSeconds = 0.0,
                BitDepth = 800.0,
                HoleDepth = 800.0,
                BlockPosition = 30.0,
                FlowM3s = 0.0,
                OmegaRadPerSec = 0.0
            };

            var output = new SimulationOutput(scenario.Name, run, scenario.SensorToBitDistanceM, scenario.Model, scenario.Sensors);
            MeasurementGenerator.GenerateSeries(random, scenario, state, output);
            CalibratorCorrector.ResetState();
            var top = new TopSideMeasurementsData();
            var dh = new DownholeMeasurementsData();
            var composer = new ComposerRecommendationsData();
            var corrected = new CorrectedMeasurementsData();
            var correctedRec = new CorrectedRecommendationsData();

            var config = new ConfigurationForWOBCorrection();

            var tops = output.Topside.OrderBy(s => s.TimestampUtc).ToList();
            var dws = output.Downhole.OrderBy(s => s.TimestampUtc).ToList();
            if (tops.Count == 0 || dws.Count == 0) return;

            int dIdx = 0;
            var dCur = dws[0];


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
            int i = 0;
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var s = tops[i % tops.Count];
                while (dIdx + 1 < dws.Count && dws[dIdx + 1].TimestampUtc <= s.TimestampUtc)
                {
                    dIdx++;
                    dCur = dws[dIdx];
                }
                try
                {
                    if (TopSideMeasurementsData.BlockPosition is null )
                    {
                        TopSideMeasurementsData.BlockPosition = new ScalarProperty();
                    }
                    TopSideMeasurementsData.BlockPosition.Value = s.BlockPositionM;
                    if (TopSideMeasurementsData.BottomOfStringDepth is null)
                    {
                        TopSideMeasurementsData.BottomOfStringDepth = new ScalarProperty();
                    }
                    TopSideMeasurementsData.BottomOfStringDepth.Value = s.BitDepthM;
                    if (TopSideMeasurementsData.BottomHoleDepth is null)
                    {
                        TopSideMeasurementsData.BottomHoleDepth = new ScalarProperty();
                    }
                    TopSideMeasurementsData.BottomHoleDepth.Value = s.HoleDepthM;
                    if (TopSideMeasurementsData.BottomOfStringVerticalDepth is null)
                    {
                        TopSideMeasurementsData.BottomOfStringVerticalDepth = new ScalarProperty();
                    }
                    TopSideMeasurementsData.BottomOfStringVerticalDepth.Value = s.TvdAtBitM;
                    if (TopSideMeasurementsData.BottomOfStringInclination is null)
                    {
                        TopSideMeasurementsData.BottomOfStringInclination = new ScalarProperty();
                    }
                    TopSideMeasurementsData.BottomOfStringInclination.Value = s.InclinationRad;
                    if (TopSideMeasurementsData.FlowrateIn is null)
                    {
                        TopSideMeasurementsData.FlowrateIn = new ScalarProperty();
                    }
                    TopSideMeasurementsData.FlowrateIn.Value = s.FlowM3s;
                    if (TopSideMeasurementsData.DrillingFluidDensityIn is null)
                    {
                        TopSideMeasurementsData.DrillingFluidDensityIn = new ScalarProperty();
                    }
                    TopSideMeasurementsData.DrillingFluidDensityIn.Value = s.RhoKgM3;
                    if (TopSideMeasurementsData.MeasuredTensionInstrumentedSub is null)
                    {
                        TopSideMeasurementsData.MeasuredTensionInstrumentedSub = new ScalarProperty();
                    }
                    TopSideMeasurementsData.MeasuredTensionInstrumentedSub.Value = s.TdN;
                    if (TopSideMeasurementsData.HookLoadAtTopDrive is null)
                    {
                        TopSideMeasurementsData.HookLoadAtTopDrive = new ScalarProperty();
                    }
                    TopSideMeasurementsData.HookLoadAtTopDrive.Value = s.TpN;
                    if (TopSideMeasurementsData.HookLoadAtAnchor is null)
                    {
                        TopSideMeasurementsData.HookLoadAtAnchor = new ScalarProperty();
                    }
                    TopSideMeasurementsData.HookLoadAtAnchor.Value = s.TdlN;
                    if (TopSideMeasurementsData.SurfaceWeightOnBit is null)
                    {
                        TopSideMeasurementsData.SurfaceWeightOnBit = new ScalarProperty();
                    }
                    TopSideMeasurementsData.SurfaceWeightOnBit.Value = s.TpN;

                    if (DownholeMeasurementsData.AverageRawWeight is null)
                    {
                        DownholeMeasurementsData.AverageRawWeight = new ScalarProperty();
                    }
                    DownholeMeasurementsData.AverageRawWeight.Value = dCur.TensionBhaN;
                    if (DownholeMeasurementsData.StringPressure is null)
                    {
                        DownholeMeasurementsData.StringPressure = new ScalarProperty();
                    }
                    DownholeMeasurementsData.StringPressure.Value = dCur.PressureInsidePa;
                    if (DownholeMeasurementsData.AnnulusPressure is null)
                    {
                        DownholeMeasurementsData.AnnulusPressure = new ScalarProperty();
                    }
                    DownholeMeasurementsData.AnnulusPressure.Value = dCur.PressureAnnulusPa;
                    if (DownholeMeasurementsData.AverageRotationalSpeed is null)
                    {
                        DownholeMeasurementsData.AverageRotationalSpeed = new ScalarProperty();
                    }
                    DownholeMeasurementsData.AverageRotationalSpeed.Value = dCur.RotationRadPerSec;

                    await PublishBlackboardAsync(TopSideMeasurementsData, stoppingToken);
                    await PublishBlackboardAsync(DownholeMeasurementsData, stoppingToken);
                    lock (_lock)
                    {
                        if (Logger is not null && Logger.IsEnabled(LogLevel.Information) &&
                            TopSideMeasurementsData.BlockPosition is not null &&
                            TopSideMeasurementsData.BlockPosition.Value is not null)
                        {
                            Logger.LogInformation("Block position: " + TopSideMeasurementsData.BlockPosition.Value.Value.ToString("F3"));
                        }
                        if (Logger is not null && Logger.IsEnabled(LogLevel.Information) &&
                            TopSideMeasurementsData.SurfaceWeightOnBit is not null &&
                            TopSideMeasurementsData.SurfaceWeightOnBit.Value is not null)
                        {
                            Logger.LogInformation("Average Surface WOB: " + TopSideMeasurementsData.SurfaceWeightOnBit.Value.Value.ToString("F3"));
                        }
                        if (Logger is not null && Logger.IsEnabled(LogLevel.Information) &&
                            DownholeMeasurementsData.AverageRawWeight is not null &&
                            DownholeMeasurementsData.AverageRawWeight.Value is not null)
                        {
                            Logger.LogInformation("Average Raw Weight: " + DownholeMeasurementsData.AverageRawWeight.Value.Value.ToString("F3"));
                        }
                    }
                    lock (_lock)
                    {
                    }
                }
                catch (Exception e)
                {
                    Logger?.LogError(e.ToString());
                }
                i++;
                ConfigurationUpdater<ConfigurationSources>.Instance.UpdateConfiguration(this);
            }
        }
        static void SetScalar(object target, string propertyName, double value)
        {
            var p = target.GetType().GetProperty(propertyName) ?? throw new InvalidOperationException($"Property {propertyName} not found");
            var scalar = p.GetValue(target);
            if (scalar is null)
            {
                scalar = Activator.CreateInstance(p.PropertyType) ?? throw new InvalidOperationException($"Cannot create {p.PropertyType.Name}");
                p.SetValue(target, scalar);
            }
            var vp = scalar.GetType().GetProperty("Value") ?? throw new InvalidOperationException("Scalar has no Value");
            vp.SetValue(scalar, value);
        }
    }
}
