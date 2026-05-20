# Contributing to OrionKey

Thank you for considering a contribution. OrionKey is a single-maintainer project, so clear, focused contributions that carry their own tests and documentation land the fastest. This guide tells you what "good" looks like here, and what will bounce.

## Table of contents

- [Code of conduct](#code-of-conduct)
- [Before you start](#before-you-start)
- [Ways to contribute](#ways-to-contribute)
- [Local development setup](#local-development-setup)
- [Repository layout](#repository-layout)
- [Coding standards](#coding-standards)
- [Testing rules](#testing-rules)
- [Documentation requirements](#documentation-requirements)
- [Commit & PR conventions](#commit--pr-conventions)
- [Security issues](#security-issues)
- [Release process](#release-process)
- [License](#license)

## Code of conduct

Be kind, be specific, be technical. Attack ideas, not people. If a behaviour would feel out of place on a mature .NET project (e.g. dotnet/runtime), it is out of place here too.

## Before you start

1. **Search first.** Check [existing issues](https://github.com/tunahanaliozturk/OrionKey/issues) and open pull requests. Your idea may already be in motion.
2. **Discuss non-trivial changes in an issue before coding.** Anything touching the public API, the source generator's emitted output, a new ID strategy, or a cross-cutting refactor needs an issue with a short design sketch first. This protects your time — the maintainer may redirect the approach before you have written 500 lines.
3. **Trivial changes can go straight to a PR.** Typos, README fixes, new test cases for existing behaviour, XML doc clarifications — no issue required.

## Ways to contribute

| Kind | Expectation |
|------|-------------|
| Bug fix | Repro test first; fix second. |
| New ID strategy | New public API → discuss in an issue first. Include the runtime generator, the generator wiring, XML docs, tests, and a CHANGELOG entry. |
| Source generator work | Emitter changes need at least one generator test asserting the exact emitted text shape. |
| Diagnostic work | New `ORIONKEY` diagnostics need a test that triggers them and an entry in the analyzer release-tracking files. |
| Performance work | Include a BenchmarkDotNet run before/after. Numbers only — no hand-waving. |
| Documentation | Fine as a standalone PR. Keep it terse and technical. |

## Local development setup

**Toolchain:**

- .NET SDK 10.0 (also test against 8.0 and 9.0 if you can — the libraries multi-target).
- Git 2.30+.
- Any editor with C# + Roslyn support (Visual Studio, Rider, VS Code + C# Dev Kit).

**Clone & build:**

```bash
git clone https://github.com/tunahanaliozturk/OrionKey.git
cd OrionKey
dotnet build Moongazing.OrionKey.sln -c Release
```

**Run the full test suite:**

```bash
dotnet test Moongazing.OrionKey.sln -c Release
```

All tests must pass on your branch before you open a PR.

**Run the benchmarks (optional, for perf PRs):**

```bash
dotnet run -c Release --project bench/Moongazing.OrionKey.Benchmarks
```

**Run the sample:**

```bash
dotnet run --project sample/Moongazing.OrionKey.Sample -c Release
```

The sample exercises every ID strategy. If you add a user-visible feature, wire a line into the sample too.

## Repository layout

```
OrionKey/
├── src/
│   ├── Moongazing.OrionKey/             runtime + bundled generator (PackageId: OrionKey)
│   ├── Moongazing.OrionKey.Generators/  Roslyn incremental generator (not packed standalone)
│   └── Moongazing.OrionKey.Testing/     deterministic test generators (PackageId: OrionKey.Testing)
├── tests/
│   ├── Moongazing.OrionKey.Tests/             runtime unit tests
│   ├── Moongazing.OrionKey.Generators.Tests/  generator + diagnostics tests
│   ├── Moongazing.OrionKey.IntegrationTests/  JSON / EF Core / minimal-API round-trips
│   └── Moongazing.OrionKey.Testing.Tests/     OrionKey.Testing tests
├── bench/                               BenchmarkDotNet harness
├── sample/                              Console app exercising every strategy
├── docs/                                Design spec, plan, Snowflake guide
├── CHANGELOG.md                         Keep-a-Changelog format
└── README.md
```

The `OrionKey` NuGet package bundles the generator assembly as an analyzer (`analyzers/dotnet/cs`); `Moongazing.OrionKey.Generators` is not published on its own. The C# namespace is flat — every runtime type lives in `Moongazing.OrionKey`. Published NuGet PackageIds drop the `Moongazing.` prefix (`OrionKey`, `OrionKey.Testing`).

## Coding standards

- **Target frameworks:** the runtime and Testing libraries multi-target `net8.0;net9.0;net10.0`. The source generator project is `netstandard2.0` (Roslyn constraint). Do not break the older TFMs without discussion.
- **Nullable reference types:** enabled project-wide. No `#nullable disable` — if the compiler complains, fix the code, not the pragma.
- **Warnings are errors.** `TreatWarningsAsErrors=true` is set in `Directory.Build.props`. Disabling it is never acceptable in a PR.
- **XML docs on every public member.** `GenerateDocumentationFile=true` is on for packable projects. Missing docs fail the build.
- **Analyzers:** `AnalysisLevel=latest-recommended`. The generator project additionally has `EnforceExtendedAnalyzerRules=true` — use `static` lambdas in incremental pipelines and follow the RS-series analyzer guidance.
- **File-scoped namespaces.**
- **One type per file**, unless it is a small private nested type.
- **No unrelated refactors in a feature PR.** Keep the diff focused.

## Testing rules

- Every behaviour change needs a test. No exceptions for "it is obvious".
- Tests live by area: runtime → `tests/Moongazing.OrionKey.Tests/`; generator and diagnostics → `tests/Moongazing.OrionKey.Generators.Tests/`; end-to-end → `tests/Moongazing.OrionKey.IntegrationTests/`; the Testing package → `tests/Moongazing.OrionKey.Testing.Tests/`.
- **Test naming:** `<Method>_Should<ExpectedOutcome>_When<Condition>`. Legible at `dotnet test --filter` time.
- Write the failing test before the fix when practical. For generator emitter changes it is almost always practical.
- No flaky timing assertions — use ranges (`Assert.InRange`) or before/after clock snapshots. Short `Thread.Sleep` calls are tolerated only where a test genuinely needs two distinct millisecond timestamps (e.g. ULID / GuidV7 sort-order tests).
- Generator tests assert the emitted text shape — `Assert.Contains("expected snippet", generatedSource)`. Do not snapshot entire emitted files; they are too brittle.
- Tests that swap process-global state (`OrionKey.Configure`, `DeterministicIdScope`) must run in a non-parallel xUnit collection.

## Documentation requirements

When you change user-visible behaviour:

1. **CHANGELOG.md** — add a bullet under the appropriate Keep-a-Changelog section (`### Added` / `### Changed` / `### Fixed` / `### Deprecated`).
2. **README.md** — if the change surfaces a new public entry point or strategy, update the relevant snippet or the strategy table.
3. **Per-package README** (`src/Moongazing.OrionKey/docs/README.md` or `src/Moongazing.OrionKey.Testing/docs/README.md`) — update if the package surface changed.
4. **XML docs** on the public API itself.
5. **Sample** (`sample/Moongazing.OrionKey.Sample/`) — if the feature is user-visible and demonstrable in a few lines, add it.

## Commit & PR conventions

**Commit messages** follow [Conventional Commits](https://www.conventionalcommits.org/):

- `feat(orionkey): add a NanoId custom-alphabet option`
- `fix(orionkey): GuidV7 net8 polyfill uses RFC big-endian layout`
- `docs(readme): document the EF Core ValueConverter emission`
- `test(orionkey): cover the ORIONKEY004 incompatible-strategy diagnostic`
- `bench(orionkey): id-generation throughput benchmarks`
- `chore(release): bump to 0.2.0`

**Pull requests:**

- One logical change per PR. Multiple unrelated fixes → multiple PRs.
- **PR title:** same style as a commit subject.
- **PR body:** why (link the issue or explain the problem in 2-3 sentences), what (high-level summary), how you tested (which test files, which commands, benchmark numbers for perf PRs).
- **Keep the PR green.** The CI pipeline builds and tests on .NET 8, 9, and 10 — all three legs must pass.
- **Squash-merge** is the default.

## Security issues

**Do not open public issues for security vulnerabilities.**

Email the maintainer directly — see the `<Authors>` field in `Directory.Build.props` for the current contact. Include a minimal reproduction, the affected versions, and your disclosure expectations.

## Release process

Releases are cut by the maintainer only. The flow is:

1. All planned changes merged to `main`.
2. Version bumped in `Directory.Build.props` in a single `chore(release):` commit.
3. CHANGELOG moves the unreleased block under the new version header with an ISO date.
4. A GitHub release is published; the CI/CD pipeline packs `OrionKey` and `OrionKey.Testing` and pushes them to NuGet.org and GitHub Packages.

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE.txt), the same licence the project ships under. No CLA required.

---

Thanks again. Contributions of any size are welcome — even a single test case that covers an overlooked edge case is a real improvement.
