# Nekara

**Nekara** (ನೇಕಾರ) means "weaver" in Kannada (local language of Karnataka, India).

Nekara is a language-agnostic **concurrency testing framework** for finding concurrency bugs that occur infrequently in normal executions. It does so by taking over the scheduling of concurrent *Tasks* and systematically exploring the various interleavings of *Tasks*.


## Introduction

Unlike a synchronous and sequential program, a concurrent program can have many different executions due to the interleaving of concurrent operations. In certain executions, a particular ordering of operations may render a bug that the programmer did not anticipate; such bugs are called *concurrency bugs* and they may not be so obvious when looking at the program code.

As a program adds more concurrency, the number of possible executions grow exponentially. This makes concurrency bugs difficult to find and reproduce, because the particular interleaving that leads to the bug may happen very rarely. Identifying such bugs are often challenging since a programmer usually has no control over the scheduling of concurrent *Tasks* and the interleavings of operations are seemingly non-deterministic. Frameworks like [PSharp](https://github.com/p-org/PSharp) provide support for systematically testing for such concurrency bugs, but currently the support remains within the framework.

To address this problem, **Nekara** aims to generalize concurrency testing and provide support for any arbitrary language or framework. The following are its design goals:

1. Abstract Models of Concurrency - the models of concurrecy must be abstract and general so that it can be applied to a variety of application domains.
2. Language-agnostic Interface and Architecture - the system must be able to easily integrate with various frameworks.


## Architecture

The system is split into the server-side and client-side.

**Nekara Server:** The server exposes an API (HTTP or WebSocket for network clients and IPC for local clients -- this is configurable) to its internal *Scheduler*. The *Scheduler* maintains an image of the program under test (running on the client-side) and controls the interleavings of different asynchronous tasks. Using the API provided, the client-side test program sends signals to the server -- e.g., "I just started a new asynchronous Task" -- but does not make progress on its own unless it receives a signal from the server.

**Client Side:** The client side library is a thin proxy that makes remote procedure calls (RPC) to the actual testing service. It exposes the same set of APIs as the server as regular methods, but under the hood marshals the calls into a network request.


## API

The server maintains a model of the current state of the program under test, represented as *Tasks* and *Resources*. More specifically, it maintains:

* Set of *Tasks*
* Set of *Resources*
* Mapping between each *Task* and the *Resources* it is waiting on

The server controls which *Task* will execute, running each *Task* one at a time. The server can switch to a different *Task* at user-defined points in the program.

### Methods

Using the API provided below, the user annotates different parts of the program to inform the server about the change in the program state. Some API methods are used to yield control to the server, upon which the server makes decisions about the scheduling of the *Tasks*:

* `CreateTask()`: called before the creation of a concurrent *Task* to inform the scheduler that a *Task* is about to be created.
* `StartTask(int taskId)`: called from the newly created *Task*, signalling that the concurrent *Task* has started. The call is held by the server (i.e., the server does not return the call) until it decides that the calling *Task* should resume execution.
* `EndTask(int taskId)`: called at the end of the *Task* to remove it from the program state. It gives control to another *Task*.
* `CreateResource(int resourceId)`: called to declare a synchronization *Resource*.
* `DeleteResource(int resourceId)`: called to remove a synchronization *Resource* from the program state.
* `BlockedOnResource(int resourceId)`: called to indicate that the *Task* is waiting on a *Resource* to be freed. This call is held by the server until another *Task* frees the corresponding resource by calling `SignalUpdatedResource`.
* `SignalUpdateResource(int resourceId)`: called to indicate that a *Resource* is no longer held by the calling *Task*. Any other *Task* waiting on the *Resource* via a `BlockedOnResource` call will become available to resume.
* `ContextSwitch()`: called anywhere within a *Task* to yield control to the *Scheduler*. It gives the server an opportunity to switch to another *Task*, allowing it to systematically explore the different interleavings of operations.
* `CreateNondetBool()`: called to obtain a random boolean value.
* `CreateNondetInteger()`: called to obtain a random integer value.
* `Assert(bool predicate)`: called to inform the server about an assertion. In case of an assertion failure, the server will mark the test as "failed" -- meaning a bug was found -- and drop all pending requests.

### C\# API

The methods described above are the low-level API, and gives the user complete control over the testing procedure. This also means that the user is entirely responsible for modelling everything correctly. For the common models in C\#, we provide a higher level API that implements the same interface as the native models.

* `Nekara.Models.Task`: implements the same interface as the native `System.Threading.Tasks.Task` class. One can simply test a program by replacing the dependency on `System.Threading.Tasks` to `Nekara.Models`.


## How to use

* Nekara is written in C#, [.NET Standard 2.0 (C# 7.3)](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/configure-language-version).

0. Build the `Nekara.sln` CSharp solution. This should produce 3 files: `Nekara.dll`, `NekaraClient.dll`, and `NekaraTests.dll`; each under its respective project directory.
1. Run the server-side program `Nekara.dll` - this is a regular HTTP server listening for incoming messages and handles the requests accordingly.
2. If using the example program `NekaraTests.dll`, skip to step 4. If not, go to step 3.
3. Instrument the program to be tested. The following needs to be done for a minimal setup to work:
    * Decorate the entry point (i.e., the method to be tested) with the `TestMethod` attribute. The attribute lets the Nekara Client know which method to test.
    * Just before calling an asynchronous function (i.e., creating a new `Task`), inject a call to `tester.CreateTask()`. This informs the testing service that a `Task` is about to be created.
    * At the beginning of the asynchronous function, inject a `tester.StartTask()` call. Similarly, inject a `tester.EndTask()` at the end of the asynchronous function.
    * Before accessing any shared variable (e.g., a variable declared outside of its own local scope), inject a call to `tester.ContextSwitch()`. This method essentially "yields control" to the testing service and allows the scheduler to explore different access patterns of the shared resource.
4. Once the test program is prepared, run the client-side program `NekaraClient.dll`. It starts in interactive mode, and it prompts the user to enter the path to the program to be tested (e.g., `NekaraTests.dll`). Once it discovers the test method, it will prompt the user to confirm, and the test begins subsequently.