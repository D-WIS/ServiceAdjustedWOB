# DWIS.Service.WOBCorrections.TestADCS

Helper worker service used to verify that corrected WOB recommendation values are available on the DWIS Blackboard for ADCS consumption.

## What It Does

This service acts as a lightweight monitor:
1. Connects to the DWIS Blackboard.
2. Subscribes to recommendation data models.
3. Reads recommendation values on each loop tick.
4. Logs both original and corrected recommended max WOB values.

It does not publish corrections or modify values.

## Data Flow Summary

Read from Blackboard:
- `ComposerRecommendationsData.WOBRecommendedMaximum`
- `CorrectedRecommendationsData.CorrectedWOBRecommendedMaximum`

Output:
- informational logs showing both values for side-by-side validation.

## Typical Use Case

Run this service together with:
- `DWIS.Service.WOBCorrections.Server` (publishes corrected values)
- source/publisher for composer recommendations

Then inspect logs in `TestADCS` to verify corrected recommendations are present and updated.

## Run Locally

From repository root:

```sh
dotnet run --project DWIS.Service.WOBCorrections.TestADCS
```

Prerequisites:
- reachable DWIS Blackboard endpoint
- valid OPC UA client configuration at:
  - `DWIS.Service.WOBCorrections.TestADCS/config/Quickstarts.ReferenceClient.Config.xml`

## Expected Logs

When values are available, the service logs lines such as:
- `Composer Recommended Max WOB: ...`
- `Corrected Recommended Max WOB: ...`

If one value is missing, only the available line is logged.

## Configuration

The project currently uses base `Configuration` from `DWIS.RigOS.Common.Worker` and standard .NET config sources:
- `appsettings.json`
- `appsettings.Development.json`
- environment variables
- user secrets

Current appsettings files define logging levels only.

## Docker

Build image from repository root:

```sh
docker build -f DWIS.Service.WOBCorrections.TestADCS/Dockerfile -t dwis-wob-testadcs .
```

Run container:

```sh
docker run -d --name dwis-wob-testadcs -v c:\Volumes\DWISTestADCS:/home dwis-wob-testadcs
```

Container entrypoint:
- `dotnet DWIS.Service.WOBCorrections.TestADCS.dll`

## Project Structure

- `Program.cs`: host bootstrap (`AddHostedService<Worker>`).
- `Worker.cs`: Blackboard read loop and logging.
- `config/Quickstarts.ReferenceClient.Config.xml`: OPC UA client settings.
- `appsettings*.json`: logging settings.
- `Dockerfile`: container build/runtime definition.

## Troubleshooting

- If no logs appear, verify Blackboard connection and OPC UA certificate settings.
- If only composer values appear, confirm correction service is running and publishing corrected recommendations.
- If only corrected values appear, confirm composer recommendation publisher is active.

## Packaging

This project targets `net8.0` and includes `README.md` and `LICENSE` in package metadata.
