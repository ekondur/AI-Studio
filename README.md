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
- Works with your own OpenAI key (or any compatible endpoint) so you stay in control of data and cost.
- Generates or updates code while preserving formatting, documentation, and project conventions.
- Built-in commands for documentation, explanations, security review, and unit test creation keep teams in flow.

## Requirements

- Visual Studio 2022 17.6 or later with the VSIX extension workload installed.
- An active OpenAI API key (or a compatible Azure/OpenAI endpoint) with network access from the IDE.
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

1. **Create an API key** at [OpenAI](https://platform.openai.com/account/api-keys) (or configure your own endpoint).
2. **Configure AI Studio** via `Tools > Options > AI Studio` and paste the key into the **General** page.
3. **Pick a feature** (for example, Code It) by selecting code, right-clicking, and choosing the desired AI Studio command.

![Configuration Screenshot](https://github.com/user-attachments/assets/3d50b0f4-b127-48ed-892d-94db90d4ca02)

### Using Private or Self-Hosted LLMs

AI Studio can call any OpenAI-compatible endpoint, including private or on-prem LLM gateways:

1. In `Tools > Options > AI Studio > General`, switch the **Base URL** to your private endpoint (for example, `https://llm.internal/api/v1`).
2. Enter the API key/token issued by your internal gateway.
3. (Optional) In `Tools > Options > AI Studio > Commands`, override individual commands to target different models (`gpt-4o`, `gpt-4o-mini`, `my-company-llm`) or apply custom instructions.

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

![Code It](https://user-images.githubusercontent.com/4971326/232882864-85547d6f-75ee-4d49-8684-a3b736b5da2e.png)

**Result**

![Code Output](https://user-images.githubusercontent.com/4971326/232883443-de21b5c2-3415-4f5b-bed9-49077bf7732c.png)

### Add Comments

1. Highlight the code you want documented.
2. Run `AI Studio > Add Comments`.
3. AI Studio adds concise inline comments without disturbing formatting.

![Add Comments](https://user-images.githubusercontent.com/4971326/232887104-8778b163-6cbf-4dcb-a12b-caa6ba266565.png)

### Refactor

1. Select a method or block.
2. Choose `AI Studio > Refactor`.
3. Compare the result with the original and apply the pieces you want to keep.

![Refactor](https://user-images.githubusercontent.com/4971326/232884573-c8f18fc5-3564-4d8d-ad3a-742b85142b36.png)

### Add Summary

1. Select a method header or type declaration.
2. Run `AI Studio > Add Summary`.
3. Automatically generates XML documentation that matches the signature.

![Summary](https://user-images.githubusercontent.com/4971326/232885737-84f7befa-1cad-4ff7-ba10-4b84f659b2fc.png)

### Explain

1. Highlight unfamiliar code.
2. Choose `AI Studio > Explain`.
3. A popup summarizes what the code does and why.

![Explain](https://github.com/user-attachments/assets/3c419429-2586-428d-a1ef-599803d137da)

### Security Check

1. Select code that handles I/O, crypto, or user data.
2. Run `AI Studio > Security Check`.
3. Review the flagged issues and suggested mitigations.

![Security Check](https://github.com/user-attachments/assets/37dbecc7-9894-49ed-a70c-efe3bb8d03a8)

### Unit Test Generation

1. Highlight a method to test.
2. Run `AI Studio > Add Unit Tests`.
3. AI Studio generates arrange/act/assert scaffolding tailored to the method.

![Unit Test Example](https://github.com/user-attachments/assets/728816aa-228d-4b06-adbb-bd79e75ae633)

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

**Example result**

![Custom Command](https://user-images.githubusercontent.com/4971326/232890352-64908383-623b-43f7-8dfa-32f305f67a43.png)

## Troubleshooting and Feedback

- Verify your API key and quota if requests fail; the Output tool window surfaces errors returned by the provider.
- Ensure Visual Studio can reach the OpenAI endpoint (corporate proxies may need to allowlist it).
- Capture screenshots/logs and [open an issue](https://github.com/ekondur/AI-Studio/issues) for bugs or ideas.
- Contributions are welcome via pull requests; please include before/after screenshots for UI tweaks.

## License

AI Studio is released under the [MIT License](LICENSE). Use it in personal or commercial projects with attribution.
