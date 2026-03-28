# Proposal: Autonomous Software Development with Jules

## Vision
To transform Jules from a reactive tool into an autonomous development partner capable of managing tasks, identifying improvements, and writing code with minimal human intervention.

## Roles & Responsibilities

We will configure Jules to act in two distinct roles using separate GitHub Actions.

### 1. The Project Manager (PM)
*   **Goal:** Maintain a healthy backlog and ensure code quality.
*   **Behavior:** Proactive, analytical, rigorous.
*   **Outputs:** GitHub Issues (labeled `jules-planned`).
*   **Schedule:** Daily (Morning).

### 2. The Developer (Dev)
*   **Goal:** Implement features and fix bugs defined by the PM or Humans.
*   **Behavior:** Creative, efficient, test-driven.
*   **Outputs:** Pull Requests (ready for human review).
*   **Trigger:** When a new Issue is labeled `jules-approved` or `jules-to-do`.

---

## Scheduled Tasks (The "Pulse")

We will configure the following scheduled tasks in GitHub Actions to drive the autonomous loop.

### A. Daily Backlog Refinement (PM)
*   **Schedule:** `0 8 * * 1-5` (Mon-Fri, 8 AM).
*   **Action:** `jules-pm-triage.yml`
*   **Logic:**
    1.  **Scan Codebase:** Analyze `src/` for TODOs, missing tests, or high cyclomatic complexity.
    2.  **Scan Backlog:** Check for stale issues or duplicate requests.
    3.  **Create Issues:** If a critical gap is found, create an Issue with a clear spec.

### B. Nightly Security & Dependency Audit (PM/QA)
*   **Schedule:** `0 2 * * *` (Daily, 2 AM).
*   **Action:** `jules-qa-audit.yml`
*   **Logic:**
    1.  **Dependency Check:** Check for outdated packages.
    2.  **Security Scan:** Look for hardcoded secrets or unsafe patterns.
    3.  **Report:** Create a "Security Alert" issue if critical vulnerabilities are found.

---

## GitHub Actions Configuration

### 1. `.github/workflows/jules-pm.yml` (The Brain)
*   **Trigger:** Schedule + `workflow_dispatch`.
*   **Job:**
    *   Call Jules API (Session: "Analyze project health").
    *   Prompt: "Identify top 3 technical debt items."
    *   Parse JSON output.
    *   `gh issue create` for new items.

### 2. `.github/workflows/jules-dev.yml` (The Hands)
*   **Trigger:** `issues` (types: [labeled, opened]).
*   **Condition:** `if: contains(github.event.issue.labels.*.name, 'jules-do')`
*   **Job:**
    *   **Read Issue:** Get title and body.
    *   **Call Jules API:**
        *   Prompt: "Implement the feature described in Issue #123."
        *   `automationMode: AUTO_CREATE_PR` (Yes, here we WANT a PR).
    *   **Output:** A new PR is created by Jules.

---

## Human in the Loop (Safety)

To ensure quality and control:
1.  **PM Phase:** Humans review the Issues created by Jules. If we agree, we add the `jules-do` label. This triggers the Developer workflow.
2.  **Dev Phase:** Jules creates a PR. The PR *requires* human approval before merging (branch protection rules).
3.  **Merge:** Humans merge the PR, closing the loop.
