# Project verification policy

The human updated the release policy on 2026-10-08:

- For fixes and feature changes within the same minor series (for example 0.11.0 to 0.11.1), run Standard verification: build, unit tests, relevant native checks, and the 600-second short stress test. Do not automatically run Full or another long soak for these updates.
- For a cross-minor release (for example 0.11 to 0.12), Full verification may include the long soak. An explicit human instruction to stop or skip it overrides the default.
- Record the evidence honestly. A stopped long run is stopped, never PASS; a Standard PASS is not a Full PASS. Select the release gate for the actual update type rather than repeatedly running long tests for small changes.
- Keep user data out of Git. Preserve existing work, use isolated profiles for diagnostics, and retain verified backups and a rollback entry before installation.

The 0.11 Full run `20261008-132410-32235995` had already completed successfully before the stop request was received. Reuse its matching sealed evidence; do not rerun it.
