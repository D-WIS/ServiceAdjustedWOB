# DWIS.Service.WOBCorrections.ModelSharedOut

Tooling project that builds a merged OpenAPI model and generates C# shared model/client classes for WOB Corrections.

This project is an executable (`net8.0`) and is intended to be run when schema dependencies change.

## Purpose

The generator reads multiple dependency OpenAPI schemas from local JSON files, merges them into one OpenAPI bundle, normalizes schema references, and generates:
- a merged OpenAPI JSON document for service exposure
- a generated C# model/client file for shared consumption by other projects

## Inputs

Local schema dependencies must be placed in:
- `DWIS.Service.WOBCorrections.ModelSharedOut/json-schemas/*.json`

Current repository includes:
- `Cluster.json`
- `DrillString.json`
- `Field.json`
- `Well.json`
- `WellBore.json`

## Outputs

Running the generator creates/overwrites:
- `DWIS.Service.WOBCorrections.ModelSharedOut/WOBCorrectionsMergedModel.cs`
- `DWIS.Service.WOBCorrections.Service/wwwroot/json-schema/WOBCorrectionsMergedModel.json`

## What Happens During Generation

1. Locate the solution root (searches upward for `*.sln`).
2. Load all `json-schemas/*.json` OpenAPI files.
3. Merge `paths` and `components.schemas` into one `OpenApiDocument`.
4. Normalize schema keys to short names (namespace removed).
5. Recursively rewrite schema references (`$ref`) to the normalized keys.
6. Serialize merged OpenAPI JSON.
7. Apply compatibility patch from OpenAPI `3.0.4` to `3.0.3` for current Swagger UI tooling compatibility.
8. Generate C# output via NSwag (`System.Text.Json`, DTOs + client classes enabled).

## Run

From repository root:

```sh
dotnet run --project DWIS.Service.WOBCorrections.ModelSharedOut
```

If output files already exist, the tool prompts before overwrite. Type `Y` to continue.

## Generated Code Notes

- `WOBCorrectionsMergedModel.cs` is auto-generated (NSwag).
- Namespace used for generated types: `DWIS.Service.WOBCorrections.ModelShared`.
- Type names are normalized to short names via `CustomTypeNameGenerator`.
- Manual edits to generated files are expected to be overwritten on next run.

## Project Structure

- `Program.cs`: entry point and generation pipeline.
- `OpenApiSchemaReferenceUpdater.cs`: schema merge + recursive reference update logic.
- `WOBCorrectionsMergedModel.cs`: generated C# model/client output.
- `json-schemas/`: source OpenAPI dependency schemas used for merge.

## Dependencies

Key packages:
- `Microsoft.OpenApi.Readers`
- `NSwag.CodeGeneration.CSharp`

## Troubleshooting

- Ensure `json-schemas` exists and contains valid OpenAPI JSON documents.
- If generation fails, verify schema compatibility and duplicate short schema names after namespace stripping.
- If service bundle is not produced, ensure target directory exists:
  - `DWIS.Service.WOBCorrections.Service/wwwroot/json-schema`
- Re-run generation after schema updates in dependencies.

## Packaging

This project includes `README.md` and `LICENSE` in package metadata.
