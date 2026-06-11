# OpenAI Chat Demo

A production-pattern .NET 8 console application demonstrating integration with the OpenAI Chat Completions API, built as a foundation for larger AI-powered applications.

## Overview

This project showcases enterprise-grade patterns for integrating Large Language Models (LLMs) into .NET applications. It implements streaming responses, conversation memory, typed exception handling, and resilient retry logic — patterns directly applicable to production AI systems.

## Features

- **Multi-turn conversation memory** — Full chat history maintained across turns for contextual responses
- **Streaming responses** — Token-by-token output via Server-Sent Events for low perceived latency
- **Token usage tracking** — Per-request input/output token counts for cost monitoring
- **Typed exception handling** — Distinct strategies for transient vs. permanent failures
- **Exponential backoff retry** — Automatic retries for rate limits, server errors, and network issues
- **Secret management** — API keys loaded from environment variables (no hardcoded credentials)
- **State integrity** — Failed requests cleaned up from history to prevent context corruption

## Tech Stack

| Component | Version | Purpose |
|-----------|---------|---------|
| .NET | 8.0 (LTS) | Runtime |
| C# | 12 | Language |
| OpenAI .NET SDK | 2.x | Official OpenAI client library |
| System.ClientModel | Latest | Typed exception handling |

**Model:** `gpt-4o-mini` (cost-efficient for chat workloads)

## Architecture Decisions

### Transient vs. Permanent Error Classification

API failures are classified by their recovery strategy:

| HTTP Status | Classification | Strategy |
|-------------|----------------|----------|
| 429 (Rate Limit) | Transient | Retry with exponential backoff |
| 500–599 (Server Errors) | Transient | Retry with exponential backoff |
| Network / Timeout | Transient | Retry with exponential backoff |
| 401 (Unauthorized) | Permanent | Fail fast — credentials issue |
| 400 (Bad Request) | Permanent | Fail fast — caller-side bug |

### Exponential Backoff

Retry delays follow a doubling pattern (1s → 2s → 4s) to prevent the thundering herd problem during service degradation.

### State Cleanup on Failure

When an API call fails, the orphaned user message is removed from the conversation history. This prevents context corruption on subsequent calls — a subtle but critical concern in stateful LLM applications.

### Exception Filters

C# exception filters (`catch ... when`) are used to handle distinct HTTP status codes from the same exception type without losing stack trace information.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- An [OpenAI API key](https://platform.openai.com/api-keys) with billing configured
- Visual Studio 2022/2026, JetBrains Rider, or VS Code

## Setup

### 1. Configure API Key

Set the API key as a user-level environment variable.

**Windows (PowerShell):**

```powershell
[System.Environment]::SetEnvironmentVariable('OPENAI_API_KEY', 'sk-proj-your-key-here', 'User')
```

**Linux / macOS (bash/zsh):**

```bash
echo 'export OPENAI_API_KEY="sk-proj-your-key-here"' >> ~/.bashrc
source ~/.bashrc
```

Restart your terminal or IDE for the variable to take effect.

### 2. Clone and Run

```bash
git clone https://github.com/MNaveedRamzan/OpenAIChatDemo.git
cd OpenAIChatDemo/OpenAIChatDemo
dotnet run
```

## Usage Example

```
Chat started (streaming + retry mode). Type 'exit' to quit.

You: What is dependency injection in .NET?