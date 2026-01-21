# Feature: Autonomous GitHub Issue Creation by Jules

## Context
We want to enable Jules (our AI Agent) to proactively identify tasks, bugs, or improvements in the codebase and create GitHub issues for them *autonomously*, without creating a Pull Request. While Jules is often associated with automated PRs, the API allows us to disable this behavior (it is off by default), enabling us to use Jules purely as an analysis engine.

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
    *   **Prompt:** "Analyze the `src/` directory for [Specific Criteria]. Identify the top 3 most critical findings. Return a pure JSON list where each item has a 'title' (short summary) and 'body' (detailed description suitable for a GitHub Issue). Do not include any other text."
    *   **Automation Mode:** We explicitly **omit** the `automationMode` field (or ensure it is NOT set to `AUTO_CREATE_PR`). This guarantees that Jules will *only* generate the plan/analysis and will **not** attempt to create a Pull Request.
3.  **Parse Output:**
    *   The Action script (bash/powershell) parses the JSON response from the Jules Activity.
    *   *Self-Correction:* The API response might include conversational text (e.g., "Here is the list..."). We will use `jq` or a small script to extract the JSON block.
4.  **Create Issues:**
    *   For each item in the parsed list, the Action executes:
        ```bash
        # Pseudo-code for duplicate check
        if ! gh issue list --search "<Title> in:title" | grep -q .; then
             gh issue create --title "<Title>" --body "<Body>" --label "jules-auto"
        fi
        ```

## Why this approach?
*   **Security:** We utilize the standard `GITHUB_TOKEN` within the Action runner to create issues, avoiding the need to give the Jules Agent direct write access to Issues if it doesn't already have it.
*   **Flexibility:** We can change the "Prompt" in the workflow file to target different things (e.g., "Find performance bottlenecks" vs "Find security flaws") without redeploying the agent.
*   **Structured Control:** We can validate the JSON output before creating issues, preventing spam or malformed issues.

## Risks & Mitigations

| Risk | Mitigation |
| :--- | :--- |
| **Malformed Output** | Jules might return markdown or extra text. **Mitigation:** The prompt will explicitly ask for "pure JSON" and the parsing script will search for the first `[` and last `]` characters to extract the payload. If parsing fails, the job logs a warning and exits without creating issues. |
| **Duplicate Issues** | Scheduled runs might find the same "bug" every night. **Mitigation:** Before creating an issue, the script will use `gh issue list` to check if an open issue with the exact same title already exists. |
| **Hallucinations** | Jules might flag code that doesn't exist. **Mitigation:** We can instruct Jules to include the `file_path` and `line_number` in the JSON, and have the script verify the file exists before creating the issue. |

## Setup Requirements
1.  **Jules API Key:** Stored as a GitHub Secret (`JULES_API_KEY`).
2.  **GitHub Token:** The default `GITHUB_TOKEN` must have `issues: write` permission.
3.  **Workflow File:** Create `.github/workflows/jules-scanner.yml`.

## Next Steps
1.  Obtain a Jules API Key.
2.  Prototype the `curl` request to Jules API to confirm it can return valid JSON output without creating a PR.
3.  Implement the GitHub Action workflow with the duplicate checking logic.
