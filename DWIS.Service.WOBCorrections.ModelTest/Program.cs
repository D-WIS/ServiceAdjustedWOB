using System.Text.Json;
using System.Globalization;
using System.Reflection;
using DWIS.Service.CorrectedSWOBCorrections.Model;
using DWIS.Service.WOBCorrections.Model;

const double topSideHz = 1.0;
const double topSideDt = 1.0 / topSideHz;
const double downholeDt = 10.0; // 0.1 Hz
const int runsPerScenario = 1;
var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

// Base physical model in SI units:
// rho in kg/m3, Q in m3/s, omega in rad/s.
const double odM = 0.1778; // 7 in
const double idM = 0.0762; // 3 in
double annularAreaM2 = Math.PI / 4.0 * (Math.Pow(odM, 2) - Math.Pow(idM, 2));

var baseModel = new PhysicalModel(
    Alpha: new AlphaModel(
        A0: 0.0,
        A1: 0.0,
        // Hydrostatic-end-pressure term uses hydrostatic component only:
        // -(p_go*A_o - p_gi*A_i) with p_go ~= p_gi = rho*g*hp -> -rho*g*hp*(Ao-Ai)
        A2: -9.81 * annularAreaM2,
        A3: annularAreaM2, // Aeff from OD 7in / ID 3in
        A4: 4300.0), // jet reaction around 5kN at Q~2000 L/min
    Beta: new BetaModel(
        B0: 0.0,
        B1: 0.0,
        B2: annularAreaM2,
        B3: 4300.0,
        B4: 0.0),
    Gamma: new GammaModel(
        G1: 0.0,
        G2: 0.0,
        G3: 0.0),
    Hydraulics: new HydraulicConstants(
        DeltaPCoeff: 4.0e7));

var noArtifacts = new SensorModels(
    Bd: 0.0,
    C0: 0.0, C1: 0.0, C2: 0.0, C3: 0.0, C4: 0.0, C5: 0.0,
    D0: 0.0, D1: 0.0, D2: 0.0);

var deadlineOnly = new SensorModels(
    Bd: 0.0,
    C0: 0.0, C1: 0.0, C2: 0.0, C3: 0.0, C4: 0.0, C5: 0.0,
    D0: 300000.0, D1: -5000.0 / 30.0, D2: -10000.0);

var loadPinsOnly = new SensorModels(
    Bd: 0.0,
    C0: 250000.0, C1: 20000.0 / 30.0, C2: 0.0, C3: 0.0, C4: 0.0, C5: 0.0,
    D0: 0.0, D1: 0.0, D2: 0.0);

var scenarios = new List<SimulationScenario>
{
    // 1) No artifacts on Td/Tp/Tdl, downhole sensor exactly at bit (lp = 0).
    new("series_01_no_artifacts_at_bit_sensor", SensorToBitDistanceM: 0.0, baseModel, noArtifacts),
    // 2) Only deadline artifact enabled.
    new("series_02_deadline_artifact_only", SensorToBitDistanceM: 0.0, baseModel, deadlineOnly),
    // 3) Only load-pin artifact enabled.
    new("series_03_loadpin_artifact_only", SensorToBitDistanceM: 0.0, baseModel, loadPinsOnly),
};

var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
for (int run = 1; run <= runsPerScenario; run++)
{
    foreach (var scenario in scenarios)
    {
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
        GenerateSeries(random, scenario, state, output);

        string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        string fileName = $"{scenario.Name}_run{run:00}_{stamp}.json";
        string path = Path.Combine(AppContext.BaseDirectory, fileName);
        File.WriteAllText(path, JsonSerializer.Serialize(output, jsonOptions));
        string topsideCsv = Path.Combine(AppContext.BaseDirectory, $"{scenario.Name}_run{run:00}_{stamp}_topside_1s.csv");
        string downholeCsv = Path.Combine(AppContext.BaseDirectory, $"{scenario.Name}_run{run:00}_{stamp}_downhole_10s.csv");
        string offBottomBhaCsv = Path.Combine(AppContext.BaseDirectory, $"{scenario.Name}_run{run:00}_{stamp}_offbottom_rotating_bha.csv");
        string alphaDiffCsv = Path.Combine(AppContext.BaseDirectory, $"{scenario.Name}_run{run:00}_{stamp}_alpha_fit_diff.csv");
        string alphaStreamDiffCsv = Path.Combine(AppContext.BaseDirectory, $"{scenario.Name}_run{run:00}_{stamp}_alpha_stream_fit_diff.csv");
        string offBottomGammaCsv = Path.Combine(AppContext.BaseDirectory, $"{scenario.Name}_run{run:00}_{stamp}_offbottom_rotating_gamma.csv");
        string gammaDiffCsv = Path.Combine(AppContext.BaseDirectory, $"{scenario.Name}_run{run:00}_{stamp}_gamma_fit_diff.csv");
        string offBottomGammaTpCsv = Path.Combine(AppContext.BaseDirectory, $"{scenario.Name}_run{run:00}_{stamp}_offbottom_rotating_gamma_tp.csv");
        string gammaTpDiffCsv = Path.Combine(AppContext.BaseDirectory, $"{scenario.Name}_run{run:00}_{stamp}_gamma_tp_fit_diff.csv");
        string offBottomGammaTdlCsv = Path.Combine(AppContext.BaseDirectory, $"{scenario.Name}_run{run:00}_{stamp}_offbottom_rotating_gamma_tdl.csv");
        string gammaTdlDiffCsv = Path.Combine(AppContext.BaseDirectory, $"{scenario.Name}_run{run:00}_{stamp}_gamma_tdl_fit_diff.csv");
        string offBottomBetaTdCsv = Path.Combine(AppContext.BaseDirectory, $"{scenario.Name}_run{run:00}_{stamp}_offbottom_rotating_beta_td.csv");
        string betaTdStreamDiffCsv = Path.Combine(AppContext.BaseDirectory, $"{scenario.Name}_run{run:00}_{stamp}_beta_td_stream_fit_diff.csv");
        string offBottomBetaTpCsv = Path.Combine(AppContext.BaseDirectory, $"{scenario.Name}_run{run:00}_{stamp}_offbottom_rotating_beta_tp.csv");
        string betaTpStreamDiffCsv = Path.Combine(AppContext.BaseDirectory, $"{scenario.Name}_run{run:00}_{stamp}_beta_tp_stream_fit_diff.csv");
        string offBottomBetaTdlCsv = Path.Combine(AppContext.BaseDirectory, $"{scenario.Name}_run{run:00}_{stamp}_offbottom_rotating_beta_tdl.csv");
        string betaTdlStreamDiffCsv = Path.Combine(AppContext.BaseDirectory, $"{scenario.Name}_run{run:00}_{stamp}_beta_tdl_stream_fit_diff.csv");
        string slipsCsv = Path.Combine(AppContext.BaseDirectory, $"{scenario.Name}_run{run:00}_{stamp}_in_slips_contexts.csv");
        string dStreamDiffCsv = Path.Combine(AppContext.BaseDirectory, $"{scenario.Name}_run{run:00}_{stamp}_d_stream_fit_diff.csv");
        string slipsTpCsv = Path.Combine(AppContext.BaseDirectory, $"{scenario.Name}_run{run:00}_{stamp}_in_slips_tp_contexts.csv");
        string cStreamDiffCsv = Path.Combine(AppContext.BaseDirectory, $"{scenario.Name}_run{run:00}_{stamp}_c_stream_fit_diff.csv");
        WriteTopsideCsv1s(output.Topside, topsideCsv);
        WriteDownholeCsv10s(output.Downhole, downholeCsv);
        var alphaSituations = WriteOffBottomRotatingBhaCsv(output, offBottomBhaCsv);
        var alphaFit = CalibratorCorrector.CalibrateAlphaFromSituations(alphaSituations);
        WriteAlphaFitDifferencesCsv(alphaSituations, alphaFit, alphaDiffCsv);
        var alphaStreamFit = CalibratorCorrector.CalibrateAlphaStreamingAdaptive(alphaSituations);
        WriteAlphaStreamingFitDifferencesCsv(alphaStreamFit, alphaStreamDiffCsv);
        var gammaSituations = WriteOffBottomRotatingGammaCsv(output, offBottomGammaCsv);
        var gammaFit = CalibratorCorrector.CalibrateGammaFromSituations(gammaSituations);
        WriteGammaFitDifferencesCsv(gammaSituations, gammaFit, gammaDiffCsv);
        var gammaTpSituations = WriteOffBottomRotatingGammaTpCsv(output, offBottomGammaTpCsv);
        var gammaTpFit = CalibratorCorrector.CalibrateGammaFromTpSituations(gammaTpSituations);
        WriteGammaTpFitDifferencesCsv(gammaTpSituations, gammaTpFit, gammaTpDiffCsv);
        var gammaTdlSituations = WriteOffBottomRotatingGammaTdlCsv(output, offBottomGammaTdlCsv);
        var gammaTdlFit = CalibratorCorrector.CalibrateGammaFromTdlSituations(gammaTdlSituations);
        WriteGammaTdlFitDifferencesCsv(gammaTdlSituations, gammaTdlFit, gammaTdlDiffCsv);
        var betaTdSituations = WriteOffBottomRotatingBetaCsv(output, offBottomBetaTdCsv, "td");
        var betaTdStream = WriteBetaStreamingFitDifferencesCsv(betaTdSituations, betaTdStreamDiffCsv);
        var betaTpSituations = WriteOffBottomRotatingBetaCsv(output, offBottomBetaTpCsv, "tp");
        var betaTpStream = WriteBetaStreamingFitDifferencesCsv(betaTpSituations, betaTpStreamDiffCsv);
        var betaTdlSituations = WriteOffBottomRotatingBetaCsv(output, offBottomBetaTdlCsv, "tdl");
        var betaTdlStream = WriteBetaStreamingFitDifferencesCsv(betaTdlSituations, betaTdlStreamDiffCsv);
        var dSituations = WriteInSlipsContextsCsv(output, slipsCsv);
        var dStream = WriteDStreamingFitDifferencesCsv(dSituations, dStreamDiffCsv);
        var cSituations = WriteInSlipsTpContextsCsv(output, slipsTpCsv);
        var cStream = WriteCStreamingFitDifferencesCsv(cSituations, cStreamDiffCsv);
        string wobLogCsv = Path.Combine(AppContext.BaseDirectory, $"{scenario.Name}_run{run:00}_{stamp}_wob_log.csv");
        ReplayCalibration(output, wobLogCsv);

        Console.WriteLine($"{scenario.Name} run {run:00}");
        Console.WriteLine($"  topside samples : {output.Topside.Count}");
        Console.WriteLine($"  downhole samples: {output.Downhole.Count}");
        Console.WriteLine($"  file: {path}");
        Console.WriteLine($"  topside csv (1s): {topsideCsv}");
        Console.WriteLine($"  downhole csv (10s): {downholeCsv}");
        Console.WriteLine($"  off-bottom rotating BHA csv: {offBottomBhaCsv}");
        Console.WriteLine($"  off-bottom rotating gamma csv: {offBottomGammaCsv}");
        Console.WriteLine($"  off-bottom rotating gamma Tp csv: {offBottomGammaTpCsv}");
        Console.WriteLine($"  off-bottom rotating gamma Tdl csv: {offBottomGammaTdlCsv}");
        Console.WriteLine($"  off-bottom rotating beta Td csv: {offBottomBetaTdCsv}");
        Console.WriteLine($"  off-bottom rotating beta Tp csv: {offBottomBetaTpCsv}");
        Console.WriteLine($"  off-bottom rotating beta Tdl csv: {offBottomBetaTdlCsv}");
        Console.WriteLine($"  in-slips contexts csv: {slipsCsv}");
        Console.WriteLine($"  in-slips Tp contexts csv: {slipsTpCsv}");
        Console.WriteLine($"  alpha fit diff csv: {alphaDiffCsv}");
        Console.WriteLine($"  alpha stream fit diff csv: {alphaStreamDiffCsv}");
        Console.WriteLine($"  gamma fit diff csv: {gammaDiffCsv}");
        Console.WriteLine($"  gamma Tp fit diff csv: {gammaTpDiffCsv}");
        Console.WriteLine($"  gamma Tdl fit diff csv: {gammaTdlDiffCsv}");
        Console.WriteLine($"  beta Td stream fit diff csv: {betaTdStreamDiffCsv}");
        Console.WriteLine($"  beta Tp stream fit diff csv: {betaTpStreamDiffCsv}");
        Console.WriteLine($"  beta Tdl stream fit diff csv: {betaTdlStreamDiffCsv}");
        Console.WriteLine($"  d stream fit diff csv: {dStreamDiffCsv}");
        Console.WriteLine($"  c stream fit diff csv: {cStreamDiffCsv}");
        Console.WriteLine($"  alpha fit: n={alphaFit.Count} meanErr={alphaFit.MeanError:G6} stdErr={alphaFit.StdError:G6} rmse={alphaFit.Rmse:G6}");
        Console.WriteLine($"  alpha stream fit: n={alphaStreamFit.Count} meanErr={alphaStreamFit.MeanError:G6} stdErr={alphaStreamFit.StdError:G6} rmse={alphaStreamFit.Rmse:G6}");
        Console.WriteLine($"  gamma fit: n={gammaFit.Count} meanErr={gammaFit.MeanError:G6} stdErr={gammaFit.StdError:G6} rmse={gammaFit.Rmse:G6}");
        Console.WriteLine($"  gamma Tp fit: n={gammaTpFit.Count} meanErr={gammaTpFit.MeanError:G6} stdErr={gammaTpFit.StdError:G6} rmse={gammaTpFit.Rmse:G6}");
        Console.WriteLine($"  gamma Tdl fit: n={gammaTdlFit.Count} meanErr={gammaTdlFit.MeanError:G6} stdErr={gammaTdlFit.StdError:G6} rmse={gammaTdlFit.Rmse:G6}");
        Console.WriteLine($"  beta Td stream fit: n={betaTdStream.Count} meanErr={betaTdStream.MeanError:G6} stdErr={betaTdStream.StdError:G6} rmse={betaTdStream.Rmse:G6}");
        Console.WriteLine($"  beta Tp stream fit: n={betaTpStream.Count} meanErr={betaTpStream.MeanError:G6} stdErr={betaTpStream.StdError:G6} rmse={betaTpStream.Rmse:G6}");
        Console.WriteLine($"  beta Tdl stream fit: n={betaTdlStream.Count} meanErr={betaTdlStream.MeanError:G6} stdErr={betaTdlStream.StdError:G6} rmse={betaTdlStream.Rmse:G6}");
        Console.WriteLine($"  d stream fit: n={dStream.Count} meanErr={dStream.MeanError:G6} stdErr={dStream.StdError:G6} rmse={dStream.Rmse:G6}");
        Console.WriteLine($"  c stream fit: n={cStream.Count} meanErr={cStream.MeanError:G6} stdErr={cStream.StdError:G6} rmse={cStream.Rmse:G6}");
        Console.WriteLine($"  wob log csv: {wobLogCsv}");
    }
}

static void GenerateSeries(Random random, SimulationScenario scenario, SimulatorState state, SimulationOutput output)
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

static void SimulatePhase(
    string phase,
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

        // Requested behavior: downhole at bit with lp=0 should remove weight/inclination influence by model setup.
        double gammaTerm =
            scenario.Model.Gamma.G1 * hp +
            scenario.Model.Gamma.G2 * rho * hp +
            scenario.Model.Gamma.G3 * rho * (l - scenario.SensorToBitDistanceM) * q * q;

        // Requested: apply about 100 kN surface drop during drilling,
        // and unload it linearly during raise_1m over the first 0.5 m hoisting distance.
        double wobAtBitN = 0.0;
        if (connected && phase == "drill")
        {
            wobAtBitN = 100_000.0;
        }
        else if (connected && phase == "tag_bottom")
        {
            // During tagging, WOB only builds after touching bottom.
            wobAtBitN = tagWobTargetN * Math.Clamp(tagCompressionM / tagCompressionAtTargetM, 0.0, 1.0);
        }
        else if (connected && phase == "raise_1m")
        {
            double liftedDistanceM = Math.Max(0.0, blockVelocity) * durationSeconds * p;
            double unloadFraction = Math.Clamp(liftedDistanceM / 0.5, 0.0, 1.0);
            wobAtBitN = 100_000.0 * (1.0 - unloadFraction);
        }

        double commonNoise = NextNoise(random, connected ? 180.0 : 120.0);
        double tBha = alphaPred - wobAtBitN + commonNoise;
        // During off_slip, support transfer from slips to hoisting should ramp the full transmitted tension.
        double supportFactor = (phase == "off_slip") ? p : 1.0;
        double transmittedTrue = tBha + gammaTerm + standHookLoadOffsetN;
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
            TrueWobN: wobAtBitN,
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

        if (phase == "tag_bottom" && wobAtBitN >= tagWobTargetN - 1e-9)
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

static void WriteTopsideCsv1s(IReadOnlyList<SimTopsideSample> samples, string path)
{
    var no = NorwegianCsvCulture();
    using var sw = new StreamWriter(path, false);
    sw.WriteLine("TimestampUtc;ElapsedSeconds;Phase;Connected;BlockPositionM;FlowLpm;RhoKgM3;TvdAtBitM;BitDepthM;HoleDepthM;InclinationRad;RotationRpm;TdKdaN;TpKdaN;TdlKdaN");
    if (samples.Count == 0) return;

    int idx = 0;
    int maxSecond = (int)Math.Floor(samples[^1].ElapsedSeconds);
    for (int sec = 0; sec <= maxSecond; sec++)
    {
        while (idx + 1 < samples.Count && samples[idx + 1].ElapsedSeconds <= sec) idx++;
        var s = samples[idx];
        sw.WriteLine(string.Join(";",
            s.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
            F(s.ElapsedSeconds, no),
            s.Phase,
            s.Connected ? "1" : "0",
            F(s.BlockPositionM, no),
            F(ToLpm(s.FlowM3s), no),
            F(s.RhoKgM3, no),
            F(s.TvdAtBitM, no),
            F(s.BitDepthM, no),
            F(s.HoleDepthM, no),
            F(s.InclinationRad, no),
            F(ToRpm(s.RotationRadPerSec), no),
            F(ToKdaN(s.TdN), no),
            F(ToKdaN(s.TpN), no),
            F(ToKdaN(s.TdlN), no)));
    }
}

static void WriteDownholeCsv10s(IReadOnlyList<SimDownholeSample> samples, string path)
{
    var no = NorwegianCsvCulture();
    using var sw = new StreamWriter(path, false);
    sw.WriteLine("TimestampUtc;ElapsedSeconds;Phase;Connected;TensionBhaKdaN;PressureInsideBar;PressureAnnulusBar;RotationRpm");
    foreach (var s in samples)
    {
        sw.WriteLine(string.Join(";",
            s.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
            F(s.ElapsedSeconds, no),
            s.Phase,
            s.Connected ? "1" : "0",
            F(ToKdaN(s.TensionBhaN), no),
            F(ToBar(s.PressureInsidePa), no),
            F(ToBar(s.PressureAnnulusPa), no),
            F(ToRpm(s.RotationRadPerSec), no)));
    }
}

static List<CalibratorCorrector.AlphaCalibrationSituation> WriteOffBottomRotatingBhaCsv(SimulationOutput output, string path)
{
    var no = NorwegianCsvCulture();
    using var sw = new StreamWriter(path, false);
    sw.WriteLine("TimestampUtc;ElapsedSeconds;TBhaN;PiPa;PaPa;HpM;RhoKgM3;ThetaRad;Qm3s");
    var situations = new List<CalibratorCorrector.AlphaCalibrationSituation>();

    if (output.Topside.Count == 0 || output.Downhole.Count == 0) return situations;

    // Use same thresholds as calibration defaults.
    double depthMargin = ConfigurationForWOBCorrection.DepthMarginDefault;
    double minOmega = ConfigurationForWOBCorrection.MinDownholeRotationalSpeedDefault;

    var tops = output.Topside.OrderBy(s => s.TimestampUtc).ToList();
    var dws = output.Downhole.OrderBy(s => s.TimestampUtc).ToList();

    int tIdx = 0;
    double? lastOmega = null;
    DateTime? lastOmegaChangeTime = null;
    foreach (var d in dws)
    {
        while (tIdx + 1 < tops.Count && tops[tIdx + 1].TimestampUtc <= d.TimestampUtc) tIdx++;
        var t = tops[tIdx];

        // Enforce a settling delay after any rotational-speed change.
        if (lastOmega.HasValue && Math.Abs(d.RotationRadPerSec - lastOmega.Value) > 1e-9)
        {
            lastOmegaChangeTime = d.TimestampUtc;
        }
        lastOmega = d.RotationRadPerSec;

        bool offBottom = t.BitDepthM <= t.HoleDepthM - depthMargin;
        bool rotating = d.RotationRadPerSec >= minOmega;
        bool passedRefreshDelay =
            !lastOmegaChangeTime.HasValue ||
            (d.TimestampUtc - lastOmegaChangeTime.Value).TotalSeconds >= downholeDt;
        if (!offBottom || !rotating || !passedRefreshDelay) continue;

        double theta = t.InclinationRad;
        double hp = t.TvdAtBitM - output.SensorToBitDistanceM * Math.Cos(theta);

        sw.WriteLine(string.Join(";",
            d.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
            F(d.ElapsedSeconds, no),
            F(d.TensionBhaN, no),
            F(d.PressureInsidePa, no),
            F(d.PressureAnnulusPa, no),
            F(hp, no),
            F(t.RhoKgM3, no),
            F(theta, no),
            F(t.FlowM3s, no)));

        situations.Add(new CalibratorCorrector.AlphaCalibrationSituation(
            Time: d.TimestampUtc,
            TBha: d.TensionBhaN,
            Pi: d.PressureInsidePa,
            Pa: d.PressureAnnulusPa,
            Hp: hp,
            Rho: t.RhoKgM3,
            Theta: theta,
            Q: t.FlowM3s));
    }

    return situations;
}

static void WriteAlphaFitDifferencesCsv(
    IReadOnlyList<CalibratorCorrector.AlphaCalibrationSituation> situations,
    CalibratorCorrector.AlphaCalibrationResult fit,
    string path)
{
    var no = NorwegianCsvCulture();
    using var sw = new StreamWriter(path, false);
    sw.WriteLine("TimestampUtc;TBhaN;PredictedTBhaN;ErrorN;PiPa;PaPa;HpM;RhoKgM3;ThetaRad;Qm3s");
    if (situations.Count == 0) return;

    foreach (var s in situations)
    {
        double cosTheta = Math.Cos(s.Theta);
        double pred =
            fit.A0 * cosTheta +
            fit.A1 * s.Rho * cosTheta +
            fit.A2 * s.Rho * s.Hp +
            fit.A3 * (s.Pi - s.Pa) +
            fit.A4 * s.Rho * s.Q * s.Q;
        double err = s.TBha - pred;

        sw.WriteLine(string.Join(";",
            s.Time.ToString("O", CultureInfo.InvariantCulture),
            F(s.TBha, no),
            F(pred, no),
            F(err, no),
            F(s.Pi, no),
            F(s.Pa, no),
            F(s.Hp, no),
            F(s.Rho, no),
            F(s.Theta, no),
            F(s.Q, no)));
    }
}

static List<CalibratorCorrector.GammaCalibrationSituation> WriteOffBottomRotatingGammaCsv(SimulationOutput output, string path)
{
    var no = NorwegianCsvCulture();
    using var sw = new StreamWriter(path, false);
    sw.WriteLine("TimestampUtc;ElapsedSeconds;TdN;BdN;TBhaN;RhoKgM3;HM;HpM;LM;LpM;Qm3s");
    var situations = new List<CalibratorCorrector.GammaCalibrationSituation>();

    if (output.Topside.Count == 0 || output.Downhole.Count == 0) return situations;

    double depthMargin = ConfigurationForWOBCorrection.DepthMarginDefault;
    double minOmega = ConfigurationForWOBCorrection.MinDownholeRotationalSpeedDefault;

    var tops = output.Topside.OrderBy(s => s.TimestampUtc).ToList();
    var dws = output.Downhole.OrderBy(s => s.TimestampUtc).ToList();

    int tIdx = 0;
    double? lastOmega = null;
    DateTime? lastOmegaChangeTime = null;
    foreach (var d in dws)
    {
        while (tIdx + 1 < tops.Count && tops[tIdx + 1].TimestampUtc <= d.TimestampUtc) tIdx++;
        var t = tops[tIdx];

        if (lastOmega.HasValue && Math.Abs(d.RotationRadPerSec - lastOmega.Value) > 1e-9)
        {
            lastOmegaChangeTime = d.TimestampUtc;
        }
        lastOmega = d.RotationRadPerSec;

        bool offBottom = t.BitDepthM <= t.HoleDepthM - depthMargin;
        bool rotating = d.RotationRadPerSec >= minOmega;
        bool passedRefreshDelay =
            !lastOmegaChangeTime.HasValue ||
            (d.TimestampUtc - lastOmegaChangeTime.Value).TotalSeconds >= downholeDt;
        if (!offBottom || !rotating || !passedRefreshDelay) continue;

        double theta = t.InclinationRad;
        double h = t.TvdAtBitM;
        double hp = h - output.SensorToBitDistanceM * Math.Cos(theta);
        double l = t.BitDepthM;
        double lp = output.SensorToBitDistanceM;
        double bd = output.Sensors.Bd;

        sw.WriteLine(string.Join(";",
            d.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
            F(d.ElapsedSeconds, no),
            F(t.TdN, no),
            F(bd, no),
            F(d.TensionBhaN, no),
            F(t.RhoKgM3, no),
            F(h, no),
            F(hp, no),
            F(l, no),
            F(lp, no),
            F(t.FlowM3s, no)));

        situations.Add(new CalibratorCorrector.GammaCalibrationSituation(
            Time: d.TimestampUtc,
            Td: t.TdN,
            Bd: bd,
            TBha: d.TensionBhaN,
            Rho: t.RhoKgM3,
            H: h,
            Hp: hp,
            L: l,
            Lp: lp,
            Q: t.FlowM3s));
    }

    return situations;
}

static void WriteGammaFitDifferencesCsv(
    IReadOnlyList<CalibratorCorrector.GammaCalibrationSituation> situations,
    CalibratorCorrector.GammaCalibrationResult fit,
    string path)
{
    var no = NorwegianCsvCulture();
    using var sw = new StreamWriter(path, false);
    sw.WriteLine("TimestampUtc;MeasuredLhsN;PredictedLhsN;ErrorN;TdN;BdN;TBhaN;RhoKgM3;HM;HpM;LM;LpM;Qm3s");
    if (situations.Count == 0) return;

    foreach (var s in situations)
    {
        double measured = s.Td - s.Bd - s.TBha;
        double pred =
            fit.G1 * s.Hp +
            fit.G2 * s.Rho * s.Hp +
            fit.G3 * s.Rho * (s.L - s.Lp) * s.Q * s.Q;
        double err = measured - pred;

        sw.WriteLine(string.Join(";",
            s.Time.ToString("O", CultureInfo.InvariantCulture),
            F(measured, no),
            F(pred, no),
            F(err, no),
            F(s.Td, no),
            F(s.Bd, no),
            F(s.TBha, no),
            F(s.Rho, no),
            F(s.H, no),
            F(s.Hp, no),
            F(s.L, no),
            F(s.Lp, no),
            F(s.Q, no)));
    }
}

static List<CalibratorCorrector.GammaCalibrationFromTpSituation> WriteOffBottomRotatingGammaTpCsv(SimulationOutput output, string path)
{
    var no = NorwegianCsvCulture();
    using var sw = new StreamWriter(path, false);
    sw.WriteLine("TimestampUtc;ElapsedSeconds;TpN;TBhaN;RhoKgM3;HpM;LM;LpM;Qm3s");
    var situations = new List<CalibratorCorrector.GammaCalibrationFromTpSituation>();

    if (output.Topside.Count == 0 || output.Downhole.Count == 0) return situations;

    double depthMargin = ConfigurationForWOBCorrection.DepthMarginDefault;
    double minOmega = ConfigurationForWOBCorrection.MinDownholeRotationalSpeedDefault;

    var tops = output.Topside.OrderBy(s => s.TimestampUtc).ToList();
    var dws = output.Downhole.OrderBy(s => s.TimestampUtc).ToList();

    int tIdx = 0;
    double? lastOmega = null;
    DateTime? lastOmegaChangeTime = null;
    foreach (var d in dws)
    {
        while (tIdx + 1 < tops.Count && tops[tIdx + 1].TimestampUtc <= d.TimestampUtc) tIdx++;
        var t = tops[tIdx];

        if (lastOmega.HasValue && Math.Abs(d.RotationRadPerSec - lastOmega.Value) > 1e-9)
        {
            lastOmegaChangeTime = d.TimestampUtc;
        }
        lastOmega = d.RotationRadPerSec;

        bool offBottom = t.BitDepthM <= t.HoleDepthM - depthMargin;
        bool rotating = d.RotationRadPerSec >= minOmega;
        bool passedRefreshDelay =
            !lastOmegaChangeTime.HasValue ||
            (d.TimestampUtc - lastOmegaChangeTime.Value).TotalSeconds >= downholeDt;
        if (!offBottom || !rotating || !passedRefreshDelay) continue;

        double theta = t.InclinationRad;
        double h = t.TvdAtBitM;
        double hp = h - output.SensorToBitDistanceM * Math.Cos(theta);
        double l = t.BitDepthM;
        double lp = output.SensorToBitDistanceM;

        sw.WriteLine(string.Join(";",
            d.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
            F(d.ElapsedSeconds, no),
            F(t.TpN, no),
            F(d.TensionBhaN, no),
            F(t.RhoKgM3, no),
            F(hp, no),
            F(l, no),
            F(lp, no),
            F(t.FlowM3s, no)));

        situations.Add(new CalibratorCorrector.GammaCalibrationFromTpSituation(
            Time: d.TimestampUtc,
            Tp: t.TpN,
            TBha: d.TensionBhaN,
            Rho: t.RhoKgM3,
            Hp: hp,
            L: l,
            Lp: lp,
            Q: t.FlowM3s));
    }

    return situations;
}

static List<CalibratorCorrector.GammaCalibrationFromTdlSituation> WriteOffBottomRotatingGammaTdlCsv(SimulationOutput output, string path)
{
    var no = NorwegianCsvCulture();
    using var sw = new StreamWriter(path, false);
    sw.WriteLine("TimestampUtc;ElapsedSeconds;TdlN;TBhaN;RhoKgM3;HpM;LM;LpM;Qm3s");
    var situations = new List<CalibratorCorrector.GammaCalibrationFromTdlSituation>();

    if (output.Topside.Count == 0 || output.Downhole.Count == 0) return situations;

    double depthMargin = ConfigurationForWOBCorrection.DepthMarginDefault;
    double minOmega = ConfigurationForWOBCorrection.MinDownholeRotationalSpeedDefault;

    var tops = output.Topside.OrderBy(s => s.TimestampUtc).ToList();
    var dws = output.Downhole.OrderBy(s => s.TimestampUtc).ToList();

    int tIdx = 0;
    double? lastOmega = null;
    DateTime? lastOmegaChangeTime = null;
    foreach (var d in dws)
    {
        while (tIdx + 1 < tops.Count && tops[tIdx + 1].TimestampUtc <= d.TimestampUtc) tIdx++;
        var t = tops[tIdx];

        if (lastOmega.HasValue && Math.Abs(d.RotationRadPerSec - lastOmega.Value) > 1e-9)
        {
            lastOmegaChangeTime = d.TimestampUtc;
        }
        lastOmega = d.RotationRadPerSec;

        bool offBottom = t.BitDepthM <= t.HoleDepthM - depthMargin;
        bool rotating = d.RotationRadPerSec >= minOmega;
        bool passedRefreshDelay =
            !lastOmegaChangeTime.HasValue ||
            (d.TimestampUtc - lastOmegaChangeTime.Value).TotalSeconds >= downholeDt;
        if (!offBottom || !rotating || !passedRefreshDelay) continue;

        double theta = t.InclinationRad;
        double h = t.TvdAtBitM;
        double hp = h - output.SensorToBitDistanceM * Math.Cos(theta);
        double l = t.BitDepthM;
        double lp = output.SensorToBitDistanceM;

        sw.WriteLine(string.Join(";",
            d.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
            F(d.ElapsedSeconds, no),
            F(t.TdlN, no),
            F(d.TensionBhaN, no),
            F(t.RhoKgM3, no),
            F(hp, no),
            F(l, no),
            F(lp, no),
            F(t.FlowM3s, no)));

        situations.Add(new CalibratorCorrector.GammaCalibrationFromTdlSituation(
            Time: d.TimestampUtc,
            Tdl: t.TdlN,
            TBha: d.TensionBhaN,
            Rho: t.RhoKgM3,
            Hp: hp,
            L: l,
            Lp: lp,
            Q: t.FlowM3s));
    }

    return situations;
}

static void WriteGammaTpFitDifferencesCsv(
    IReadOnlyList<CalibratorCorrector.GammaCalibrationFromTpSituation> situations,
    CalibratorCorrector.GammaCalibrationResult fit,
    string path)
{
    var no = NorwegianCsvCulture();
    using var sw = new StreamWriter(path, false);
    sw.WriteLine("TimestampUtc;MeasuredLhsN;PredictedLhsN;ErrorN;TpN;TBhaN;RhoKgM3;HpM;LM;LpM;Qm3s");
    if (situations.Count == 0) return;

    foreach (var s in situations)
    {
        double measured = s.Tp - s.TBha;
        double pred =
            fit.G1 * s.Hp +
            fit.G2 * s.Rho * s.Hp +
            fit.G3 * s.Rho * (s.L - s.Lp) * s.Q * s.Q;
        double err = measured - pred;

        sw.WriteLine(string.Join(";",
            s.Time.ToString("O", CultureInfo.InvariantCulture),
            F(measured, no),
            F(pred, no),
            F(err, no),
            F(s.Tp, no),
            F(s.TBha, no),
            F(s.Rho, no),
            F(s.Hp, no),
            F(s.L, no),
            F(s.Lp, no),
            F(s.Q, no)));
    }
}

static void WriteGammaTdlFitDifferencesCsv(
    IReadOnlyList<CalibratorCorrector.GammaCalibrationFromTdlSituation> situations,
    CalibratorCorrector.GammaCalibrationResult fit,
    string path)
{
    var no = NorwegianCsvCulture();
    using var sw = new StreamWriter(path, false);
    sw.WriteLine("TimestampUtc;MeasuredLhsN;PredictedLhsN;ErrorN;TdlN;TBhaN;RhoKgM3;HpM;LM;LpM;Qm3s");
    if (situations.Count == 0) return;

    foreach (var s in situations)
    {
        double measured = s.Tdl - s.TBha;
        double pred =
            fit.G1 * s.Hp +
            fit.G2 * s.Rho * s.Hp +
            fit.G3 * s.Rho * (s.L - s.Lp) * s.Q * s.Q;
        double err = measured - pred;

        sw.WriteLine(string.Join(";",
            s.Time.ToString("O", CultureInfo.InvariantCulture),
            F(measured, no),
            F(pred, no),
            F(err, no),
            F(s.Tdl, no),
            F(s.TBha, no),
            F(s.Rho, no),
            F(s.Hp, no),
            F(s.L, no),
            F(s.Lp, no),
            F(s.Q, no)));
    }
}

static List<CalibratorCorrector.BetaCalibrationSituation> WriteOffBottomRotatingBetaCsv(
    SimulationOutput output,
    string path,
    string source)
{
    var no = NorwegianCsvCulture();
    using var sw = new StreamWriter(path, false);
    sw.WriteLine("TimestampUtc;ElapsedSeconds;Source;TTopSideOffN;HM;RhoKgM3;PiPa;PaPa;Qm3s;LM");
    var situations = new List<CalibratorCorrector.BetaCalibrationSituation>();

    if (output.Topside.Count == 0 || output.Downhole.Count == 0) return situations;

    double depthMargin = ConfigurationForWOBCorrection.DepthMarginDefault;
    double minOmega = ConfigurationForWOBCorrection.MinDownholeRotationalSpeedDefault;

    var tops = output.Topside.OrderBy(s => s.TimestampUtc).ToList();
    var dws = output.Downhole.OrderBy(s => s.TimestampUtc).ToList();

    int tIdx = 0;
    double? lastOmega = null;
    DateTime? lastOmegaChangeTime = null;
    foreach (var d in dws)
    {
        while (tIdx + 1 < tops.Count && tops[tIdx + 1].TimestampUtc <= d.TimestampUtc) tIdx++;
        var t = tops[tIdx];

        if (lastOmega.HasValue && Math.Abs(d.RotationRadPerSec - lastOmega.Value) > 1e-9)
        {
            lastOmegaChangeTime = d.TimestampUtc;
        }
        lastOmega = d.RotationRadPerSec;

        bool offBottom = t.BitDepthM <= t.HoleDepthM - depthMargin;
        bool rotating = d.RotationRadPerSec >= minOmega;
        bool passedRefreshDelay =
            !lastOmegaChangeTime.HasValue ||
            (d.TimestampUtc - lastOmegaChangeTime.Value).TotalSeconds >= downholeDt;
        if (!offBottom || !rotating || !passedRefreshDelay) continue;

        double tTopSideOff;
        if (source == "td")
        {
            tTopSideOff = t.TdN - output.Sensors.Bd;
        }
        else if (source == "tp")
        {
            double z = t.BlockPositionM;
            double q = t.FlowM3s;
            double fp =
                output.Sensors.C0 +
                output.Sensors.C1 * z +
                output.Sensors.C2 * z * z +
                output.Sensors.C3 * q +
                output.Sensors.C4 * q * q +
                output.Sensors.C5 * z * q;
            tTopSideOff = t.TpN - fp;
        }
        else if (source == "tdl")
        {
            double z = t.BlockPositionM;
            double signV = PhaseSignVelocity(t.Phase);
            double fdl =
                output.Sensors.D0 +
                output.Sensors.D1 * z +
                output.Sensors.D2 * signV;
            tTopSideOff = t.TdlN - fdl;
        }
        else
        {
            throw new InvalidOperationException($"Unknown beta source: {source}");
        }

        sw.WriteLine(string.Join(";",
            d.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
            F(d.ElapsedSeconds, no),
            source,
            F(tTopSideOff, no),
            F(t.TvdAtBitM, no),
            F(t.RhoKgM3, no),
            F(d.PressureInsidePa, no),
            F(d.PressureAnnulusPa, no),
            F(t.FlowM3s, no),
            F(t.BitDepthM, no)));

        situations.Add(new CalibratorCorrector.BetaCalibrationSituation(
            Time: d.TimestampUtc,
            TTopSideOff: tTopSideOff,
            H: t.TvdAtBitM,
            Rho: t.RhoKgM3,
            Pi: d.PressureInsidePa,
            Pa: d.PressureAnnulusPa,
            Q: t.FlowM3s,
            L: t.BitDepthM));
    }

    return situations;
}

static BetaStreamSummary WriteBetaStreamingFitDifferencesCsv(
    IReadOnlyList<CalibratorCorrector.BetaCalibrationSituation> situations,
    string path)
{
    var no = NorwegianCsvCulture();
    using var sw = new StreamWriter(path, false);
    sw.WriteLine("TimestampUtc;MeasuredTopSideOffN;PredictedTopSideOffN;ErrorN;HM;RhoKgM3;PiPa;PaPa;Qm3s;LM");
    if (situations.Count == 0) return new BetaStreamSummary(0, double.NaN, double.NaN, double.NaN, double.NaN);

    var acc = new List<CalibratorCorrector.BetaCalibrationSituation>(situations.Count);
    double sum = 0.0;
    double sumSq = 0.0;
    double sumAbs = 0.0;
    int n = 0;

    foreach (var s in situations.OrderBy(x => x.Time))
    {
        acc.Add(s);
        var fit = CalibratorCorrector.CalibrateBetaFromSituations(acc);

        double pred =
            fit.B0 * s.H +
            fit.B1 * s.Rho * s.H +
            fit.B2 * (s.Pi - s.Pa) +
            fit.B3 * s.Rho * s.Q * s.Q +
            fit.B4 * s.Rho * s.L * s.Q * s.Q;
        double err = s.TTopSideOff - pred;

        sw.WriteLine(string.Join(";",
            s.Time.ToString("O", CultureInfo.InvariantCulture),
            F(s.TTopSideOff, no),
            F(pred, no),
            F(err, no),
            F(s.H, no),
            F(s.Rho, no),
            F(s.Pi, no),
            F(s.Pa, no),
            F(s.Q, no),
            F(s.L, no)));

        sum += err;
        sumSq += err * err;
        sumAbs += Math.Abs(err);
        n++;
    }

    double mean = sum / n;
    double rmse = Math.Sqrt(sumSq / n);
    double mae = sumAbs / n;
    double variance = (sumSq / n) - mean * mean;
    double std = Math.Sqrt(Math.Max(0.0, variance));
    return new BetaStreamSummary(n, mean, std, mae, rmse);
}

static List<CalibratorCorrector.DCalibrationSituation> WriteInSlipsContextsCsv(SimulationOutput output, string path)
{
    var no = NorwegianCsvCulture();
    using var sw = new StreamWriter(path, false);
    sw.WriteLine("TimestampUtc;ElapsedSeconds;Phase;BlockPositionM;SignVelocity;TdN;TpN;TdlN");
    var situations = new List<CalibratorCorrector.DCalibrationSituation>();

    foreach (var t in output.Topside.OrderBy(s => s.TimestampUtc))
    {
        if (t.Connected) continue; // in-slips / empty-block conditions

        double signV = PhaseSignVelocity(t.Phase);
        sw.WriteLine(string.Join(";",
            t.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
            F(t.ElapsedSeconds, no),
            t.Phase,
            F(t.BlockPositionM, no),
            F(signV, no),
            F(t.TdN, no),
            F(t.TpN, no),
            F(t.TdlN, no)));

        situations.Add(new CalibratorCorrector.DCalibrationSituation(
            Time: t.TimestampUtc,
            Tdl: t.TdlN,
            Z: t.BlockPositionM,
            SignVelocity: signV));
    }

    return situations;
}

static DStreamSummary WriteDStreamingFitDifferencesCsv(
    IReadOnlyList<CalibratorCorrector.DCalibrationSituation> situations,
    string path)
{
    var no = NorwegianCsvCulture();
    using var sw = new StreamWriter(path, false);
    sw.WriteLine("TimestampUtc;MeasuredTdlN;PredictedTdlN;ErrorN;BlockPositionM;SignVelocity");
    if (situations.Count == 0) return new DStreamSummary(0, double.NaN, double.NaN, double.NaN, double.NaN);

    var acc = new List<CalibratorCorrector.DCalibrationSituation>(situations.Count);
    double sum = 0.0;
    double sumSq = 0.0;
    double sumAbs = 0.0;
    int n = 0;

    foreach (var s in situations.OrderBy(x => x.Time))
    {
        acc.Add(s);
        var fit = CalibratorCorrector.CalibrateDFromSituations(acc);

        double pred = fit.D0 + fit.D1 * s.Z + fit.D2 * s.SignVelocity;
        double err = s.Tdl - pred;

        sw.WriteLine(string.Join(";",
            s.Time.ToString("O", CultureInfo.InvariantCulture),
            F(s.Tdl, no),
            F(pred, no),
            F(err, no),
            F(s.Z, no),
            F(s.SignVelocity, no)));

        sum += err;
        sumSq += err * err;
        sumAbs += Math.Abs(err);
        n++;
    }

    double mean = sum / n;
    double rmse = Math.Sqrt(sumSq / n);
    double mae = sumAbs / n;
    double variance = (sumSq / n) - mean * mean;
    double std = Math.Sqrt(Math.Max(0.0, variance));
    return new DStreamSummary(n, mean, std, mae, rmse);
}

static List<CalibratorCorrector.CCalibrationSituation> WriteInSlipsTpContextsCsv(SimulationOutput output, string path)
{
    var no = NorwegianCsvCulture();
    using var sw = new StreamWriter(path, false);
    sw.WriteLine("TimestampUtc;ElapsedSeconds;Phase;BlockPositionM;Qm3s;TpN");
    var situations = new List<CalibratorCorrector.CCalibrationSituation>();

    foreach (var t in output.Topside.OrderBy(s => s.TimestampUtc))
    {
        if (t.Connected) continue; // in-slips / empty-block conditions

        sw.WriteLine(string.Join(";",
            t.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
            F(t.ElapsedSeconds, no),
            t.Phase,
            F(t.BlockPositionM, no),
            F(t.FlowM3s, no),
            F(t.TpN, no)));

        situations.Add(new CalibratorCorrector.CCalibrationSituation(
            Time: t.TimestampUtc,
            Tp: t.TpN,
            Z: t.BlockPositionM,
            Q: t.FlowM3s));
    }

    return situations;
}

static CStreamSummary WriteCStreamingFitDifferencesCsv(
    IReadOnlyList<CalibratorCorrector.CCalibrationSituation> situations,
    string path)
{
    var no = NorwegianCsvCulture();
    using var sw = new StreamWriter(path, false);
    sw.WriteLine("TimestampUtc;MeasuredTpN;PredictedTpN;ErrorN;BlockPositionM;Qm3s");
    if (situations.Count == 0) return new CStreamSummary(0, double.NaN, double.NaN, double.NaN, double.NaN);

    var acc = new List<CalibratorCorrector.CCalibrationSituation>(situations.Count);
    double sum = 0.0;
    double sumSq = 0.0;
    double sumAbs = 0.0;
    int n = 0;

    foreach (var s in situations.OrderBy(x => x.Time))
    {
        acc.Add(s);
        var fit = CalibratorCorrector.CalibrateCFromSituations(acc);

        double pred =
            fit.C0 +
            fit.C1 * s.Z +
            fit.C2 * s.Z * s.Z +
            fit.C3 * s.Q +
            fit.C4 * s.Q * s.Q +
            fit.C5 * s.Z * s.Q;
        double err = s.Tp - pred;

        sw.WriteLine(string.Join(";",
            s.Time.ToString("O", CultureInfo.InvariantCulture),
            F(s.Tp, no),
            F(pred, no),
            F(err, no),
            F(s.Z, no),
            F(s.Q, no)));

        sum += err;
        sumSq += err * err;
        sumAbs += Math.Abs(err);
        n++;
    }

    double mean = sum / n;
    double rmse = Math.Sqrt(sumSq / n);
    double mae = sumAbs / n;
    double variance = (sumSq / n) - mean * mean;
    double std = Math.Sqrt(Math.Max(0.0, variance));
    return new CStreamSummary(n, mean, std, mae, rmse);
}

static void WriteAlphaStreamingFitDifferencesCsv(
    CalibratorCorrector.AlphaStreamingResult fit,
    string path)
{
    var no = NorwegianCsvCulture();
    using var sw = new StreamWriter(path, false);
    sw.WriteLine("TimestampUtc;TBhaN;PredictedTBhaN;ErrorN;ActiveParameterCount;ActiveMask");
    foreach (var p in fit.Points)
    {
        sw.WriteLine(string.Join(";",
            p.Time.ToString("O", CultureInfo.InvariantCulture),
            F(p.TBha, no),
            F(p.PredictedTBha, no),
            F(p.Error, no),
            p.ActiveParameterCount.ToString(CultureInfo.InvariantCulture),
            p.ActiveMask));
    }
}

static void ReplayCalibration(SimulationOutput output, string wobLogPath)
{
    Console.WriteLine($"  calibration replay: {output.ScenarioName}");
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
    var no = NorwegianCsvCulture();
    using var sw = new StreamWriter(wobLogPath, false);
    sw.WriteLine("TimestampUtc;ElapsedSeconds;Phase;TrueWobN;CorrectedDwobN;CorrectedSwobN;CorrectedSwobTd;CorrectedSwobTp;CorrectedSwobTdl;CorrectedSwobTdBHA;CorrectedSwobTpBHA;CorrectedSwobTdlBHA");
    foreach (var s in tops)
    {
        while (dIdx + 1 < dws.Count && dws[dIdx + 1].TimestampUtc <= s.TimestampUtc)
        {
            dIdx++;
            dCur = dws[dIdx];
        }

        SetScalar(top, nameof(TopSideMeasurementsData.BlockPosition), s.BlockPositionM);
        SetScalar(top, nameof(TopSideMeasurementsData.BottomOfStringDepth), s.BitDepthM);
        SetScalar(top, nameof(TopSideMeasurementsData.BottomHoleDepth), s.HoleDepthM);
        SetScalar(top, nameof(TopSideMeasurementsData.BottomOfStringVerticalDepth), s.TvdAtBitM);
        SetScalar(top, nameof(TopSideMeasurementsData.BottomOfStringInclination), s.InclinationRad);
        SetScalar(top, nameof(TopSideMeasurementsData.FlowrateIn), s.FlowM3s);
        SetScalar(top, nameof(TopSideMeasurementsData.DrillingFluidDensityIn), s.RhoKgM3);
        SetScalar(top, nameof(TopSideMeasurementsData.MeasuredTensionInstrumentedSub), s.TdN);
        SetScalar(top, nameof(TopSideMeasurementsData.HookLoadAtTopDrive), s.TpN);
        SetScalar(top, nameof(TopSideMeasurementsData.HookLoadAtAnchor), s.TdlN);
        SetScalar(top, nameof(TopSideMeasurementsData.SurfaceWeightOnBit), 0.0);

        SetScalar(dh, nameof(DownholeMeasurementsData.AverageRawWeight), dCur.TensionBhaN);
        SetScalar(dh, nameof(DownholeMeasurementsData.StringPressure), dCur.PressureInsidePa);
        SetScalar(dh, nameof(DownholeMeasurementsData.AnnulusPressure), dCur.PressureAnnulusPa);
        SetScalar(dh, nameof(DownholeMeasurementsData.AverageRotationalSpeed), dCur.RotationRadPerSec);
        SetScalar(composer, nameof(ComposerRecommendationsData.WOBRecommendedMaximum), 0.0);
        SetScalarNullable(corrected!, nameof(CorrectedMeasurementsData.CorrectedSurfaceWeightOnBit), null);
        SetScalarNullable(corrected!, nameof(CorrectedMeasurementsData.CorrectedDownholeWeightOnBit), null);
        SetScalarNullable(corrected!, nameof(CorrectedMeasurementsData.CorrectedHookLoadAtTopDrive), null);
        SetScalarNullable(corrected!, nameof(CorrectedMeasurementsData.CorrectedHookLoadAtDeadLine), null);
        SetScalar(correctedRec, nameof(CorrectedRecommendationsData.CorrectedWOBRecommendedMaximum), 0.0);

        CalibratorCorrector.Process(
            logger: null,
            now: s.TimestampUtc,
            topSide: top,
            downhole: dh,
            composerRecommendationsData: composer,
            SensorToBitDistance: output.SensorToBitDistanceM,
            configuration: config,
            correctedMeasurements: corrected!,
            correctedRecommendationsData: correctedRec);

        double trueWob = s.TrueWobN;

        double[]? alphaNow = GetRlsBeta("RlsAlpha");
        double[]? betaNow = GetRlsBeta("RlsBeta");
        double[]? gammaNow = GetRlsBeta("RlsGamma");
        double[]? bdB = GetRlsBeta("RlsBd");
        double[]? fpB = GetRlsBeta("RlsFp_WithTd");
        double[]? fdlB = GetRlsBeta("RlsFdl_WithTd");

        double dp = dCur.PressureInsidePa - dCur.PressureAnnulusPa;
        double q2 = s.FlowM3s * s.FlowM3s;
        bool shouldReportWob = s.Connected && dCur.RotationRadPerSec >= config.MinDownholeRotationalSpeed;
        double correctedDwob = double.NaN;

        double z = s.BlockPositionM;
        double signV = PhaseSignVelocity(s.Phase);
        double fpPred = double.NaN;
        if (fpB is not null)
        {
            fpPred = fpB[0] + fpB[1] * z + fpB[2] * z * z + fpB[3] * s.FlowM3s + fpB[4] * q2 + fpB[5] * z * s.FlowM3s;
        }
        double fdlPred = double.NaN;
        if (fdlB is not null)
        {
            fdlPred = fdlB[0] + fdlB[1] * z + fdlB[2] * signV;
        }
        double bdPred = double.NaN;
        if (bdB is not null)
        {
            bdPred = bdB[0];
        }
        double tdCorr = s.TdN - bdPred;
        double tpCorr = s.TpN - fpPred;
        double tdlCorr = s.TdlN - fdlPred;

        double correctedDWOB = double.NaN;
        if (shouldReportWob &&
            corrected is not null &&
            corrected.CorrectedDownholeWeightOnBit is not null &&
            corrected.CorrectedDownholeWeightOnBit.Value is not null)
        {
            correctedDWOB = corrected.CorrectedDownholeWeightOnBit.Value.Value;
            correctedDwob = correctedDWOB;
        }
        double correctedSWOB = double.NaN;
        if (shouldReportWob &&
            corrected is not null &&
            corrected.CorrectedSurfaceWeightOnBit is not null &&
            corrected.CorrectedSurfaceWeightOnBit.Value is not null)
        {
            correctedSWOB = corrected.CorrectedSurfaceWeightOnBit.Value.Value;
        }
        double correctedSWOBTd = double.NaN;
        double correctedSWOBTp = double.NaN;
        double correctedSWOBTdl = double.NaN;
        if (betaNow is not null)
        {
            double betaPred = betaNow[0] * s.TvdAtBitM +
                              betaNow[1] * s.RhoKgM3 * s.TvdAtBitM +
                              betaNow[2] * dp +
                              betaNow[3] * s.RhoKgM3 * q2 +
                              betaNow[4] * s.RhoKgM3 * s.BitDepthM * q2;
            if (shouldReportWob)
            {
                correctedSWOBTd = -tdCorr + betaPred;
                correctedSWOBTp = -tpCorr + betaPred;
                correctedSWOBTdl = -tdlCorr + betaPred;
            }
        }
        double correctedSWOBTdBHA = double.NaN;
        double correctedSWOBTpBHA = double.NaN;
        double correctedSWOBTdlBHA = double.NaN;
        if (gammaNow is not null && alphaNow is not null)
        {
            double hp = s.TvdAtBitM - output.SensorToBitDistanceM * Math.Cos(s.InclinationRad);
            double gammaPred = gammaNow[0] * hp +
                               gammaNow[1] * s.RhoKgM3 * hp +
                               gammaNow[2] * s.RhoKgM3 * (s.BitDepthM - output.SensorToBitDistanceM) * q2;
            double alphaPred = alphaNow[0] * Math.Cos(s.InclinationRad) + 
                               alphaNow[1] * s.RhoKgM3 * Math.Cos(s.InclinationRad) + 
                               alphaNow[2] * s.RhoKgM3 * hp +
                               alphaNow[3] * (dCur.PressureInsidePa - dCur.PressureAnnulusPa) + 
                               alphaNow[4] * s.RhoKgM3 * s.FlowM3s * s.FlowM3s;
            if (shouldReportWob &&
                dh.AverageRawWeight is not null &&
                dh.AverageRawWeight.Value is not null)
            {
                correctedSWOBTdBHA = -tdCorr + gammaPred + alphaPred;
                correctedSWOBTpBHA = -tpCorr + gammaPred + alphaPred;
                correctedSWOBTdlBHA = -tdlCorr + gammaPred + alphaPred;
            }
        }
        sw.WriteLine(string.Join(";",
            s.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
            F(s.ElapsedSeconds, no),
            s.Phase,
            F(trueWob, no),
            FNullable(correctedDwob, no),
            FNullable(correctedSWOB, no),
            FNullable(correctedSWOBTd, no),
            FNullable(correctedSWOBTp, no),
            FNullable(correctedSWOBTdl, no),
            FNullable(correctedSWOBTdBHA, no),
            FNullable(correctedSWOBTpBHA, no),
            FNullable(correctedSWOBTdlBHA, no)));
    }

    var alpha = GetRlsBeta("RlsAlpha");
    var beta = GetRlsBeta("RlsBeta");
    var gamma = GetRlsBeta("RlsGamma");
    var bd = GetRlsBeta("RlsBd");
    var c = GetRlsBeta("RlsFp_WithTd");
    var d = GetRlsBeta("RlsFdl_WithTd");

    PrintRecovery("alpha", alpha, new[] { output.Model.Alpha.A0, output.Model.Alpha.A1, output.Model.Alpha.A2, output.Model.Alpha.A3, output.Model.Alpha.A4 });
    PrintRecovery("beta", beta, new[] { output.Model.Beta.B0, output.Model.Beta.B1, output.Model.Beta.B2, output.Model.Beta.B3, output.Model.Beta.B4 });
    PrintRecovery("gamma", gamma, new[] { output.Model.Gamma.G1, output.Model.Gamma.G2, output.Model.Gamma.G3 });
    PrintRecovery("b_d", bd, new[] { output.Sensors.Bd });
    PrintRecovery("c", c, new[] { output.Sensors.C0, output.Sensors.C1, output.Sensors.C2, output.Sensors.C3, output.Sensors.C4, output.Sensors.C5 });
    PrintRecovery("d", d, new[] { output.Sensors.D0, output.Sensors.D1, output.Sensors.D2 });
}

static double PhaseSignVelocity(string phase)
{
    return phase switch
    {
        "off_slip" => 1.0,
        "raise_1m" => 1.0,
        "ream_up" => 1.0,
        "friction_up" => 1.0,
        "tag_bottom" => -1.0,
        "ream_down" => -1.0,
        "friction_down" => -1.0,
        "set_slips" => -1.0,
        "connection" => 1.0,
        _ => 0.0
    };
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

static void SetScalarNullable(object target, string propertyName, double? value)
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

static double[]? GetRlsBeta(string fieldName)
{
    var t = typeof(CalibratorCorrector);
    return fieldName switch
    {
        "RlsAlpha" => GetAlphaAdaptive(t),
        "RlsBeta" => GetAdaptiveCoef(t, "BetaAdaptive"),
        "RlsGamma" => GetAdaptiveCoef(t, "GammaAdaptive"),
        "RlsBd" => GetAdaptiveCoef(t, "BdAdaptive"),
        "RlsFp_WithTd" => GetAdaptiveCoef(t, "FpWithTdAdaptive"),
        "RlsFdl_WithTd" => GetAdaptiveCoef(t, "FdlWithTdAdaptive"),
        "RlsFp_NoTd" => GetAdaptiveCoef(t, "FpNoTdAdaptive"),
        "RlsFdl_NoTd" => GetAdaptiveCoef(t, "FdlNoTdAdaptive"),
        "RlsFp_Unconnected" => GetAdaptiveCoef(t, "FpUnconnectedAdaptive"),
        "RlsFdl_Unconnected" => GetAdaptiveCoef(t, "FdlUnconnectedAdaptive"),
        _ => throw new InvalidOperationException($"Unsupported model field name {fieldName}")
    };
}

static double[] GetAlphaAdaptive(Type calibratorType)
{
    var f = calibratorType.GetField("AlphaAdaptive", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Missing field AlphaAdaptive");
    var a = f.GetValue(null) ?? throw new InvalidOperationException("Null AlphaAdaptive");
    var pA0 = a.GetType().GetProperty("A0") ?? throw new InvalidOperationException("Missing AlphaAdaptive.A0");
    var pA1 = a.GetType().GetProperty("A1") ?? throw new InvalidOperationException("Missing AlphaAdaptive.A1");
    var pA2 = a.GetType().GetProperty("A2") ?? throw new InvalidOperationException("Missing AlphaAdaptive.A2");
    var pA3 = a.GetType().GetProperty("A3") ?? throw new InvalidOperationException("Missing AlphaAdaptive.A3");
    var pA4 = a.GetType().GetProperty("A4") ?? throw new InvalidOperationException("Missing AlphaAdaptive.A4");
    return new[]
    {
        (double)(pA0.GetValue(a) ?? double.NaN),
        (double)(pA1.GetValue(a) ?? double.NaN),
        (double)(pA2.GetValue(a) ?? double.NaN),
        (double)(pA3.GetValue(a) ?? double.NaN),
        (double)(pA4.GetValue(a) ?? double.NaN)
    };
}

static double[]? GetAdaptiveCoef(Type calibratorType, string adaptiveFieldName)
{
    var f = calibratorType.GetField(adaptiveFieldName, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Missing field {adaptiveFieldName}");
    var model = f.GetValue(null) ?? throw new InvalidOperationException($"Null {adaptiveFieldName}");
    var pCoef = model.GetType().GetProperty("Coef")
        ?? throw new InvalidOperationException($"Missing {adaptiveFieldName}.Coef");
    var pMeanError = model.GetType().GetProperty("MeanError")
        ?? throw new InvalidOperationException($"Missing {adaptiveFieldName}.MeanError");
    if (pMeanError.GetValue(model) is double meanError && double.IsNaN(meanError))
    {
        return null; // not initialized yet
    }
    else
    {
        return ((double[]?)pCoef.GetValue(model)) ?? Array.Empty<double>();
    }
}

static void PrintRecovery(string name, double[]? estimated, double[] truth)
{
    double worstRel = 0.0;
    double worstAbsForZeroTruth = 0.0;
    bool hasNonZeroTruth = false;
    bool hasZeroTruth = false;
    if (estimated is not null) 
    {
        for (int i = 0; i < Math.Min(estimated.Length, truth.Length); i++)
        {
            double absErr = Math.Abs(estimated[i] - truth[i]);
            if (Math.Abs(truth[i]) < 1e-9)
            {
                hasZeroTruth = true;
                if (absErr > worstAbsForZeroTruth) worstAbsForZeroTruth = absErr;
            }
            else
            {
                hasNonZeroTruth = true;
                double rel = absErr / Math.Abs(truth[i]);
                if (rel > worstRel) worstRel = rel;
            }
        }
    }
    if (hasNonZeroTruth)
    {
        Console.WriteLine($"    {name}: worst rel err (non-zero truth) = {100.0 * worstRel:F2}%");
    }
    if (hasZeroTruth)
    {
        Console.WriteLine($"    {name}: worst abs err (zero truth) = {worstAbsForZeroTruth:G6}");
    }
}

static string F(double v, CultureInfo culture) => v.ToString("G17", culture);
static string FNullable(double v, CultureInfo culture) => double.IsNaN(v) ? string.Empty : v.ToString("G17", culture);
static CultureInfo NorwegianCsvCulture()
{
    var c = (CultureInfo)CultureInfo.GetCultureInfo("nb-NO").Clone();
    c.NumberFormat.NegativeSign = "-";
    return c;
}
static double ToLpm(double m3s) => m3s * 60000.0;
static double ToRpm(double radPerSec) => radPerSec * 60.0 / (2.0 * Math.PI);
// Assumption: 1 kdaN = 10,000 N.
static double ToKdaN(double n) => n / 10000.0;
static double ToBar(double pa) => pa / 100000.0;

public record AlphaModel(double A0, double A1, double A2, double A3, double A4);
public record BetaModel(double B0, double B1, double B2, double B3, double B4);
public record GammaModel(double G1, double G2, double G3);
public record HydraulicConstants(double DeltaPCoeff);
public record PhysicalModel(AlphaModel Alpha, BetaModel Beta, GammaModel Gamma, HydraulicConstants Hydraulics);

public record SensorModels(
    double Bd,
    double C0, double C1, double C2, double C3, double C4, double C5,
    double D0, double D1, double D2);

public record SimulationScenario(string Name, double SensorToBitDistanceM, PhysicalModel Model, SensorModels Sensors);

public record BetaStreamSummary(
    int Count,
    double MeanError,
    double StdError,
    double Mae,
    double Rmse);

public record DStreamSummary(
    int Count,
    double MeanError,
    double StdError,
    double Mae,
    double Rmse);

public record CStreamSummary(
    int Count,
    double MeanError,
    double StdError,
    double Mae,
    double Rmse);

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








