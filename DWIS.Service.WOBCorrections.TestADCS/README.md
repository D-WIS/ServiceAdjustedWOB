# A Contextual Data Bridge to Publish the BHADrillString on the DWIS Blackboard
This service publishes the bhadrillstring for the currently selected wellbore to the DWIS Blackboard. It fetches trajectories from the OSDC BHADrillString microservice and only publishes when the selected wellbore changes.

## What It Does
- Subscribes to the DWIS Blackboard for the selected wellbore.
- Queries the OSDC BHADrillString microservice for trajectories that match that wellbore.
- Publishes the matching bhadrillstring to the DWIS Blackboard as a contextual data signal.
- Repeats in a loop and reloads configuration periodically.

## Data Flow Summary
1. Read the selected wellbore from the Blackboard.
2. If the wellbore changed, fetch bhadrillstring summaries from the OSDC microservice.
3. Find the bhadrillstring whose `WellBoreID` matches the selected wellbore and fetch the full bhadrillstring by ID.
4. Publish the bhadrillstring to the Blackboard.

## Configuration
The service uses standard .NET configuration sources (appsettings, environment variables, user secrets).

### Required
- `BHADrillStringHostURL`: Base URL for the OSDC BHADrillString microservice.

The service appends `BHADrillString/api/` to this value. Make sure the URL ends with a `/` so the final address is correct.

Example:
```text
BHADrillStringHostURL = https://dev.digiwells.no/
```

### OPC UA Client Configuration
The OPC UA client configuration is provided in:
`config/Quickstarts.ReferenceClient.Config.xml`

The Docker image copies this file into `/app/config`. The current config auto-accepts untrusted certificates, which is intended for development only.

## Running Locally
Run from the project directory:
```sh
dotnet run
```

Ensure the following are reachable:
- DWIS Blackboard OPC UA endpoint
- OSDC BHADrillString microservice (`BHADrillStringHostURL`)

## Docker
### Create a replicated DWIS Blackboard
```sh
docker run -dit --name blackboard -P -p 48030:48030/tcp --hostname localhost digiwells/ddhubserver:latest --useHub --hubURL https://dwis.digiwells.no/blackboard/applications
```

### Run the bhadrillstring bridge
```sh
docker run -dit --name DWISBHADrillStringBridge -v c:\Volumes\DWISContextualDataBHADrillString:/home digiwells/dwiscontextualdatabridgebhadrillstringserver:stable
```

## Project Structure
- `Program.cs`: Host bootstrap.
- `Worker.cs`: Main loop; Blackboard and OSDC interactions.
- `SelectedWellboreData.cs`: Blackboard query model for the selected wellbore.
- `BHADrillStringData.cs`: Blackboard publish model for the bhadrillstring.
- `ConfigurationForOSDC.cs`: Configuration model for this service.

## Troubleshooting
- Verify `BHADrillStringHostURL` has a trailing `/`.
- Check the OPC UA client cert store paths defined in `config/Quickstarts.ReferenceClient.Config.xml`.
- If no bhadrillstring is published, confirm the selected wellbore contains a valid `WellBoreID` and the OSDC service exposes a matching bhadrillstring.

## Notes
- This service only publishes on wellbore changes; it does not continuously republish unchanged trajectories.
- The OSDC BHADrillString microservice endpoint is derived from `BHADrillStringHostURL` and the fixed suffix `BHADrillString/api/`.
