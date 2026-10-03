# Reviewed generator contracts

`Contracts/` contains 69 fixed expectations captured after successful Roslyn/Scriban comparison on the parent customization commit (7478fc6), before Roslyn retirement: three sample profiles/targets, two full-spec union modes, and all 64 configuration combinations. Each captures owned file inventory, normalized public APIs or generated test coverage, and operation query values. LF normalization applies only to query snapshot comparison; output/runtime newline tests remain separate.

Tests read these fixtures and never rewrite them. Investigate failures, review intended contract changes, and update only the affected expectation alongside the corresponding behavior test. Compiler/runtime/real server/package checks complement these source contract fingerprints.

`LegacyOwnership.json` stores a portable version-1 ownership artifact and its exact hashed source/input files. The upgrade test reconstructs it in a temporary directory, checks tamper rejection, and verifies migration and preservation of an unowned consumer file. It is historical migration data, not a second generator or sample project. Preserve exact bytes when changing fixture storage.
