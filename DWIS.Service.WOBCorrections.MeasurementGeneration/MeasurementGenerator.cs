namespace DWIS.Service.WOBCorrections.MeasurementGeneration
{
    public record SimulationScenario(string Name, double SensorToBitDistanceM, PhysicalModel Model, SensorModels Sensors);

    public class SimulatorState
    {
        public DateTime Time { get; set; }
        public double ElapsedSeconds { get; set; }
        public double NextDownholeSeconds { get; set; }
        public double BitDepth { get; set; }
        public double HoleDepth { get; set; }
        public double BlockPosition { get; set; }
        public double FlowM3s { get; set; }
        public double OmegaRadPerSec { get; set; }
    }
    public record SimulationOutput(
    string ScenarioName,
    int RunIndex,
    double SensorToBitDistanceM,
    PhysicalModel Model,
    SensorModels Sensors)
    {
        public List<SimTopsideSample> Topside { get; } = new();
        public List<SimDownholeSample> Downhole { get; } = new();
    }
    public record SimTopsideSample(
    DateTime TimestampUtc,
    double ElapsedSeconds,
    string Phase,
    bool Connected,
    double BlockPositionM,
    double FlowM3s,
    double RhoKgM3,
    double TvdAtBitM,
    double BitDepthM,
    double HoleDepthM,
    double InclinationRad,
    double RotationRadPerSec,
    double TrueWobN,
    double TdN,
    double TpN,
    double TdlN);

    public record SimDownholeSample(
        DateTime TimestampUtc,
        double ElapsedSeconds,
        string Phase,
        bool Connected,
        double TensionBhaN,
        double PressureInsidePa,
        double PressureAnnulusPa,
        double RotationRadPerSec);

    public record SensorModels(
    double Bd,
    double C0, double C1, double C2, double C3, double C4, double C5,
    double D0, double D1, double D2);

    public record HydraulicConstants(double DeltaPCoeff);

    public record AlphaModel(double A0, double A1, double A2, double A3, double A4);
    public record BetaModel(double B0, double B1, double B2, double B3, double B4);

    public record PhysicalModel(AlphaModel Alpha, BetaModel Beta, HydraulicConstants Hydraulics);

    public class MeasurementGenerator
    {
        public const double topSideHz = 1.0;
        public const double topSideDt = 1.0 / topSideHz;
        public const double downholeDt = 10.0; // 0.1 Hz

        public static void GenerateSeries(Random random, SimulationScenario scenario, SimulatorState state, SimulationOutput output)
        {
            const double initialOffSlipsHookLoadN = 250_000.0;
            int standIndex = 0;
            for (double standStart = 800.0; standStart < 1100.0; standStart += 30.0)
            {
                var standEnd = Math.Min(standStart + 30.0, 1100.0);
                double depthCenter = 0.5 * (standStart + standEnd);
                double startupFlow = Lerp(1500.0 / 60000.0, 2500.0 / 60000.0, (depthCenter - 800.0) / 300.0);
                double startupOmega = Lerp(RpmToRadPerSec(120.0), RpmToRadPerSec(180.0), (depthCenter - 800.0) / 300.0);
                double slipsSpeed = Lerp(0.3, 0.6, random.NextDouble());
                double standHookLoadOffsetN = initialOffSlipsHookLoadN + standIndex * 5_000.0;

                // Off-slip transition: raise block 0.2 m in ~2 s and retrieve hook-load support.
                SimulatePhase("off_slip", 2.0, 0.0, 0.0, 0.0, 0.0, connected: true, drilling: false, random, scenario, state, output, blockVelocity: +0.1, standHookLoadOffsetN: standHookLoadOffsetN);
                SimulatePhase("pump_startup", 120.0, 0.0, startupFlow, 0.0, 0.0, connected: true, drilling: false, random, scenario, state, output, standHookLoadOffsetN: standHookLoadOffsetN);
                SimulatePhase("rotation_startup", 60.0, startupFlow, startupFlow, 0.0, startupOmega, connected: true, drilling: false, random, scenario, state, output, standHookLoadOffsetN: standHookLoadOffsetN);
                SimulatePhase("tag_bottom", 900.0, startupFlow, startupFlow, startupOmega, startupOmega, connected: true, drilling: false, random, scenario, state, output, blockVelocity: -30.0 / 3600.0, standHookLoadOffsetN: standHookLoadOffsetN);
                SimulatePhase("drill", 1800.0, startupFlow, startupFlow + 120.0 / 60000.0, startupOmega, startupOmega + RpmToRadPerSec(10.0), connected: true, drilling: true, random, scenario, state, output, drillRate: 60.0 / 3600.0, flowOscAmp: 0.0, standHookLoadOffsetN: standHookLoadOffsetN);
                SimulatePhase("raise_1m", 120.0, startupFlow, startupFlow, startupOmega, startupOmega, connected: true, drilling: false, random, scenario, state, output, blockVelocity: +30.0 / 3600.0, standHookLoadOffsetN: standHookLoadOffsetN);
                SimulatePhase("ream_up", 100.0, startupFlow, startupFlow, startupOmega, startupOmega, connected: true, drilling: false, random, scenario, state, output, blockVelocity: +0.1, standHookLoadOffsetN: standHookLoadOffsetN);
                SimulatePhase("ream_down", 50.0, startupFlow - 400.0 / 60000.0, startupFlow - 400.0 / 60000.0, RpmToRadPerSec(90.0), RpmToRadPerSec(90.0), connected: true, drilling: false, random, scenario, state, output, blockVelocity: -0.2, standHookLoadOffsetN: standHookLoadOffsetN);
                SimulatePhase("friction_up", 20.0, startupFlow - 400.0 / 60000.0, startupFlow - 400.0 / 60000.0, 0.0, 0.0, connected: true, drilling: false, random, scenario, state, output, blockVelocity: +0.1, standHookLoadOffsetN: standHookLoadOffsetN);
                SimulatePhase("friction_down", 10.0, startupFlow - 400.0 / 60000.0, startupFlow - 400.0 / 60000.0, 0.0, 0.0, connected: true, drilling: false, random, scenario, state, output, blockVelocity: -0.2, standHookLoadOffsetN: standHookLoadOffsetN);

                // set_slips: around 2 s, lower block by 0.2 m and tension drops to near zero.
                SimulatePhase("set_slips", 2.0, 0.0, 0.0, 0.0, 0.0, connected: false, drilling: false, random, scenario, state, output, blockVelocity: -0.1);
                // connection: raise block to 30 m while disconnected.
                SimulatePhase("connection", Math.Max(5.0, (30.0 - state.BlockPosition) / slipsSpeed), 0.0, 0.0, 0.0, 0.0, connected: false, drilling: false, random, scenario, state, output, blockVelocity: slipsSpeed);
                SimulatePhase("wait_on_slips", 30.0, 0.0, 0.0, 0.0, 0.0, connected: false, drilling: false, random, scenario, state, output);

                state.BitDepth = Math.Min(state.BitDepth, standEnd);
                state.HoleDepth = Math.Max(state.HoleDepth, standEnd);
                standIndex++;
            }
        }

        static void SimulatePhase(string phase,
                                  double durationSeconds,
                                  double flowStartM3s,
                                  double flowEndM3s,
                                  double omegaStartRadPerSec,
                                  double omegaEndRadPerSec,
                                  bool connected,
                                  bool drilling,
                                  Random random,
                                  SimulationScenario scenario,
                                  SimulatorState state,
                                  SimulationOutput output,
                                  double blockVelocity = 0.0,
                                  double drillRate = 0.0,
                                  double flowOscAmp = 0.0,
                                  double standHookLoadOffsetN = 0.0)
        {
            int steps = Math.Max(1, (int)Math.Ceiling(durationSeconds / topSideDt));
            double tagCompressionM = 0.0;
            const double tagWobTargetN = 100_000.0;
            const double tagCompressionAtTargetM = 0.5;
            for (int i = 0; i < steps; i++)
            {
                double p = steps == 1 ? 1.0 : (double)i / (steps - 1);
                state.FlowM3s = Lerp(flowStartM3s, flowEndM3s, p) + flowOscAmp * Math.Sin(0.04 * state.ElapsedSeconds);
                state.OmegaRadPerSec = Lerp(omegaStartRadPerSec, omegaEndRadPerSec, p);
                state.BlockPosition = Math.Clamp(state.BlockPosition + blockVelocity * topSideDt, 0.0, 35.0);

                if (connected && !drilling && phase == "tag_bottom")
                {
                    // During tagging, bit depth increases only until touch-bottom.
                    // Additional downward block travel after touch is converted to axial compression/WOB.
                    double downMove = Math.Max(0.0, -blockVelocity * topSideDt);
                    double upMove = Math.Max(0.0, blockVelocity * topSideDt);
                    double gapToBottom = Math.Max(0.0, state.HoleDepth - state.BitDepth);

                    if (downMove > 0.0)
                    {
                        double closeGap = Math.Min(gapToBottom, downMove);
                        state.BitDepth = Math.Clamp(state.BitDepth + closeGap, 800.0, 1100.0);
                        double excessCompression = downMove - closeGap;
                        tagCompressionM = Math.Max(0.0, tagCompressionM + excessCompression);
                    }
                    else if (upMove > 0.0)
                    {
                        // If moved up, release compression first, then lift off bottom.
                        double release = Math.Min(tagCompressionM, upMove);
                        tagCompressionM -= release;
                        double remainingUp = upMove - release;
                        state.BitDepth = Math.Clamp(state.BitDepth - remainingUp, 800.0, 1100.0);
                    }
                }
                else if (connected && !drilling)
                {
                    state.BitDepth = Math.Clamp(state.BitDepth - blockVelocity * topSideDt, 800.0, 1100.0);
                }

                if (connected && drilling)
                {
                    // While drilling, block position follows the bit advance.
                    state.BlockPosition = Math.Clamp(state.BlockPosition - drillRate * topSideDt, 0.0, 30.0);
                    state.BitDepth = Math.Clamp(state.BitDepth + drillRate * topSideDt, 800.0, 1100.0);
                    // While drilling, hole depth follows bit depth.
                    state.HoleDepth = state.BitDepth;
                }

                double inclRad = InclinationRad(state.BitDepth);
                double rho = DensityKgM3(state.BitDepth);
                double h = state.BitDepth * Math.Cos(inclRad);
                double hp = h - scenario.SensorToBitDistanceM * Math.Cos(inclRad);
                double l = state.BitDepth;
                double q = Math.Max(0.0, state.FlowM3s);

                double hydrostaticComponentPa = Math.Max(0.0, rho * 9.81 * hp);
                double hydrostaticTotalPa = 101325.0 + hydrostaticComponentPa;
                bool circulating = q > 1e-6;
                double pa = circulating ? hydrostaticTotalPa + 10.0 * 100000.0 : hydrostaticTotalPa;
                double pi = circulating ? hydrostaticTotalPa + 70.0 * 100000.0 : hydrostaticTotalPa;

                double alphaPred =
                    scenario.Model.Alpha.A0 * Math.Cos(inclRad) +
                    scenario.Model.Alpha.A1 * rho * Math.Cos(inclRad) +
                    scenario.Model.Alpha.A2 * rho * hp +
                    scenario.Model.Alpha.A3 * (pi - pa) +
                    scenario.Model.Alpha.A4 * rho * q * q;

                double betaTerm =
                    scenario.Model.Beta.B0 * h +
                    scenario.Model.Beta.B1 * rho * h +
                    scenario.Model.Beta.B2 * (pi - pa) +
                    scenario.Model.Beta.B3 * rho * q * q +
                    scenario.Model.Beta.B4 * rho * l * q * q;

                // Requested: apply about 100 kN surface drop during drilling,
                // and unload it linearly during raise_1m over the first 0.5 m hoisting distance.
                double wobN = 0.0;
                if (connected && phase == "drill")
                {
                    wobN = 100_000.0;
                }
                else if (connected && phase == "tag_bottom")
                {
                    // During tagging, WOB only builds after touching bottom.
                    wobN = tagWobTargetN * Math.Clamp(tagCompressionM / tagCompressionAtTargetM, 0.0, 1.0);
                }
                else if (connected && phase == "raise_1m")
                {
                    double liftedDistanceM = Math.Max(0.0, blockVelocity) * durationSeconds * p;
                    double unloadFraction = Math.Clamp(liftedDistanceM / 0.5, 0.0, 1.0);
                    wobN = 100_000.0 * (1.0 - unloadFraction);
                }

                double commonNoise = NextNoise(random, connected ? 180.0 : 120.0);
                double tBha = alphaPred - wobN + commonNoise;
                // During off_slip, support transfer from slips to hoisting should ramp the full transmitted tension.
                double supportFactor = (phase == "off_slip") ? p : 1.0;
                double transmittedTrue = betaTerm - wobN + standHookLoadOffsetN;
                double tTrue = connected ? supportFactor * transmittedTrue : 0.0;

                double signVz = Math.Sign(blockVelocity);
                double fp =
                    scenario.Sensors.C0 +
                    scenario.Sensors.C1 * state.BlockPosition +
                    scenario.Sensors.C2 * state.BlockPosition * state.BlockPosition +
                    scenario.Sensors.C3 * q +
                    scenario.Sensors.C4 * q * q +
                    scenario.Sensors.C5 * state.BlockPosition * q;
                double fdl =
                    scenario.Sensors.D0 +
                    scenario.Sensors.D1 * state.BlockPosition +
                    scenario.Sensors.D2 * signVz;

                double td = connected ? tTrue + scenario.Sensors.Bd + NextNoise(random, 140.0) : scenario.Sensors.Bd + NextNoise(random, 120.0);
                double tp = connected ? tTrue + fp + NextNoise(random, 160.0) : fp + NextNoise(random, 130.0);
                double tdl = connected ? tTrue + fdl + NextNoise(random, 160.0) : fdl + NextNoise(random, 130.0);
                output.Topside.Add(new SimTopsideSample(
                    TimestampUtc: state.Time,
                    ElapsedSeconds: state.ElapsedSeconds,
                    Phase: phase,
                    Connected: connected,
                    BlockPositionM: state.BlockPosition,
                    FlowM3s: q,
                    RhoKgM3: rho,
                    TvdAtBitM: h,
                    BitDepthM: state.BitDepth,
                    HoleDepthM: state.HoleDepth,
                    InclinationRad: inclRad,
                    RotationRadPerSec: state.OmegaRadPerSec,
                    TrueWobN: wobN,
                    TdN: td,
                    TpN: tp,
                    TdlN: tdl));

                if (state.ElapsedSeconds + 1e-9 >= state.NextDownholeSeconds)
                {
                    output.Downhole.Add(new SimDownholeSample(
                        TimestampUtc: state.Time,
                        ElapsedSeconds: state.ElapsedSeconds,
                        Phase: phase,
                        Connected: connected,
                        TensionBhaN: tBha,
                        PressureInsidePa: pi,
                        PressureAnnulusPa: pa,
                        RotationRadPerSec: state.OmegaRadPerSec));
                    state.NextDownholeSeconds += downholeDt;
                }

                state.ElapsedSeconds += topSideDt;
                state.Time = state.Time.AddSeconds(topSideDt);

                if (phase == "tag_bottom" && wobN >= tagWobTargetN - 1e-9)
                {
                    // End tag phase as soon as target WOB is reached.
                    break;
                }
            }
        }
        static double Lerp(double a, double b, double t) => a + (b - a) * Math.Clamp(t, 0.0, 1.0);
        static double NextNoise(Random r, double sigma) => (r.NextDouble() * 2.0 - 1.0) * sigma;
        static double DensityKgM3(double bitDepthM) => Lerp(1010.0, 1100.0, (bitDepthM - 800.0) / 300.0);
        static double InclinationRad(double bitDepthM) => Lerp(24.0, 20.0, (bitDepthM - 800.0) / 300.0) * Math.PI / 180.0;
        static double RpmToRadPerSec(double rpm) => rpm * 2.0 * Math.PI / 60.0;

    }
}
