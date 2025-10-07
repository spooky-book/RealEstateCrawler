# RealEstateCrawler

A maintainable .NET template for experimenting with a Playwright-powered crawler targeting [realestate.com.au](https://www.realestate.com.au/).

## Features

- **Generic host + dependency injection** via `Microsoft.Extensions.Hosting` for easy service composition.
- **Typed configuration** for crawler, Playwright, and storage behaviour, sourced from `appsettings.json`, environment variables, or command-line overrides.
- **Playwright integration** with a reusable browser factory to handle lifecycle management.
- **Rich data contract** capturing core real-estate listing attributes that can be extended over time.
- **JSON Lines persistence** (`.ndjson`) for piping into downstream tooling or analytics.
- **Dry-run mode** to validate the pipeline without hitting the live site.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Playwright CLI dependencies](https://playwright.dev/dotnet/docs/intro#installing-browsers) (run `pwsh bin/Debug/net8.0/playwright.ps1 install` after the first build or `npx playwright install` if you have Node.js available).

## Getting started

1. Restore and build the project:
   ```bash
   dotnet build
   ```

2. (First run only) install Playwright browsers:
   ```bash
   pwsh bin/Debug/net8.0/playwright.ps1 install
   ```

3. Adjust `appsettings.json` to include the suburbs and limits you care about. Key sections:
   - `Crawler`: base URL, throttling, dry-run toggle, and suburb list
   - `Playwright`: browser flavour, headless setting, navigation timeout
   - `Storage`: output directory and filename pattern tokens (`{suburb}`, `{state}`, `{timestamp}` or `{timestamp:format}`)

4. Execute the crawler:
   ```bash
   dotnet run
   ```

   During dry-run mode (`Crawler:DryRun = true`) the crawler emits a synthetic listing per suburb for pipeline validation. Set it to `false` when you are ready to scrape the live site.

### Configuration overrides

- Environment variables prefixed with `REA_CRAWLER_` will override config values (e.g. `REA_CRAWLER__Crawler__DryRun=false`).
- Additional configuration sources can be added via `Program.cs` if you prefer user secrets, Azure Key Vault, etc.

## Extending the template

- Implement pagination inside `RealEstateComAuCrawler.ExtractListingUrlsAsync` to walk through multiple list pages.
- Flesh out `ScrapeListingAsync` with resilient selectors, structured parsing (potentially using ChatGPT or other NLP tools for descriptions), and error handling.
- Replace `JsonListingRepository` with a database-backed repository if you want durable storage beyond NDJSON.
- Introduce rate limiting, rotating proxies, or captcha handling in the crawler service as needed.

## Caveats

- The provided selectors are indicative and may need to be updated to match the live DOM structure. Use Playwright's inspector/devtools to verify.
- Respect the target website's terms of service and robots.txt. Apply appropriate throttling to avoid overwhelming their infrastructure.
