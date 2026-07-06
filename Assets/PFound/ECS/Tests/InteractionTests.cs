using PFound.ECS;

// Interactions: directed entity↔entity relationships grouped by an interaction-type id.
// Pure-core returns a managed view (Unity layer may expose NativeArray over the same data).
internal static class InteractionTests
{
    private const int Attack = 1;
    private const int Trade = 2;

    public static void Run()
    {
        StartAndGet();
        AccumulateAndIsolateByType();
        OfInteractor();
        ForEachInteractee();
        Clear();
    }

    private static void StartAndGet()
    {
        var w = new World();
        var a = w.Create();
        var b = w.Create();
        w.Interactions.Start(Attack, a, b);

        var list = w.Interactions.Get(Attack);
        TestKit.Check(list.Count == 1, "Get returns the started interaction");
        TestKit.Check(list[0].Interactor == a && list[0].Interactee == b,
            "interaction carries the right interactor and interactee");
    }

    private static void AccumulateAndIsolateByType()
    {
        var w = new World();
        var a = w.Create(); var b = w.Create(); var c = w.Create();
        w.Interactions.Start(Attack, a, b);
        w.Interactions.Start(Attack, a, c);
        w.Interactions.Start(Trade, b, c);

        TestKit.Check(w.Interactions.Get(Attack).Count == 2, "interactions of a type accumulate");
        TestKit.Check(w.Interactions.Get(Trade).Count == 1, "interaction types are isolated");
        TestKit.Check(w.Interactions.Get(99).Count == 0, "unknown interaction type is empty");
    }

    private static void OfInteractor()
    {
        var w = new World();
        var a = w.Create(); var b = w.Create(); var c = w.Create();
        w.Interactions.Start(Attack, a, b);
        w.Interactions.Start(Attack, a, c);
        w.Interactions.Start(Attack, b, c);

        TestKit.Check(w.Interactions.OfInteractor(Attack, a).Count == 2,
            "OfInteractor returns only interactions started by that interactor");
        TestKit.Check(w.Interactions.OfInteractor(Attack, b).Count == 1, "OfInteractor filters correctly for b");
    }

    private static void ForEachInteractee()
    {
        var w = new World();
        var a = w.Create(); var b = w.Create(); var c = w.Create();
        w.Interactions.Start(Attack, a, b);
        w.Interactions.Start(Attack, a, c);

        int visited = 0;
        bool sawB = false, sawC = false;
        w.Interactions.ForEachInteractee(Attack, a, e => { visited++; if (e == b) sawB = true; if (e == c) sawC = true; });
        TestKit.Check(visited == 2 && sawB && sawC, "ForEachInteractee visits every interactee of the interactor");
    }

    private static void Clear()
    {
        var w = new World();
        var a = w.Create(); var b = w.Create();
        w.Interactions.Start(Attack, a, b);
        w.Interactions.Clear();
        TestKit.Check(w.Interactions.Get(Attack).Count == 0, "Clear removes all interactions (per-frame reset)");
    }
}
