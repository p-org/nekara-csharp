# TestingService

## Current State (for devs)

There are currently 2 C# Solutions in this project due to merging of 2 different repositories.

* `TestingService/TestingService.sln` is the original solution file containing `TestingService` and `ProgramUnderTest`. This solution file is left there for reference.
* `AsyncTester.sln` was copied into this repository and it points to `AsyncTester`, `ClientProgram`, and `Benchmarks`. This project contains the split-up version of the system.
    * `AsyncTester` contains the core code and the server-side start-up program
    * `ClientProgram` contains the client-side testing API and the start-up program
    * `Benchmarks` contains the set of benchmark programs we use to test the system

We will clean up the repository soon, once the bare architecture is in place. For now, leaving all the code where they are.


## Architecture

The system is split into the server-side and client-side.

**Server Side:** The server side exposes an API (HTTP for network clients and IPC for local clients; this is configurable) to its internal *Scheduler*. The *Scheduler* essentially manages the interleavings of different asynchronous tasks. The program under test will send signals to the server -- e.g., "I just started a new asynchronous task" -- but it will not make progress on its own (even if can) unless it receives a signal from the server. 

**Client Side:** The client side library/program is a thin proxy to the actual testing service. The program under test makes local calls to this proxy service, which relays the calls to the server-side and mediates the responses.


## How to use

0. Build the `AsyncTester.sln` CSharp solution. This should produce 3 files: `AsyncTester.exe`, `ClientProgram.exe`, and `Benchmarks.dll`; each under its respective project directory.
1. Run the server-side program `AsyncTester.exe` - this is a regular HTTP server listening for incoming messages and handles the requests accordingly.
2. If using the example program `Benchmarks.dll`, skip to step 4. If not, go to step 3.
3. Instrument the program to be tested. The following needs to be done for a minimal setup to work:
    * Decorate the entry point (i.e., the method to be tested) with the `TestMethod` attribute. The method should have the following signature: `void Method(ITestingService tester)`. The `tester` object will be passed to the test method by the testing service; the user is responsible for maintaining its reference in their test program. The `tester` object exposes the API described in the next section.
    * Just before calling an asynchronous function (i.e., creating a new `Task`), inject a call to `tester.CreateTask()`. This informs the testing service that a `Task` is about to be created.
    * At the beginning of the asynchronous function, inject a `tester.StartTask()` call. Similarly, inject a `tester.EndTask()` at the end of the asynchronous function.
    * Before accessing any shared variable (e.g., a variable declared outside of its own local scope), inject a call to `tester.ContextSwitch()`. This method essentially "yields control" to the testing service and allows the scheduler to explore different access patterns of the shared resource.
4. Once the test program is prepared, run the client-side program `ClientProgram.exe`. It starts in interactive mode, and it prompts the user to enter the path to the program to be tested (e.g., `Benchmarks.dll`). Once it discovers the test method, it will prompt the user to confirm, and the test begins subsequently.

## API

* `CreateTask`: 
* `StartTask`:
* `EndTask`:
* `ContextSwitch`: 
* `CreateResource`:
* `DeleteResource`:
* `BlockedOnResource`:
* `SignalUpdateResource`: