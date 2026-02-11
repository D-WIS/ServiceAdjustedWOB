# DWIS.Service.WOBCorrections.TestComposer

Helper worker service that simulates composer-side processing by forwarding advisor WOB limits into composer recommendation signals on the DWIS Blackboard.

## What It Does

On each loop tick, the service:
1. Reads advisor recommendations from the Blackboard.
2. Copies advisor max WOB to composer max WOB.
3. Publishes composer recommendations back to the Blackboard.
4. Logs both advisor and composer values.

Current rule in `Worker.cs`:
- `ComposerRecommendationsData.WOBRecommendedMaximum = AdvisorRecommendationsData.WOBMaxLimit`

## Data Flow Summary

Reads from Blackboard:
- `AdvisorRecommendationsData.WOBMaxLimit`

Publishes to Blackboard:
- `ComposerRecommendationsData.WOBRecommendedMaximum`

This makes it useful for validating the advisor -> composer -> correction-service chain end to end.

## Typical Use Case

Run this service together with:
- `DWIS.Service.WOBCorrections.TestAdvisor` (publishes advisor WOB limit)
- `DWIS.Service.WOBCorrections.Server` (consumes composer recommendation and publishes corrected recommendation)
- `DWIS.Service.WOBCorrections.TestADCS` (observes corrected recommendation)

## Run Locally

From repository root:

```sh
dotnet run --project DWIS.Service.WOBCorrections.TestComposer
```

Prerequisites:
- reachable DWIS Blackboard endpoint
- valid OPC UA client configuration at:
  - `DWIS.Service.WOBCorrections.TestComposer/config/Quickstarts.ReferenceClient.Config.xml`

## Expected Logs

When values are available, logs include:
- `Advisor Recommended Max WOB: ...`
- `Composer Recommended Max WOB: ...`

## Configuration

The service uses base `Configuration` from `DWIS.RigOS.Common.Worker` and standard .NET configuration sources:
- `appsettings.json`
- `appsettings.Development.json`
- environment variables
- user secrets

Current appsettings define logging levels only.

## Docker

Build image from repository root:

```sh
docker build -f DWIS.Service.WOBCorrections.TestComposer/Dockerfile -t dwis-wob-testcomposer .
```

Run container:

```sh
docker run -d --name dwis-wob-testcomposer -v c:\Volumes\DWISTestComposer:/home dwis-wob-testcomposer
```

Container entrypoint:
- `dotnet DWIS.Service.WOBCorrections.TestComposer.dll`

## Project Structure

- `Program.cs`: host bootstrap (`AddHostedService<Worker>`).
- `Worker.cs`: Blackboard read/transform/publish loop.
- `config/Quickstarts.ReferenceClient.Config.xml`: OPC UA client settings.
- `appsettings*.json`: logging settings.
- `Dockerfile`: container build/runtime definition.

## Troubleshooting

- If composer values are missing, verify advisor publishes `WOBMaxLimit`.
- If logs are empty, check Blackboard connectivity and OPC UA certificate settings.
- If values do not propagate, ensure query registration and publish permissions are valid in your Blackboard setup.

## Packaging

This project targets `net8.0` and includes `README.md` and `LICENSE` in package metadata.
