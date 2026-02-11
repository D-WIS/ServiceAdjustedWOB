# DWIS.Service.WOBCorrections.TestSources

Helper worker service that injects synthetic top-side and downhole measurements into the DWIS Blackboard so they can be consumed by `DWIS.Service.WOBCorrections.Server`.

## What It Does

At startup, the service:
1. Builds a synthetic drilling scenario and model coefficients.
2. Generates a full simulated time series using `MeasurementGenerator.GenerateSeries(...)`.
3. Registers top-side and downhole data models for Blackboard publishing.

During runtime loop, it:
1. Replays top-side samples in sequence.
2. Aligns and updates the current downhole sample by timestamp.
3. Publishes top-side and downhole values to Blackboard each tick.
4. Logs key values (block position, surface WOB, average raw weight).

The replay is cyclic: when the end of the generated series is reached, it wraps to the beginning.

## Data Flow Summary

Publishes to Blackboard:
- `TopSideMeasurementsData`
- `DownholeMeasurementsData`

No Blackboard queries are required for this helper; it acts as a source injector.

## Simulated Signals

Published top-side channels include:
- block position
- bit depth and hole depth
- vertical depth and inclination
- flowrate and mud density
- measured tensions/hookloads (`Td`, `Tp`, `Tdl` mapped to model fields)
- surface WOB proxy

Published downhole channels include:
- average raw weight
- string pressure
- annulus pressure
- average rotational speed

## Timing and Configuration

Configuration type:
- `ConfigurationSources`

Key setting:
- `LoopDurationDownholeTelemetry` (default: `00:00:10`)

This controls intended downhole telemetry cadence handling. Main loop cadence is inherited from `DWISWorker` base configuration.

## Run Locally

From repository root:

```sh
dotnet run --project DWIS.Service.WOBCorrections.TestSources
```

Prerequisites:
- reachable DWIS Blackboard endpoint
- valid OPC UA client configuration at:
  - `DWIS.Service.WOBCorrections.TestSources/config/Quickstarts.ReferenceClient.Config.xml`

## Typical Integration Setup

Use with:
- `DWIS.Service.WOBCorrections.Server` (consumes injected measurements and publishes corrected outputs)
- `DWIS.Service.WOBCorrections.TestAdvisor` / `TestComposer` / `TestADCS` for end-to-end recommendation-path validation

## Expected Logs

When running, logs typically include:
- `Block position: ...`
- `Average Surface WOB: ...`
- `Average Raw Weight: ...`

## Docker

Build image from repository root:

```sh
docker build -f DWIS.Service.WOBCorrections.TestSources/Dockerfile -t dwis-wob-testsources .
```

Run container:

```sh
docker run -d --name dwis-wob-testsources -v c:\Volumes\DWISTestSources:/home dwis-wob-testsources
```

Container entrypoint:
- `dotnet DWIS.Service.WOBCorrections.TestSources.dll`

## Project Structure

- `Program.cs`: host bootstrap (`AddHostedService<Worker>`).
- `Worker.cs`: scenario generation and publish loop.
- `ConfigurationSources.cs`: test-source specific configuration model.
- `config/Quickstarts.ReferenceClient.Config.xml`: OPC UA client settings.
- `appsettings*.json`: logging settings.
- `Dockerfile`: container build/runtime definition.

## Troubleshooting

- If no data appears on Blackboard, check OPC UA endpoint/certificate configuration.
- If correction service outputs remain empty, verify this source is publishing all required channels.
- If replay timing seems off, review base loop settings and `LoopDurationDownholeTelemetry`.

## Packaging

This project targets `net8.0` and includes `README.md` and `LICENSE` in package metadata.
