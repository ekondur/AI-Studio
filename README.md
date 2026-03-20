# AI Studio

[![Codacy Badge](https://app.codacy.com/project/badge/Grade/400ac43e51b04f3fb2f335c1688b8d4b)](https://app.codacy.com/gh/ekondur/AI-Studio/dashboard?utm_source=gh&utm_medium=referral&utm_content=&utm_campaign=Badge_grade)

AI Studio is a Visual Studio 2022 extension that adds AI-assisted code generation, refactoring, documentation, testing, and security analysis to the IDE directly from the context menu.

- [Download from Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=ekondur.AI-Studio)
- [Report an issue or request a feature](https://github.com/ekondur/AI-Studio/issues)

![AI Studio Preview](https://user-images.githubusercontent.com/4971326/234110009-382af5bf-9bc8-4bec-892b-90bf66b03fa3.png)

## Table of Contents

- [Highlights](#highlights)
- [Requirements](#requirements)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Command Cheat Sheet](#command-cheat-sheet)
- [Feature Walkthrough](#feature-walkthrough)
- [Configuration](#configuration)
- [Advanced Customization](#advanced-customization)
- [Troubleshooting and Feedback](#troubleshooting-and-feedback)
- [License](#license)

## Highlights

- Seamless right-click workflow across C#, XAML, and most text-based files inside Visual Studio.
- Supports **OpenAI**, **Anthropic (Claude)**, and **Ollama** (local models) — switch provider from the Options page.
- Generates or updates code while preserving formatting, documentation, and project conventions.
- Built-in commands for documentation, explanations, security review, and unit test creation keep teams in flow.
- Integrated Output tool window now supports follow-up chat with a VS-themed input bar and smooth streaming updates without focus jumps.

## Requirements

- Visual Studio 2022 17.6 or later with the VSIX extension workload installed.
- An API key for your chosen provider (not required for Ollama) with network access from the IDE.
- .NET Framework 4.8 SDK (installed with Visual Studio) for local builds.

## Installation

### Marketplace (recommended)

1. Open Visual Studio and choose `Extensions > Manage Extensions`.
2. Search for **AI Studio**.
3. Click **Download** and restart Visual Studio to complete the installation.

### Build from source

1. Clone or fork this repository.
2. Open `AI Studio.sln` in Visual Studio 2022.
3. Build the solution in `Release` mode.
4. Double-click the generated `.vsix` under `bin\Release` to install it into your local instance.

## Quick Start

1. **Choose a provider** and obtain an API key if required (see the [Provider Setup](#provider-setup) section below).
2. **Configure AI Studio** via `Tools > Options > AI Studio > General`: set the **AI Provider**, paste the key, and enter the model name.
3. **Pick a feature** (for example, Code It) by selecting code, right-clicking, and choosing the desired AI Studio command.

![Configuration Screenshot](https://github.com/user-attachments/assets/ca2b6228-23a9-44fd-8a82-0ee6aca8bd66)

## Provider Setup

AI Studio supports three providers out of the box. Switch between them at any time under `Tools > Options > AI Studio > General`.

### OpenAI

| Setting | Value |
| --- | --- |
| AI Provider | `OpenAI` |
| API Key | Your key from [platform.openai.com/account/api-keys](https://platform.openai.com/account/api-keys) |
| Language Model | e.g. `gpt-4o-mini`, `gpt-4o`, `o4-mini` |
| API Endpoint | `https://api.openai.com/v1/` (default) |

### Anthropic (Claude)

| Setting | Value |
| --- | --- |
| AI Provider | `Anthropic` |
| API Key | Your key from [console.anthropic.com/settings/keys](https://console.anthropic.com/settings/keys) |
| Language Model | e.g. `claude-sonnet-4-5`, `claude-opus-4-5`, `claude-haiku-4-5` |
| API Endpoint | *(not used — handled by the SDK)* |

### Ollama (local models, no API key required)

[Ollama](https://ollama.com) runs models locally on your machine. No API key or internet access is required.

| Setting | Value |
| --- | --- |
| AI Provider | `Ollama` |
| API Key | *(leave empty)* |
| Language Model | e.g. `llama3.2`, `mistral`, `codestral` |
| API Endpoint | `http://localhost:11434/v1/` (default) |

Pull a model first with `ollama pull llama3.2`, then start Ollama before using AI Studio commands.

### Using Any Other OpenAI-Compatible Endpoint

Select `OpenAI` as the provider and change the **API Endpoint** to your private or self-hosted URL (e.g. `https://llm.internal/api/v1`). Enter the API key issued by that gateway and set the appropriate model name.

## Command Cheat Sheet

| Command | When to use | Output |
| --- | --- | --- |
| Code It | Turn TODOs or signatures into working code. | Inserts generated implementation inline. |
| Add Comments | Document existing logic without manual XML comments. | Adds inline comments or summaries. |
| Add Summary | Produce XML doc comments for public APIs. | Generates `<summary>` and related tags. |
| Refactor | Improve readability or performance of selected code. | Replaces the selection with an optimized version. |
| Explain | Understand unfamiliar code quickly. | Displays a plain-language explanation. |
| Security Check | Inspect code for risky patterns and mitigations. | Lists potential vulnerabilities plus suggestions. |
| Add Unit Tests | Generate unit tests tailored to the selected method. | Creates a new test class or method snippet. |

## Feature Walkthrough

### Code It

1. Place the caret on an empty method or select a stub.
2. Right-click and choose `AI Studio > Code It`.
3. Review the generated implementation and accept or adjust as needed.

### Add Comments

1. Highlight the code you want documented.
2. Run `AI Studio > Add Comments`.
3. AI Studio adds concise inline comments without disturbing formatting.

### Refactor

1. Select a method or block.
2. Choose `AI Studio > Refactor`.
3. Compare the result with the original and apply the pieces you want to keep.

### Add Summary

1. Select a method header or type declaration.
2. Run `AI Studio > Add Summary`.
3. Automatically generates XML documentation that matches the signature.

### Explain

1. Highlight unfamiliar code.
2. Choose `AI Studio > Explain`.
3. A popup summarizes what the code does and why.

### Security Check

1. Select code that handles I/O, crypto, or user data.
2. Run `AI Studio > Security Check`.
3. Review the flagged issues and suggested mitigations.

### Unit Test Generation

1. Highlight a method to test.
2. Run `AI Studio > Add Unit Tests`.
3. AI Studio generates arrange/act/assert scaffolding tailored to the method.

#### Test Configuration

Configure test generation under `Tools > Options > AI Studio > Unit Test`.

![Unit Test Settings](https://user-images.githubusercontent.com/4971326/232892595-9e304843-8b0d-4420-b058-a0f44688f46e.png)

- **Custom instructions** let you specify frameworks (xUnit, NUnit, MSTest), naming rules, or mocking preferences.
- **Response behavior** controls how aggressively tests are regenerated versus appended.

## Configuration

All configuration lives under `Tools > Options > AI Studio`:

- **General**
  - Paste your API key, pick the default model, and decide whether to format AI changes automatically.
  - Toggle telemetry/diagnostics and adjust temperature or response size if needed.
- **Commands**
  - Override the system prompt for each command, enforce coding guidelines, or switch to a custom model per command.
- **Unit Test**
  - Choose the target test framework, namespace, class name template, and add reusable instructions for deterministic tests.

## Advanced Customization

- **Custom prompts per command**: Tailor Refactor to focus on performance while Configure Comments to prioritize XML docs using `Tools > Options > AI Studio > Commands`.
- **Bring your own endpoint**: Point the extension at Azure OpenAI, a private/self-hosted LLM gateway, or any OpenAI-compatible proxy by entering the base URL and model ID in the General page.
- **Formatting control**: Enable *Format changed text* to run Visual Studio formatting on every AI edit to keep diffs clean.
- **Response behavior**: Decide whether AI should insert results inline, append to the Output window first, or prompt for confirmation.

![Command Settings](https://github.com/ekondur/AI-Studio/assets/4971326/0b49f17d-fa00-40dd-a1d3-ff8aa7e43f2d)

## Troubleshooting and Feedback

- Verify your API key and quota if requests fail; the Output tool window surfaces errors returned by the provider.
- Ensure Visual Studio can reach the OpenAI endpoint (corporate proxies may need to allowlist it).
- Capture screenshots/logs and [open an issue](https://github.com/ekondur/AI-Studio/issues) for bugs or ideas.
- Contributions are welcome via pull requests; please include before/after screenshots for UI tweaks.

## License

AI Studio is released under the [MIT License](LICENSE). Use it in personal or commercial projects with attribution.
