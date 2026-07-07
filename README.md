# OpenAI + Anthropic Chat Demo

A production-pattern .NET 8 console application demonstrating multi-model AI integration with both OpenAI GPT and Anthropic Claude, built as a foundation for larger AI-powered applications.

## Overview

This project showcases enterprise-grade patterns for integrating multiple Large Language Model (LLM) providers into .NET applications through a common abstraction layer. It implements the Strategy pattern for provider selection, structured logging, configuration-driven behavior, and typed exception handling — patterns directly applicable to production AI systems.

## Features

- **Multi-provider support** — Seamless switching between OpenAI (GPT-4o-mini) and Anthropic (Claude Haiku 4.5) via configuration or runtime command
- **Provider abstraction (Strategy pattern)** — `IChatProvider` interface enables provider-agnostic application logic
- **Multi-turn conversation memory** — Full chat history maintained across turns for contextual responses
- **Token usage tracking** — Per-request input/output token counts for cost monitoring across providers
- **Runtime provider switching** — `switch` command to change providers mid-session for live comparison
- **Structured logging** — Serilog with console + rolling file sinks and rich contextual properties
- **Configuration externalization** — Model, prompts, retry settings, and log levels in `appsettings.json`
- **Strongly-typed configuration** — POCO binding with sensible defaults for graceful degradation
- **Secret management** — API keys loaded from environment variables (never committed)
- **State integrity** — Failed requests cleaned up from history to prevent context corruption

## Tech Stack

| Component | Version | Purpose |
|-----------|---------|---------|
| .NET | 8.0 (LTS) | Runtime |
| C# | 12 | Language |
| OpenAI .NET SDK | 2.x | Official OpenAI client library |
| Anthropic .NET SDK | 12.x | Official Anthropic client library (beta) |
| Serilog | Latest | Structured logging framework |
| Microsoft.Extensions.Configuration | Latest | Configuration binding |

**Models used:**
- `gpt-4o-mini` — OpenAI (cost-efficient for chat workloads)
- `claude-haiku-4-5-20251001` — Anthropic (fast, cost-efficient)

## Architecture

### Provider Abstraction (Strategy Pattern)

```
┌─────────────────────┐
│    Program.cs       │  (Application layer — provider-agnostic)
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│   IChatProvider     │  (Strategy interface)
└──────────┬──────────┘
           │
     ┌─────┴─────┐
     ▼           ▼
┌─────────┐ ┌───────────┐
│ OpenAI  │ │ Anthropic │  (Concrete strategies)
│Provider │ │  Provider │
└─────────┘ └───────────┘
```

The application layer interacts only with the `IChatProvider` interface. Concrete implementations adapt provider-specific SDK types (`ChatMessage`, `MessageParam`) to common domain types (`ChatTurn`, `ChatResponse`).

### Design Decisions

**Strategy + Adapter + Factory patterns** work together:
- **Strategy**: `IChatProvider` defines the algorithm interface
- **Adapter**: Each provider adapts vendor SDK types to domain types
- **Factory**: `CreateProvider()` selects concrete implementation based on configuration

**Provider-agnostic domain types**: `ChatTurn` and `ChatResponse` records ensure vendor SDK types never leak into application logic. This makes adding new providers (Gemini, Mistral, local Ollama) a matter of implementing the interface — zero changes to application code.

**Configuration-driven behavior**: Active provider, models, prompts, and retry policies live in `appsettings.json`. Behavior changes require no code recompilation.

**Vendor-specific handling** (encapsulated in providers):
- OpenAI treats system prompts as regular messages; Anthropic uses a separate `System` field
- OpenAI returns tokens as `int`; Anthropic returns `long` (cast to `int` at boundary)
- OpenAI uses `ChatCompletion` directly; Anthropic uses typed content blocks (`TryPickText`)

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- An [OpenAI API key](https://platform.openai.com/api-keys) with billing configured
- An [Anthropic API key](https://console.anthropic.com/settings/keys) with billing configured
- Visual Studio 2022/2026, JetBrains Rider, or VS Code

## Setup

### 1. Configure API Keys

Set both API keys as user-level environment variables.

**Windows (PowerShell):**

```powershell
[System.Environment]::SetEnvironmentVariable('OPENAI_API_KEY', 'sk-proj-your-key-here', 'User')
[System.Environment]::SetEnvironmentVariable('ANTHROPIC_API_KEY', 'sk-ant-api03-your-key-here', 'User')
```

**Linux / macOS (bash/zsh):**

```bash
echo 'export OPENAI_API_KEY="sk-proj-your-key-here"' >> ~/.bashrc
echo 'export ANTHROPIC_API_KEY="sk-ant-api03-your-key-here"' >> ~/.bashrc
source ~/.bashrc
```

Restart your terminal or IDE for the variables to take effect.

### 2. Clone and Run

```bash
git clone https://github.com/MNaveedRamzan/OpenAIChatDemo.git
cd OpenAIChatDemo/OpenAIChatDemo
dotnet run
```

### 3. Select Default Provider (Optional)

Edit `appsettings.json` to change the default provider:

```json
{
  "AIProvider": {
    "Active": "openai"    // or "anthropic"
  }
}
```

## Usage Example

```
╔════════════════════════════════════════════════╗
║  Multi-Model Chat Demo                         ║
║  Provider: OpenAI                              ║
║  Model:    gpt-4o-mini                         ║
╚════════════════════════════════════════════════╝
Commands: 'exit' to quit, 'switch' to change provider

You: What is dependency injection?

OpenAI: Dependency injection is a design pattern where an object receives
its dependencies from external sources rather than creating them itself...
[Tokens — Input: 22, Output: 45]

You: switch

>>> Switched to Anthropic (claude-haiku-4-5-20251001). History cleared.

You: What is dependency injection?

Anthropic: Dependency injection (DI) is a technique where objects receive
their dependencies from an external source instead of creating them internally...
[Tokens — Input: 18, Output: 52]

You: exit
Chat ended.
```

## Project Structure

```
OpenAIChatDemo/
├── Configuration/
│   └── AppSettings.cs           # Strongly-typed config POCOs
├── Providers/
│   ├── IChatProvider.cs         # Strategy interface + domain types
│   ├── OpenAIProvider.cs        # OpenAI adapter
│   └── AnthropicProvider.cs     # Anthropic adapter
├── Program.cs                    # Application entry + provider factory
├── appsettings.json              # Runtime configuration
└── OpenAIChatDemo.csproj         # Project + package references
```

## Configuration

The `appsettings.json` file controls all runtime behavior:

```json
{
  "AIProvider": {
    "Active": "openai"
  },
  "OpenAI": {
    "Model": "gpt-4o-mini",
    "SystemPrompt": "You are a helpful assistant that provides concise answers."
  },
  "Anthropic": {
    "Model": "claude-haiku-4-5-20251001",
    "SystemPrompt": "You are a helpful assistant that provides concise answers.",
    "MaxTokens": 1024
  },
  "Retry": {
    "MaxAttempts": 3,
    "InitialDelayMs": 1000,
    "BackoffMultiplier": 2
  },
  "Serilog": { /* structured logging config */ }
}
```

## Logging

Structured logs are written to two sinks:

- **Console** — Human-readable output with timestamps and log levels
- **File** — Rolling daily log files in `logs/chat-YYYYMMDD.log` (7-day retention)

Each log entry preserves structured properties (provider, token counts, retry attempts) as JSON, enabling downstream analysis with tools like Seq, Elasticsearch, or Datadog.

## Roadmap

This project is a foundation for two larger portfolio projects:

- **SupportPilot** — Multi-channel AI customer support agent with RAG, function calling, and multi-agent orchestration
- **DocuMind** — Enterprise document Q&A system with hybrid search and citation-backed answers

## License

MIT

## Author

Muhammad Naveed Ramzan — Senior Full Stack .NET Developer with 10+ years of enterprise experience, specializing in AI integration for production systems.

- GitHub: [@MNaveedRamzan](https://github.com/MNaveedRamzan)
- LinkedIn: [Muhammad Naveed Ramzan](https://linkedin.com/in/m-naveed-ramzan)