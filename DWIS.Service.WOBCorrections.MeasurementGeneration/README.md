# DWIS.Service.WOBCorrections.MeasurementGeneration

Synthetic measurement generator for drilling operations used by the WOB correction workflow.

This library creates synchronized:
- top-side measurements at `1 Hz`
- downhole measurements at `0.1 Hz` (`10 s` interval)

It is used to produce deterministic, repeatable datasets for calibration and validation of WOB correction models.

## What It Simulates

For each stand, the generator runs a realistic phase sequence:
1. `off_slip`
2. `pump_startup`
3. `rotation_startup`
4. `tag_bottom`
5. `drill`
6. `raise_1m`
7. `ream_up`
8. `ream_down`
9. `friction_up`
10. `friction_down`
11. `set_slips`
12. `connection`
13. `wait_on_slips`

The full series spans bit depth from `800 m` to `1100 m` in stand increments of `30 m`.

## Public Types

- `SimulationScenario`: scenario name + physical model + sensor artifact model.
- `SimulatorState`: mutable runtime state (time, depths, block position, flow, rotation).
- `SimulationOutput`: result container with:
  - `Topside` (`List<SimTopsideSample>`)
  - `Downhole` (`List<SimDownholeSample>`)
- `MeasurementGenerator.GenerateSeries(...)`: main entry point.

## Physics and Signal Model (High Level)

The generator combines:
- depth-dependent fluid density and inclination
- hydrostatic and hydraulic pressure components
- flow and rotation ramps per phase
- WOB behavior during `tag_bottom`, `drill`, and `raise_1m`
- sensor artifacts for top-side channels (`Bd`, `C*`, `D*` coefficients)
- bounded random noise for both top-side and downhole signals

## Units

Core units are SI:
- length: `m`
- flow: `m^3/s`
- pressure: `Pa`
- force/tension/WOB: `N`
- rotation: `rad/s`
- timestamps: UTC (`DateTime`)

## Quick Start

```csharp
using DWIS.Service.WOBCorrections.MeasurementGeneration;

var model = new PhysicalModel(
    Alpha: new AlphaModel(A0: 0, A1: 0, A2: -1, A3: 1, A4: 4300),
    Beta:  new BetaModel(B0: 0, B1: 0, B2: 1, B3: 4300, B4: 0),
    Hydraulics: new HydraulicConstants(DeltaPCoeff: 4.0e7));

var sensors = new SensorModels(
    Bd: 0,
    C0: 0, C1: 0, C2: 0, C3: 0, C4: 0, C5: 0,
    D0: 0, D1: 0, D2: 0);

var scenario = new SimulationScenario(
    Name: "example",
    SensorToBitDistanceM: 0.0,
    Model: model,
    Sensors: sensors);

var state = new SimulatorState
{
    Time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    ElapsedSeconds = 0.0,
    NextDownholeSeconds = 0.0,
    BitDepth = 800.0,
    HoleDepth = 800.0,
    BlockPosition = 30.0,
    FlowM3s = 0.0,
    OmegaRadPerSec = 0.0
};

var output = new SimulationOutput(
    ScenarioName: scenario.Name,
    RunIndex: 1,
    SensorToBitDistanceM: scenario.SensorToBitDistanceM,
    Model: scenario.Model,
    Sensors: scenario.Sensors);

MeasurementGenerator.GenerateSeries(new Random(1234), scenario, state, output);

Console.WriteLine($"Top-side samples: {output.Topside.Count}");
Console.WriteLine($"Downhole samples: {output.Downhole.Count}");
```

## Output Channels

`SimTopsideSample` includes:
- kinematics/depths (`BlockPositionM`, `BitDepthM`, `HoleDepthM`, `TvdAtBitM`, `InclinationRad`)
- process conditions (`FlowM3s`, `RhoKgM3`, `RotationRadPerSec`)
- forces (`TrueWobN`, `TdN`, `TpN`, `TdlN`)
- metadata (`TimestampUtc`, `ElapsedSeconds`, `Phase`, `Connected`)

`SimDownholeSample` includes:
- tension and pressures (`TensionBhaN`, `PressureInsidePa`, `PressureAnnulusPa`)
- rotation (`RotationRadPerSec`)
- metadata (`TimestampUtc`, `ElapsedSeconds`, `Phase`, `Connected`)

## Typical Usage in This Repository

- `DWIS.Service.WOBCorrections.ModelTest` generates JSON/CSV datasets for calibration checks.
- `DWIS.Service.WOBCorrections.TestSources` streams generated values to the Blackboard for integration-style testing.

## Packaging

This project is a `net8.0` library. Both `README.md` and `LICENSE` are included in the NuGet package metadata.
