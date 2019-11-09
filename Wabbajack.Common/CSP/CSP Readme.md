### Overview of CSP (Communicating Sequential Processes) for the C# programmer

#### What is CSP?
Communicating Sequential processes is a programming model invented in 1978 by Tony Hoare, who described a process
of computation where hundreds or thousands of small processes communicate via channels. Think of this process like
a assembly line. Each worker in the factory is a process, and the conveyor belts are the channels. The workers don't need
to know where a part came from, or where it's going, they simply take one item off the belt, perform an operation and pass
the item on to another belt. This analogy works quite well, and the following observations about a factory also apply to 
CSP: 

* Multiple workers can pull from the same belt (channel/queue)
* Multiple workers can put work onto the belt
* Belts can buffer items, for slow consumers, but at some point they backup and block the writer
* A worker can pull/push to multiple belts. 


#### What does this look like in C#?

The basic unit of CSP in this library is the channel: 

```
var chan = Channel.Create()
```

Without any other parameters this creates a channel with a size of 0, so every pending put must be matched
1:1 with a take. This creates a syncronization point. Channels are fully async and thread-safe:

```
public async Task TestTakePutBlocking()
{
    var channel = Channel.Create<int>();
    // Channel size is 0, so we can't await, because we'd never complete
    var ptask = channel.Put(1);

    // Now the put is dispatched to the scheduler because we've taken the value
    var (open, val) = await channel.Take();

    Assert.AreEqual(1, val);
    Assert.IsTrue(open);
    Assert.IsTrue(await ptask);
}
```