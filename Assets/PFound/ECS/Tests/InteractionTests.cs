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
        BidirectionalLookup();
        DedupeAndExistence();
        CompletionState();
        CompleteAllVariants();
        DirtyTracking();
        RoleQueries();
        TypedInteractionTypeRegistry();
        InteractionScopedQuery();
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

    private static void BidirectionalLookup()
    {
        var w = new World();
        var a = w.Create(); var b = w.Create(); var c = w.Create();
        w.Interactions.Start(Attack, a, c);
        w.Interactions.Start(Attack, b, c);

        TestKit.Check(w.Interactions.OfInteractee(Attack, c).Count == 2,
            "OfInteractee returns every interaction targeting the interactee (reverse lookup)");

        int seen = 0; bool sawA = false, sawB = false;
        w.Interactions.ForEachInteractor(Attack, c, e => { seen++; if (e == a) sawA = true; if (e == b) sawB = true; });
        TestKit.Check(seen == 2 && sawA && sawB, "ForEachInteractor visits every interactor of the interactee");
    }

    private static void DedupeAndExistence()
    {
        var w = new World();
        var a = w.Create(); var b = w.Create();
        w.Interactions.Start(Attack, a, b);
        w.Interactions.Start(Attack, a, b); // duplicate ⇒ no-op

        TestKit.Check(w.Interactions.Get(Attack).Count == 1, "duplicate active interaction is de-duplicated");
        TestKit.Check(w.Interactions.Exists(Attack, a, b), "Exists is true for a started interaction");
        TestKit.Check(!w.Interactions.Exists(Attack, b, a), "Exists is direction-sensitive");
        TestKit.Check(!w.Interactions.Exists(Trade, a, b), "Exists is type-sensitive");
    }

    private static void CompletionState()
    {
        var w = new World();
        var a = w.Create(); var b = w.Create();
        w.Interactions.Start(Attack, a, b);
        var it = w.Interactions.Get(Attack)[0];

        TestKit.Check(!w.Interactions.IsComplete(it), "a fresh interaction is not complete");
        w.Interactions.CompleteInteraction(it);
        TestKit.Check(w.Interactions.IsComplete(it), "CompleteInteraction marks it complete");

        w.Interactions.Start(Attack, a, b); // re-start a completed interaction ⇒ reactivate
        TestKit.Check(!w.Interactions.IsComplete(w.Interactions.Get(Attack)[0]),
            "restarting a completed interaction reactivates it");
    }

    private static void CompleteAllVariants()
    {
        var w = new World();
        var a = w.Create(); var b = w.Create(); var c = w.Create();
        w.Interactions.Start(Attack, a, b);
        w.Interactions.Start(Attack, a, c);
        w.Interactions.Start(Attack, b, c);

        w.Interactions.CompleteAllOfInteractor(Attack, a);
        int completedForA = 0;
        foreach (var it in w.Interactions.OfInteractor(Attack, a))
            if (w.Interactions.IsComplete(it)) completedForA++;
        TestKit.Check(completedForA == 2, "CompleteAllOfInteractor completes every interaction of that interactor");

        w.Interactions.CompleteAllOfType(Attack);
        int completedTotal = 0;
        foreach (var it in w.Interactions.Get(Attack))
            if (w.Interactions.IsComplete(it)) completedTotal++;
        TestKit.Check(completedTotal == 3, "CompleteAllOfType completes every interaction of the type");
    }

    private static void DirtyTracking()
    {
        var w = new World();
        var a = w.Create(); var b = w.Create();
        w.Interactions.Start(Attack, a, b);
        TestKit.Check(w.Interactions.IsDirty(Attack), "starting an interaction marks its type dirty");
        TestKit.Check(!w.Interactions.IsDirty(Trade), "an untouched type is not dirty");

        w.Interactions.ClearDirtyMask();
        TestKit.Check(!w.Interactions.IsDirty(Attack), "ClearDirtyMask resets the dirty set");

        w.Interactions.CompleteInteraction(w.Interactions.Get(Attack)[0]);
        TestKit.Check(w.Interactions.IsDirty(Attack), "completing an interaction marks its type dirty");
    }

    private static void RoleQueries()
    {
        var w = new World();
        var a = w.Create(); var b = w.Create();
        w.Interactions.Start(Attack, a, b);

        TestKit.Check(w.Interactions.HasRole(Attack, a, InteractionRole.Interactor), "HasRole: a is the interactor");
        TestKit.Check(w.Interactions.HasRole(Attack, b, InteractionRole.Interactee), "HasRole: b is the interactee");
        TestKit.Check(!w.Interactions.HasRole(Attack, a, InteractionRole.Interactee), "HasRole: a is not the interactee");
        TestKit.Check(w.Interactions.HasInteractionOfType(Attack, a) && w.Interactions.HasInteractionOfType(Attack, b),
            "HasInteractionOfType is true for either role");
    }

    private static void TypedInteractionTypeRegistry()
    {
        var duel = InteractionType.Create("Duel");
        TestKit.Check(duel.IsValid, "a created interaction type is valid");
        TestKit.Check(duel.Name == "Duel", "the type name is registered");
        TestKit.Check(InteractionTypeRegistry.GetName(duel.Reverse.Value) == "Duel_reverse",
            "the reverse type name is registered");
        TestKit.Check(InteractionTypeRegistry.TryGetType("Duel", out int id) && id == duel.Value,
            "name→id reverse lookup resolves");

        // Typed value flows through the int-keyed API via implicit conversion.
        var w = new World();
        var a = w.Create(); var b = w.Create();
        w.Interactions.Start(duel, a, b);
        TestKit.Check(w.Interactions.Get(duel).Count == 1, "typed InteractionType drives the int-keyed API");
        TestKit.Check(!InteractionType.Invalid.IsValid, "Invalid type is not valid");
    }

    private struct Combatant { public int Hp; }

    private static void InteractionScopedQuery()
    {
        var w = new World();
        var a = w.Create(); var b = w.Create(); var c = w.Create();
        w.Add(a, new Combatant { Hp = 10 });
        w.Add(b, new Combatant { Hp = 10 });
        w.Add(c, new Combatant { Hp = 10 });
        w.Interactions.Start(Attack, a, b); // a interactor, b interactee

        int interactors = 0; bool onlyA = true;
        w.Query<Combatant>().WithInteraction(Attack).AsInteractor()
            .ForEach((World _, Entity e, ref Combatant __) => { interactors++; if (e != a) onlyA = false; });
        TestKit.Check(interactors == 1 && onlyA, "interaction-scoped query returns only the interactor role");

        int interactees = 0; bool onlyB = true;
        w.Query<Combatant>().WithInteraction(Attack, InteractionRole.Interactee)
            .ForEach((World _, Entity e, ref Combatant __) => { interactees++; if (e != b) onlyB = false; });
        TestKit.Check(interactees == 1 && onlyB, "interaction-scoped query returns only the interactee role");

        // Cached (Build) path stays correct as interactions change.
        var id = w.Query<Combatant>().WithInteraction(Attack).AsInteractor().Build();
        TestKit.Check(w.Entities(id).Count == 1, "cached interaction-scoped query matches the interactor");
        w.Interactions.Start(Attack, c, b);
        TestKit.Check(w.Entities(id).Count == 2, "cached interaction-scoped query refreshes when interactions change");
    }
}
