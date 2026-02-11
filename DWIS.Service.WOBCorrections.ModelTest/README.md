# DWIS.Service.WOBCorrections.ModelTest

Console test harness for validating automatic taring and correction of downhole and surface WOB signals.

This project combines:
- synthetic measurement generation (`DWIS.Service.WOBCorrections.MeasurementGeneration`)
- calibration/correction logic (`DWIS.Service.WOBCorrections.Model`)

and produces JSON/CSV artifacts for analysis.

## Purpose

`ModelTest` runs deterministic simulation scenarios, calibrates model terms (alpha, beta, C, D), replays correction, and exports intermediate and final diagnostics.

It is intended for:
- regression checks on calibration behavior
- comparing error metrics across scenarios
- investigating sensor artifact handling (`Td`, `Tp`, `Tdl` paths)

## Scenarios Included

The program currently executes three scenarios:
1. `series_01_no_artifacts_at_bit_sensor`
2. `series_02_deadline_artifact_only`
3. `series_03_loadpin_artifact_only`

`runsPerScenario` is set to `1` by default in `Program.cs`.

## What the Program Does

For each scenario/run:
1. Build initial simulation state at UTC `2026-01-01T00:00:00Z`.
2. Generate top-side and downhole measurements.
3. Save full simulation JSON.
4. Export time-series CSVs (`topside_1s`, `downhole_10s`).
5. Extract calibration situations (`alpha`, `beta`, `C`, `D` contexts).
6. Run batch/streaming calibration fits and export fit-difference CSVs.
7. Replay correction and export `wob_log.csv`.
8. Print summary statistics (count, mean error, std error, RMSE) to console.

## Run

From repository root:

```sh
dotnet run --project DWIS.Service.WOBCorrections.ModelTest
```

Output files are written to:
- `DWIS.Service.WOBCorrections.ModelTest/bin/Debug/net8.0/`

Filenames include:
- scenario name
- run index (`run01`)
- UTC timestamp (`yyyyMMdd_HHmmss`)

## Generated Artifacts

Each scenario produces a set of files, including:
- `<scenario>_runXX_<stamp>.json`
- `<scenario>_runXX_<stamp>_topside_1s.csv`
- `<scenario>_runXX_<stamp>_downhole_10s.csv`
- `<scenario>_runXX_<stamp>_offbottom_rotating_bha.csv`
- `<scenario>_runXX_<stamp>_offbottom_rotating_beta_td.csv`
- `<scenario>_runXX_<stamp>_offbottom_rotating_beta_tp.csv`
- `<scenario>_runXX_<stamp>_offbottom_rotating_beta_tdl.csv`
- `<scenario>_runXX_<stamp>_in_slips_contexts.csv`
- `<scenario>_runXX_<stamp>_in_slips_tp_contexts.csv`
- `<scenario>_runXX_<stamp>_alpha_fit_diff.csv`
- `<scenario>_runXX_<stamp>_alpha_stream_fit_diff.csv`
- `<scenario>_runXX_<stamp>_beta_td_stream_fit_diff.csv`
- `<scenario>_runXX_<stamp>_beta_tp_stream_fit_diff.csv`
- `<scenario>_runXX_<stamp>_beta_tdl_stream_fit_diff.csv`
- `<scenario>_runXX_<stamp>_d_stream_fit_diff.csv`
- `<scenario>_runXX_<stamp>_c_stream_fit_diff.csv`
- `<scenario>_runXX_<stamp>_wob_log.csv`

## CSV Format Notes

- CSV separator is semicolon (`;`).
- Numeric formatting uses Norwegian culture formatting in writers.
- Timestamps are exported as ISO-8601 UTC strings.

## Console Metrics

The program prints, per scenario:
- sample counts (`topside`, `downhole`)
- generated file paths
- fit summaries for:
  - alpha batch and streaming
  - beta streaming (`Td`, `Tp`, `Tdl`)
  - D streaming
  - C streaming

Reported metrics include:
- `n`
- `meanErr`
- `stdErr`
- `rmse`

## Project Structure

- `Program.cs`: end-to-end scenario execution, export, calibration replay, and reporting.
- `README.md`: usage and output reference.
- `LICENSE`: license file for packaging.

## Dependencies

Project references:
- `DWIS.Service.WOBCorrections.MeasurementGeneration`
- `DWIS.Service.WOBCorrections.Model`

## Packaging

This project is `net8.0` and includes `README.md` and `LICENSE` in package metadata.
