# Implement Automated Testing for CI/CD

## Goal
Establish a robust automated testing strategy that runs in GitHub Actions to ensure code quality and system stability.

## Problem
The current codebase lacks automated tests. The critical logic for IP configuration relies on WMI calls (`IpHelper`), which are tightly coupled to the operating system and hard to test without side effects. The Named Pipe communication logic is also untested.

## Proposed Strategy

### 1. Refactor for Testability (Dependency Injection)
To test the core logic without modifying the actual system network settings (which would break CI runners), we must decouple the WMI operations from the business logic.

*   **Action:** Create an interface `INetworkAdapterService` in `IpChanger.Common` (or `Service`).
    ```csharp
    public interface INetworkAdapterService
    {
        IpConfigResponse ApplyConfig(string adapterId, IpConfigRequest request);
        // ... other methods
    }
    ```
*   **Action:** Implement `WmiNetworkAdapterService` which contains the existing WMI logic from `IpHelper`.
*   **Action:** Update `Worker` (or a new `RequestProcessor` class) to accept `INetworkAdapterService` via constructor.

### 2. Unit Testing (xUnit + Moq)
Unit tests will verify the logic in isolation.

*   **Action:** Create a new project `src/IpChanger.Tests`.
*   **Tooling:** Use `xUnit` as the test framework and `Moq` for mocking dependencies.
*   **Test Cases:**
    *   **IpHelper Logic:** Mock `INetworkAdapterService` and verify that `ApplyConfig` is called with the correct parameters when a request is processed.
    *   **Validation:** Verify `IpConfigRequest` validation logic (e.g., valid IP format).

### 3. Integration Testing (IPC)
Test the communication channel between the UI and Service.

*   **Action:** Create an integration test in `IpChanger.Tests`.
*   **Mechanism:**
    *   Start a `NamedPipeServerStream` in a background thread (simulating the Service).
    *   Start a `NamedPipeClientStream` (simulating the UI).
    *   Send a serialized `IpConfigRequest` from client to server.
    *   Verify the server receives it, deserializes it correctly, and sends a response.
    *   Verify the client receives the response.

### 4. GitHub Actions Workflow
Automate the testing process.

*   **Action:** Create `.github/workflows/test.yml`.
*   **Steps:**
    1.  Checkout code.
    2.  Setup .NET 8.
    3.  Build solution.
    4.  Run tests: `dotnet test --no-build --verbosity normal`.

## Why this approach?
*   **Simplicity:** It uses standard .NET tools (xUnit, Moq, dotnet test) without complex E2E frameworks.
*   **Safety:** By mocking WMI, we avoid changing the IP address of the GitHub Actions runner, which prevents connectivity loss during builds.
*   **Reliability:** Tests run on every push, ensuring that changes to the protocol or logic don't break the application.
