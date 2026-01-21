# Feature: Autonomous GitHub Issue Creation by Jules

## Context
We want to enable Jules (our AI Agent) to proactively identify tasks, bugs, or improvements in the codebase and create GitHub issues for them *autonomously*, without needing to create a Pull Request first. The current Jules API defaults to creating PRs (`AUTO_CREATE_PR`), so we need a hybrid approach to support Issue creation.

## Proposed Architecture

We will implement a **"Think-Then-Act"** workflow using GitHub Actions.

### 1. Components
*   **Orchestrator:** A GitHub Action workflow (`.github/workflows/jules-scanner.yml`).
*   **The Brain (Jules):** The Jules API (`POST /sessions`).
*   **The Hands (GitHub CLI):** The `gh` command-line tool, pre-installed on GitHub Runners.

### 2. Workflow
1.  **Trigger:** The workflow runs on a **Schedule** (e.g., nightly) or **Manual Dispatch**.
2.  **Scan & Analyze (Jules API):**
    *   The Action sends a request to the Jules API to create a new session.
    *   **Prompt:** "Analyze the `src/` directory for [Specific Criteria: e.g., missing error handling, TODOs, security risks]. Identify the top 3 most critical findings. Output them as a JSON list with 'title' and 'body' fields. Do NOT create a PR."
    *   **Automation Mode:** We leave this default (or explicitly *not* `AUTO_CREATE_PR` if possible) so Jules returns the text/JSON response in the Activity stream.
3.  **Parse Output:**
    *   The Action script (bash/powershell) parses the JSON response from the Jules Activity.
4.  **Create Issues:**
    *   For each item in the parsed list, the Action executes:
        ```bash
        gh issue create --title "<Title>" --body "<Body>" --label "jules-auto"
        ```

## Why this approach?
*   **Security:** We utilize the standard `GITHUB_TOKEN` within the Action runner to create issues, avoiding the need to give the Jules Agent direct write access to Issues if it doesn't already have it.
*   **Flexibility:** We can change the "Prompt" in the workflow file to target different things (e.g., "Find performance bottlenecks" vs "Find security flaws") without redeploying the agent.
*   **Structured Control:** We can validate the JSON output before creating issues, preventing spam or malformed issues.

## Setup Requirements
1.  **Jules API Key:** Stored as a GitHub Secret (`JULES_API_KEY`).
2.  **GitHub Token:** The default `GITHUB_TOKEN` has permission to create issues.
3.  **Workflow File:** Create `.github/workflows/jules-scanner.yml`.

## Next Steps
1.  Obtain a Jules API Key.
2.  Prototype the `curl` request to Jules API to confirm it can return valid JSON output without creating a PR.
3.  Implement the GitHub Action workflow.
