using PFound.ECS;

// Events: typed Subscribe/Publish with deferred dispatch (queued on Publish, delivered on
// Dispatch). Replaces the old leaked EventBus<T> internal with a real API on World.Events.
internal static class EventTests
{
    private struct Damage { public int Amount; }
    private struct Healed { public int Amount; }

    public static void Run()
    {
        DeferredDelivery();
        MultipleSubscribers();
        TypeRouting();
        QueueDrainsAfterDispatch();
        Unsubscribe();
        PublishDuringDispatchDefersToNextRound();
        BatchDelivery();
        BatchAndPerEventCoexist();
    }

    private static void DeferredDelivery()
    {
        var w = new World();
        int received = 0;
        w.Events.Subscribe<Damage>(d => received += d.Amount);

        w.Events.Publish(new Damage { Amount = 7 });
        TestKit.Check(received == 0, "Publish does not deliver immediately (deferred)");

        w.Events.Dispatch();
        TestKit.Check(received == 7, "Dispatch delivers queued events to subscribers");
    }

    private static void MultipleSubscribers()
    {
        var w = new World();
        int a = 0, b = 0;
        w.Events.Subscribe<Damage>(d => a += d.Amount);
        w.Events.Subscribe<Damage>(d => b += d.Amount);
        w.Events.Publish(new Damage { Amount = 3 });
        w.Events.Dispatch();
        TestKit.Check(a == 3 && b == 3, "all subscribers of a type receive the event");
    }

    private static void TypeRouting()
    {
        var w = new World();
        int dmg = 0, heal = 0;
        w.Events.Subscribe<Damage>(d => dmg += d.Amount);
        w.Events.Subscribe<Healed>(h => heal += h.Amount);
        w.Events.Publish(new Damage { Amount = 5 });
        w.Events.Publish(new Healed { Amount = 2 });
        w.Events.Dispatch();
        TestKit.Check(dmg == 5 && heal == 2, "events route to handlers of their own type only");
    }

    private static void QueueDrainsAfterDispatch()
    {
        var w = new World();
        int count = 0;
        w.Events.Subscribe<Damage>(_ => count++);
        w.Events.Publish(new Damage());
        w.Events.Dispatch();
        w.Events.Dispatch(); // second dispatch has nothing left
        TestKit.Check(count == 1, "queue empties after Dispatch — events are not redelivered");
    }

    private static void Unsubscribe()
    {
        var w = new World();
        int count = 0;
        var sub = w.Events.Subscribe<Damage>(_ => count++);
        sub.Dispose();
        w.Events.Publish(new Damage());
        w.Events.Dispatch();
        TestKit.Check(count == 0, "disposed subscription stops receiving events");
    }

    private static void PublishDuringDispatchDefersToNextRound()
    {
        var w = new World();
        int rounds = 0;
        w.Events.Subscribe<Damage>(d =>
        {
            rounds++;
            if (d.Amount > 0) w.Events.Publish(new Damage { Amount = 0 }); // re-publish once
        });
        w.Events.Publish(new Damage { Amount = 1 });
        w.Events.Dispatch();
        TestKit.Check(rounds == 1, "event published during Dispatch is not delivered in the same round");
        w.Events.Dispatch();
        TestKit.Check(rounds == 2, "it is delivered on the next Dispatch");
    }

    private static void BatchDelivery()
    {
        var w = new World();
        int calls = 0, total = 0, batchLen = 0;
        w.Events.SubscribeBatch<Damage>(events =>
        {
            calls++;
            batchLen = events.Length;
            for (int i = 0; i < events.Length; i++) total += events[i].Amount;
        });

        w.Events.Publish(new Damage { Amount = 3 });
        w.Events.Publish(new Damage { Amount = 4 });
        w.Events.Publish(new Damage { Amount = 5 });
        w.Events.Dispatch();

        TestKit.Check(calls == 1, "batch handler is invoked once per Dispatch");
        TestKit.Check(batchLen == 3 && total == 12, "batch handler receives all queued events in one Span");

        w.Events.Dispatch();
        TestKit.Check(calls == 1, "batch handler is not re-invoked when nothing is queued");
    }

    private static void BatchAndPerEventCoexist()
    {
        var w = new World();
        int perEvent = 0, batchCount = 0;
        w.Events.Subscribe<Damage>(_ => perEvent++);
        w.Events.SubscribeBatch<Damage>(events => batchCount += events.Length);

        w.Events.Publish(new Damage { Amount = 1 });
        w.Events.Publish(new Damage { Amount = 1 });
        w.Events.Dispatch();

        TestKit.Check(perEvent == 2 && batchCount == 2,
            "per-event and batch subscribers both receive the same round");
    }
}
