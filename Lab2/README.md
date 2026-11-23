# Lab 2: A Coffee Shop Server (C# / .NET 10)

A coffee shop server written directly on the `Socket` class. Clients order products, workers prepare them, a provider brings new stock, and the shop stays open when a connection fails.

No `TcpListener`, `TcpClient`, `NetworkStream` or messaging library is used.

## Contents

1. [Build and run](#1-build-and-run)
2. [The protocol](#2-the-protocol)
3. [How it works, in simple words](#3-how-it-works-in-simple-words)
4. [Map of the files](#4-map-of-the-files)
5. [What to show at the defense](#5-what-to-show-at-the-defense)
6. [Answers to the task questions](#6-answers-to-the-task-questions)
7. [Answers to the control questions](#7-answers-to-the-control-questions)
8. [Bonus](#8-bonus)
9. [C# words used here](#9-c-words-used-here)
10. [Troubleshooting](#10-troubleshooting)

## 1. Build and run

You need the .NET 10 SDK. From the `RC_E_F` folder:

```powershell
dotnet build Lab2\Lab2.csproj -c Release
```

Then open one terminal for each program:

```powershell
.\artifacts\bin\Lab2\release\Lab2.exe server 5050
.\artifacts\bin\Lab2\release\Lab2.exe client 127.0.0.1 5050
.\artifacts\bin\Lab2\release\Lab2.exe provider 127.0.0.1 5050
.\artifacts\bin\Lab2\release\Lab2.exe demo 127.0.0.1 5050
```

To build and open the server, two clients and the provider in one step:

```powershell
powershell -ExecutionPolicy Bypass -File Lab2\start_shop.ps1
```

**In Visual Studio:** set `Lab2` as the startup project, choose `server` in the Run dropdown and press Ctrl+F5. Then choose `client` or `provider` and press Ctrl+F5 again.

Server options:

```text
Lab2 server 5050                 keeps the saved stock, or picks 10-50 of each product
Lab2 server 5050 --stock random  always picks a new random stock of 10-50
Lab2 server 5050 --stock 1       starts with 1 of each product
```

Commands in the client and provider:

```text
menu
order croissant 2 tea 1 coffee 1
deliver coffee 20 croissant 10     (provider only)
help
quit
test split    one message in two pieces, then two messages together
test long     a message that is too long
test junk     random bytes
test silent   stops the heartbeat, like a lost network
```

## 2. The protocol

Every message is one line of text that ends with `\n`, at most 256 bytes. This is the **delimiter** framing from Lecture 4.

| Sent to the server | Meaning |
|---|---|
| `HELLO client` / `HELLO provider` | says who this connection is |
| `MENU` | asks for products and stock |
| `ORDER coffee 1 tea 2` | places one order |
| `DELIVER coffee 20` | adds stock (provider only) |
| `PING` | heartbeat |
| `HELP` | lists the commands |

| Sent by the server | Meaning |
|---|---|
| `WELCOME ...` | the connection is ready |
| `OK role=client` | the role was accepted |
| `MENU coffee=23 (4s), tea=10 (3s), croissant=41 (2s)` | the menu |
| `ACCEPTED order=7 time=11s` | the products were taken, preparation will follow |
| `REFUSED coffee: asked 5, left 3` | not enough stock |
| `READY order=7` | the order is prepared |
| `DELIVERED coffee +20 = 43` | the delivery was added |
| `ALERT tea is low: 4 left` | a product fell below 5 |
| `ERROR ...` | invalid input |
| `PONG` | heartbeat answer |

## 3. How it works, in simple words

Think of a real coffee shop.

- **The door** is the listening socket. It only lets people in.
- **Each customer gets a waiter**, a thread that listens to that one connection.
- **The stock shelf** is shared, so only one person may change it at a time. That is the `lock`.
- **The order rail** is the queue. Waiters put orders on it, oldest first.
- **The cooks** are the workers. There is one per logical processor, and each makes one whole order at a time.
- **Each customer has a mailbox**, the outbox. Anyone can drop a message in it, and one writer thread delivers the messages in order. Nobody ever writes on the socket at the same time as somebody else.

The path of one order:

```text
client --ORDER--> handler thread --lock: check + take stock--> queue --> worker waits --> READY
                       |                                                                   |
                       +--ACCEPTED--> outbox --> writer thread --> client <-- outbox <-----+
```

When a client disappears, its handler thread stops reading and calls `CancelOrders`. The products go back on the shelf and the cook working on that order is woken up, so the cook stops and takes the next order.

## 4. Map of the files

| File | What it does |
|---|---|
| `Program.cs` | reads the command line and starts one role |
| `Server.cs` | listening socket, `Bind`, `Listen`, `Accept`, one thread per connection |
| `ClientHandler.cs` | reads one connection and runs its commands |
| `Connection.cs` | cuts bytes into lines, the outbox and writer thread, heartbeat, close |
| `Shop.cs` | stock, order queue, workers, alerts, cancelling, saving the stock |
| `Order.cs` | the `Product` and `Order` classes |
| `ConsoleApp.cs` | the interactive client and provider |
| `Demo.cs` | many clients order at the same moment |
| `Log.cs` | one log line with time and endpoint |
| `java/ShopClient.java` | the bonus client in Java |
| `start_shop.ps1` | builds and opens four windows |

## 5. What to show at the defense

**Task 1 (grade 5).** Start the server and one client.

```text
menu
order croissant 2 tea 1 coffee 1     ACCEPTED at once, READY after 11 s
order coffee 9999                    REFUSED, says how many are left
order pizza 1                        ERROR
order coffee -1                      ERROR
test split                           two menus: framing works
```

**Task 2 (grade 6).** This laptop has 32 logical processors, so the demo needs more than 32 clients. It opens 40 by default:

```powershell
.\artifacts\bin\Lab2\release\Lab2.exe server 5050 --stock 100
.\artifacts\bin\Lab2\release\Lab2.exe demo 127.0.0.1 5050
```

Expected result: 32 orders ready after 2 s, then 8 after 4 s, because those 8 waited in the queue.

For the last croissant, restart with `--stock 1` and run the demo again. Exactly one client gets it and all the others are refused.

**Task 3 (grade 7).** Start a client and a provider.

```text
client:    deliver coffee 5          ERROR, only the provider can deliver
provider:  deliver coffee 20         stock grows at once
client:    order tea <almost all>    the provider gets ALERT without asking
```

Close the provider, make another product fall below 5, then start the provider again. It receives the alert as soon as it connects.

**Task 4 (grade 8).**

- Order `coffee 5`, then close the client window. The server log shows the order cancelled, and `menu` in another client shows the products back.
- Order `coffee 5`, then kill the client in Task Manager. The result is the same.
- Order `coffee 5`, then type `test silent`. After about 5 s the server logs `no heartbeat, network lost` and cancels it.
- Type `test long` and `test junk`. Each gets an ERROR and the shop keeps working.
- Start typing without pressing Enter while an order finishes. READY appears and your text stays on the prompt.
- Close the server. The clients print a message and end by themselves.

## 6. Answers to the task questions

### Task 1

**1. How does your code cut the received bytes into messages? Two pieces? Two messages in one receive?**
`Connection.ReadLine` looks at the bytes one by one and collects them until it finds `\n`. If a message arrives in two pieces, the first piece stays in `_line` and the method calls `Receive` again. If two messages arrive in one receive, the method returns the first one and `_position` remembers where it stopped, so the next call returns the second one without calling `Receive`.

**2. Why does the server answer at once, and tell the client later that the order is ready?**
Preparation can take many seconds. If the server answered only at the end, the client would see nothing for all that time. It would not know whether the order was accepted, what its number is, or whether the server is still alive.

### Task 2

**1. Which data is shared, and how is it protected?**

| Shared data | Protection |
|---|---|
| stock, active orders, order state, provider, next order number | one `lock (_lock)` in `Shop` |
| the order queue | `BlockingCollection`, thread-safe |
| each connection's outbox | `BlockingCollection`, thread-safe |
| "was this connection closed" | `Interlocked.Exchange` |
| the heartbeat flag | `volatile` |
| the cancel signal of an order | `ManualResetEventSlim`, thread-safe |
| the console in the client | `lock (_consoleLock)` |

**2. Two clients order the last croissant. What could go wrong without protection?**

```text
stock = 1
Thread A reads stock: 1, enough
Thread B reads stock: 1, enough
Thread A writes stock = 0, tells client A "accepted"
Thread B writes stock = 0, tells client B "accepted"
Two croissants were sold, but only one existed.
```

In `PlaceOrder`, the check and the subtraction are inside one `lock`, so B can only read the stock after A has finished.

**3. A worker tells a client READY while that client's thread is reading. What must you take care of?**
Reading and writing at the same time on one socket is fine, because TCP has two independent directions. The danger is **two threads writing** at once, because their bytes could mix. So a worker never touches the socket. It puts `READY` in the outbox, and one writer thread sends everything in order. A slow client also cannot hold a worker this way.

**4. Why one worker per CPU core? What would happen with twice as many?**
One thread per core is the right size for work that uses the CPU, because more threads would only take turns on the same cores. Here the workers only wait and use almost no CPU. With twice as many, twice as many orders would be prepared at once and the queue would be shorter. The cost would be more memory and more threads to manage. If the preparation were real CPU work, the extra workers would bring almost no gain.

### Task 3

**1. How does your server know which connection is the provider?**
The provider's first message is `HELLO provider`. Its `ClientHandler` remembers the role, and `Shop` keeps that connection in `_provider`. Only one provider can be connected, and a connection cannot change its role later. There is no password, so the server trusts that first message.

**2. A delivery and an order change the stock at the same moment. Show that no change is lost.**
Both change the stock inside the same `lock`, so one runs fully and then the other. With stock 10, an order of 3 and a delivery of 20 always end at 27, in either order. Without the lock, both could read 10, and the last write would leave 7 or 30.

**3. Which thread sends the alert to the provider, and when?**
The handler thread of the client whose order made the stock fall below 5 notices it in `PlaceOrder`, at that moment, and puts `ALERT` in the provider's outbox. The provider's **writer thread** then sends it. If no provider is connected, the provider's own handler thread sends the current alerts in `TrySetProvider` when it connects.

### Task 4

**1. How does your server notice that a client has disappeared? What at once, what not?**

- **Closed connection:** `Receive` returns 0. Noticed at once.
- **Killed process:** the operating system closes the socket, so `Receive` returns 0 or throws `ConnectionReset`. Noticed at once.
- **Lost network:** nothing arrives, not even a FIN, so it is **not** noticed at once. Our client sends `PING` when it has been quiet for 1 s, and the server gives up after 5 s of silence (`ReceiveTimeout`). For a client without PING, such as telnet, TCP keep-alive notices it after about 16 s.

**2. A client disappears while a worker prepares its order. How does the worker stop, and how do the products get back?**
The handler thread calls `CancelOrders`. Under the lock it marks the order `Cancelled`, adds the products back to the stock, and calls `order.Cancelled.Set()`. The worker is waiting in `order.Cancelled.Wait(time)`, so it wakes up at once and takes the next order. The next order that uses those products waits its whole time again.

**3. Why must the client receive and read the keyboard at the same time? How does yours do it?**
Both calls block. With one thread, while it waits for the keyboard, `READY` would stay unseen until the user presses Enter. So one thread receives and the main thread reads keys. A lock protects the console: when a message arrives, the half-typed line is cleared, the message is printed, and the typed text is written back.

## 7. Answers to the control questions

**1. Listening socket vs the sockets returned by accept? How many with five clients and the provider?**
The listening socket only accepts new connections and never carries data. Each `Accept` returns a new connected socket for one client. With five clients and the provider there is **1 listening socket and 6 connected sockets**.

**2. Why is one send not one receive? How does your protocol deal with it?**
TCP is a stream of bytes. It keeps every byte in order, but it does not keep the borders between messages. Our protocol ends every message with `\n`, and `ReadLine` rebuilds the messages.

**3. What does it mean when receive returns 0? And when it fails because the connection was reset?**
`0` means the other side closed normally (FIN) and will send nothing more. A reset (`SocketError.ConnectionReset`) means the connection was broken suddenly, for example because the process crashed. In both cases the server closes the socket and cancels the orders.

**4. Where is a producer-consumer pattern? Producers, consumers, queue?**
- **Orders:** the producers are the client handler threads, the queue is `_queue`, and the consumers are the workers.
- **Outgoing messages:** the producers are handlers and workers, the queue is the connection's `_outbox`, and the consumer is its writer thread.

**5. What does it cost when a thread waits for a client? What would change with 1000 clients?**
A waiting thread uses no CPU, but it still holds its stack (about 1 MB reserved) and the operating system must manage it. This server uses two threads per connection, so 1000 clients would need about 2000 threads. It would still work, but with a lot of wasted memory. The better design for many clients is `await ReceiveAsync`, which holds no thread while waiting. Lecture 4 measured 1015 threads against 16 for 1000 clients.

**6. Explain step by step a race condition that could happen without the locks.**
A lost update between an order and a delivery:

```text
stock = 10
Order thread reads 10
Delivery thread reads 10
Delivery thread writes 10 + 20 = 30
Order thread writes 10 - 3 = 7
The stock is 7, but it should be 27. The delivery of 20 was lost.
```

**7. What identifies one TCP connection? How can five clients on one machine use the same server port?**
Four values: client IP, client port, server IP and server port. The five clients have the same IP and connect to the same server port, but the operating system gives each client a **different client port**, so all five connections are different.

## 8. Bonus

- **Two computers.** The server binds to `IPAddress.Any`. Find the server's address with `ipconfig`, allow the program in Windows Firewall for the private network, then run `Lab2 client <that address> 5050` on the other computer.
- **Saved stock.** The server writes `stock.txt` next to the exe, and a restart continues from it. Products in unfinished orders are counted as stock, because those orders die with the server. The file is first written under a temporary name, so a crash cannot leave a half-written file.
- **Two languages.** The client in Java works with the C# server, because the protocol is plain text:

```powershell
java Lab2\java\ShopClient.java client 127.0.0.1 5050
java Lab2\java\ShopClient.java provider 127.0.0.1 5050
```

## 9. C# words used here

- **`Socket`**: the operating system's network connection, as a C# object.
- **`Thread`**: a separate line of execution. `IsBackground = true` means it does not keep the program alive.
- **`() => ...`**: a lambda, a small function without a name.
- **`lock (x) { ... }`**: only one thread at a time can be inside for the same `x`.
- **`volatile`**: every read sees the latest value of a field.
- **`Interlocked.Exchange`**: changes a value in one step that cannot be interrupted. Here it makes `Close` run only once.
- **`BlockingCollection<T>`**: a thread-safe queue. `TryAdd` puts an item in, and taking from an empty one waits.
- **`ManualResetEventSlim`**: a signal. `Wait` sleeps until another thread calls `Set`, or until the time is over.
- **`AutoResetEvent`**: a signal that wakes one waiting thread and then resets itself.
- **`Barrier`**: threads wait until all of them arrive, then continue together.
- **`enum`**: a type with a fixed list of names, like `Waiting`, `Preparing`, `Ready`.
- **`out`**: a parameter the method fills in, a way to return a second value.
- **`Connection?`**: the `?` means this variable can be `null`.
- **`$"text {value}"`**: a string with values placed inside it.
- **`try / catch / finally`**: `catch` handles an error, and `finally` always runs, so the cleanup cannot be skipped.
- **`using var`**: closes the object automatically at the end of the block.

## 10. Troubleshooting

- **`AddressAlreadyInUse`**: an old server is still running. Close it, or find it with `netstat -ano | findstr 5050`.
- **`ConnectionRefused`**: the server is not started, or the port is wrong.
- **Another computer cannot connect**: check the address, make sure both are on the same network, and allow the program in the firewall.
- **Debugger paused on the server**: clients stop getting `PONG` and leave after 10 s. Use Ctrl+F5 for the demo.
- **The stock is not random**: the server loaded `stock.txt`. Start it with `--stock random`.
