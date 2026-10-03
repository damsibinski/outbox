# Contributing to Outbox

Thank you for considering a contribution. Outbox is a small, focused library —
keep changes easy to review and aligned with its scope: transactional outbox
persistence and background processing for .NET.

## Before you start

- Search existing GitHub issues for duplicates before opening a new one.
- For larger changes (new providers, API redesign, retry or dead-letter
  behavior), open an issue first so we can agree on direction.
- Outbox is pre-release. Public APIs may still change; breaking changes belong
  in [CHANGELOG.md](CHANGELOG.md).

## Development setup

Requirements:

- .NET 10 SDK
- Docker (for integration tests only)

Clone the repository and build the solution:

```bash
git clone <repository-url>
cd outbox
dotnet build Outbox.slnx
```

## Running tests

Unit tests:

```bash
dotnet test tests/Outbox.Tests/Outbox.Tests.csproj
```

Integration tests use Testcontainers and start SQL Server, PostgreSQL, and
MySQL locally. Docker must be running:

```bash
dotnet test tests/Outbox.IntegrationTests/Outbox.IntegrationTests.csproj
```

Run the full test suite before opening a pull request:

```bash
dotnet test Outbox.slnx
```

## Pull requests

1. Fork the repository and create a branch from `main`.
2. Make focused changes — one concern per pull request when possible.
3. Add or update tests for behavior you change.
4. Update [README.md](README.md) when public usage changes.
5. Add an entry to [CHANGELOG.md](CHANGELOG.md) for user-visible changes.
6. Ensure `dotnet build Outbox.slnx` and `dotnet test Outbox.slnx` pass.

Describe what changed and why in the pull request body. Link related issues
when applicable.

## Provider changes

SQL Server, PostgreSQL, and MySQL share the same acceptance scenarios in
`tests/Outbox.IntegrationTests`. When you change one provider, keep behavior
consistent across all three unless the pull request documents an intentional,
provider-specific difference.

Schema changes belong next to the provider project
(`src/Outbox.{Provider}/OutboxMessages.sql`).

## Scope

Contributions that fit the project:

- bug fixes and test coverage;
- documentation improvements;
- provider fixes or parity improvements;
- small, well-motivated API improvements.

Out of scope for now:

- broker-specific abstractions (Service Bus, Kafka adapters, and similar);
- enterprise features that add operational complexity without clear benefit to
  small teams;
- changes that require heavy new dependencies.

When in doubt, open an issue.

## Code style

Match the existing codebase:

- functional components and clear naming over clever abstractions;
- minimal diff — solve the problem without unrelated refactors;
- English identifiers and comments where comments are needed;
- async/await for I/O; no blocking calls in library paths.

## License

By contributing, you agree that your contributions are licensed under the
[MIT License](LICENSE.md), the same license as the project.
