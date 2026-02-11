# DWIS.Service.WOBCorrections

Solution for calibrating and correcting surface/downhole WOB measurements, then propagating corrected limits through advisor/composer/ADCS-style flows on the DWIS Blackboard.

## Solution Overview

This repository contains:
- core calibration/correction model logic
- synthetic measurement generation and replay tools
- the main correction server worker
- helper workers to simulate upstream/downstream components
- model/code generation tooling for shared OpenAPI-based contracts

All projects target `.NET 8`.

## Architecture at a Glance

High-level flow:
1. `TestSources` publishes synthetic top-side + downhole measurements.
2. `Server` ingests measurements and computes corrected outputs.
3. `TestAdvisor` consumes corrected WOB and publishes an advisor max limit.
4. `TestComposer` forwards advisor max limit as composer recommendation.
5. `Server` applies correction to composer recommendation and republishes corrected recommendation.
6. `TestADCS` monitors original vs corrected recommendation values.

Offline validation path:
- `ModelTest` generates datasets and fit diagnostics (JSON/CSV) to evaluate calibration behavior.

## Projects

- `DWIS.Service.WOBCorrections.Model`
  - Core domain models and calibration/correction engine (`CalibratorCorrector`).
- `DWIS.Service.WOBCorrections.MeasurementGeneration`
  - Synthetic drilling measurement generator (top-side + downhole series).
- `DWIS.Service.WOBCorrections.Server`
  - Main worker service that reads measurements/recommendations and publishes corrected outputs.
- `DWIS.Service.WOBCorrections.TestSources`
  - Synthetic source injector for top-side/downhole signals.
- `DWIS.Service.WOBCorrections.TestAdvisor`
  - Helper worker that produces advisor max WOB from corrected WOB.
- `DWIS.Service.WOBCorrections.TestComposer`
  - Helper worker that simulates composer recommendation forwarding.
- `DWIS.Service.WOBCorrections.TestADCS`
  - Monitor helper that verifies corrected recommendations are visible downstream.
- `DWIS.Service.WOBCorrections.ModelTest`
  - Console test harness exporting calibration and replay diagnostics.
- `DWIS.Service.WOBCorrections.ModelSharedOut`
  - OpenAPI merge/codegen tool for shared model/client artifacts.

## Quick Start (End-to-End)

Prerequisites:
- DWIS Blackboard available/reachable
- valid OPC UA client config for each worker (`config/Quickstarts.ReferenceClient.Config.xml` in each service project)

Run in separate terminals from repository root:

```sh
dotnet run --project DWIS.Service.WOBCorrections.TestSources
dotnet run --project DWIS.Service.WOBCorrections.Server
dotnet run --project DWIS.Service.WOBCorrections.TestAdvisor
dotnet run --project DWIS.Service.WOBCorrections.TestComposer
dotnet run --project DWIS.Service.WOBCorrections.TestADCS
```

For offline calibration diagnostics:

```sh
dotnet run --project DWIS.Service.WOBCorrections.ModelTest
```

## Build

Build the full solution:

```sh
dotnet build DWIS.Service.WOBCorrections.sln
```

## Project Documentation

Each project has its own detailed README:
- `DWIS.Service.WOBCorrections.Model/README.md`
- `DWIS.Service.WOBCorrections.MeasurementGeneration/README.md`
- `DWIS.Service.WOBCorrections.Server/README.md`
- `DWIS.Service.WOBCorrections.TestSources/README.md`
- `DWIS.Service.WOBCorrections.TestAdvisor/README.md`
- `DWIS.Service.WOBCorrections.TestComposer/README.md`
- `DWIS.Service.WOBCorrections.TestADCS/README.md`
- `DWIS.Service.WOBCorrections.ModelTest/README.md`
- `DWIS.Service.WOBCorrections.ModelSharedOut/README.md`
