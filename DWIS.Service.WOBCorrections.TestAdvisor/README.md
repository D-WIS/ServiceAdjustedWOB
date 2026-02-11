# DWIS.Service.WOBCorrections.TestAdvisor

Helper worker service that verifies corrected WOB availability and publishes a simple advisor WOB limit signal to the DWIS Blackboard.

## What It Does

On each loop tick, the service:
1. Reads top-side measurements.
2. Reads downhole measurements.
3. Reads corrected measurements.
4. Computes/publishes an advisor recommendation:
   - `WOBMaxLimit = 10.0 * CorrectedSurfaceWeightOnBit`
5. Logs corrected WOB and advisor max WOB values.

This component is intended for integration validation and demonstration workflows.

## Data Flow Summary

Reads from Blackboard:
- `TopSideMeasurementsData`
- `DownholeMeasurementsData`
- `CorrectedMeasurementsData`

Publishes to Blackboard:
- `AdvisorRecommendationsData`

Current recommendation rule in `Worker.cs`:
- if corrected surface WOB is available, set `AdvisorRecommendationsData.WOBMaxLimit` to `10x` corrected surface WOB.

## Typical Use Case

Run this service with:
- `DWIS.Service.WOBCorrections.Server` (publishes corrected measurements)
- test/source publishers for top-side and downhole input channels

Then verify that advisor recommendation values are produced and visible to downstream consumers.

## Run Locally

From repository root:

```sh
dotnet run --project DWIS.Service.WOBCorrections.TestAdvisor
```

Prerequisites:
- reachable DWIS Blackboard endpoint
- valid OPC UA client configuration at:
  - `DWIS.Service.WOBCorrections.TestAdvisor/config/Quickstarts.ReferenceClient.Config.xml`

## Expected Logs

When values are available, logs include:
- `Corrected WOB: ...`
- `Advisor Recommended Max WOB: ...`

## Configuration

The service uses base `Configuration` from `DWIS.RigOS.Common.Worker` and standard .NET config sources:
- `appsettings.json`
- `appsettings.Development.json`
- environment variables
- user secrets

Current appsettings define logging levels only.

## Docker

Build image from repository root:

```sh
docker build -f DWIS.Service.WOBCorrections.TestAdvisor/Dockerfile -t dwis-wob-testadvisor .
```

Run container:

```sh
docker run -d --name dwis-wob-testadvisor -v c:\Volumes\DWISTestAdvisor:/home dwis-wob-testadvisor
```

Container entrypoint:
- `dotnet DWIS.Service.WOBCorrections.TestAdvisor.dll`

## Project Structure

- `Program.cs`: host bootstrap (`AddHostedService<Worker>`).
- `Worker.cs`: Blackboard read/compute/publish loop.
- `config/Quickstarts.ReferenceClient.Config.xml`: OPC UA client settings.
- `appsettings*.json`: logging settings.
- `Dockerfile`: container build/runtime definition.

## Troubleshooting

- If advisor values are not published, verify corrected measurements are present first.
- If corrected WOB is missing, ensure the correction server is running and has required input signals.
- If no logs appear, validate Blackboard connectivity and OPC UA client certificate settings.

## Packaging

This project targets `net8.0` and includes `README.md` and `LICENSE` in package metadata.
