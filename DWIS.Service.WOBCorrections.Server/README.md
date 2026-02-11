# DWIS.Service.WOBCorrections.Server

Worker service that performs automatic taring/correction of surface and downhole WOB signals and republishes corrected values for downstream consumers (including ADCS-facing recommendations).

## What It Does

On each loop iteration, the service:
1. Reads top-side measurements from the DWIS Blackboard.
2. Reads downhole measurements from the DWIS Blackboard.
3. Reads composer recommendations (e.g., max WOB recommendation).
4. Reads contextual BHA drill string data.
5. Runs `CalibratorCorrector.Process(...)` from `DWIS.Service.WOBCorrections.Model`.
6. Publishes corrected measurements.
7. Publishes corrected recommendations.
8. Reloads configuration.

## Data Flow Summary

Inputs queried from Blackboard:
- `TopSideMeasurementsData`
- `DownholeMeasurementsData`
- `ComposerRecommendationsData`
- `BHADrillStringData`

Outputs published to Blackboard:
- `CorrectedMeasurementsData`
- `CorrectedRecommendationsData`

## Input and Output Signals (High Level)

Typical input channels include:
- top-side: block position, bit depth, hole depth, flowrate, mud density, tension/hookload variants, surface WOB
- downhole: average raw weight, string pressure, annulus pressure, rotational speed
- composer: recommended max WOB

Typical output channels include:
- corrected surface WOB
- corrected downhole WOB
- corrected hookload at top drive / deadline
- corrected max WOB recommendation

## Sensor-to-Bit Distance

The worker currently uses a default sensor-to-bit distance of `2.0 m` in the correction call.

`Worker.cs` contains a placeholder for deriving this value from `BHADrillStringData`; if no extraction is implemented, the default is used.

## Configuration

The service uses standard .NET configuration sources (`appsettings`, environment variables, user secrets).

Configuration model:
- `DWIS.Service.WOBCorrections.Model/ConfigurationForWOBCorrection.cs`

Key calibration parameters (defaults in code):
- `WindowDuration`: `00:00:30`
- `MaxSurfaceAge`: `00:10:00`
- `MinSurfaceSamplesPerWindow`: `10`
- `DepthMargin`: `0.5`
- `MinDownholeRotationalSpeed`: `50 rpm` (stored in `rad/s`)
- `MaxRelQMad`: `0.15`
- `MaxRelTMad`: `0.25`
- `MaxDepthMad`: `0.2`
- `MinVelocityForMotion`: `1e-5`
- `FactorThresholdInSlips`: `1.5`
- `DeltaTensionInSlips`: `50000`
- `MinDistanceInSlips`: `0.1`

Additional config field:
- `BHADrillStringHostURL` (available in config model; useful in related components handling BHA sourcing)

## Running Locally

From repository root:

```sh
dotnet run --project DWIS.Service.WOBCorrections.Server
```

Prerequisites:
- reachable DWIS Blackboard endpoint
- valid OPC UA client config at `DWIS.Service.WOBCorrections.Server/config/Quickstarts.ReferenceClient.Config.xml`
- upstream services or test publishers providing the required input channels

## Docker

Build image from repository root:

```sh
docker build -f DWIS.Service.WOBCorrections.Server/Dockerfile -t dwis-wob-corrections-server .
```

Run container:

```sh
docker run -d --name dwis-wob-corrections-server -v c:\Volumes\DWISWOBCorrections:/home dwis-wob-corrections-server
```

The image copies OPC UA config to `/app/config` during build and starts with:
- `dotnet DWIS.Service.WOBCorrections.Server.dll`

## Project Structure

- `Program.cs`: host bootstrap (`AddHostedService<Worker>`).
- `Worker.cs`: Blackboard query/publish loop and correction orchestration.
- `BHADrillStringData.cs`: contextual drill-string query model.
- `appsettings.json`: baseline logging config.
- `config/Quickstarts.ReferenceClient.Config.xml`: OPC UA client config.
- `Dockerfile`: container build/runtime definition.

## Troubleshooting

- If no corrected values are published, verify all input signals are present on Blackboard.
- If corrected values look unchanged, verify calibration windows are populated (sample density and signal variability matter).
- If connection fails at startup, validate OPC UA certificates/endpoints in `config/Quickstarts.ReferenceClient.Config.xml`.
- If recommendation correction is missing, confirm composer provides `WOBRecommendedMaximum`.

## Packaging

This project targets `net8.0` and includes `README.md` and `LICENSE` in package metadata.
