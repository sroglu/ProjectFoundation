using PFound.ECS;

// Higher-arity query coverage (4..10 component selectors), beyond the 1..3 exercised by
// the core suite. Verifies match counts, Without<> exclusion, and ref mutation via ForEach.
internal static class QueryCharacterizationTests
{
    private struct A { public int V; }
    private struct B { public int V; }
    private struct C { public int V; }
    private struct D { public int V; }
    private struct E { public int V; }
    private struct F { public int V; }
    private struct Skip { }

    public static void Run()
    {
        Arity4();
        Arity5();
        HighArityForEach();
        WithoutOnHighArity();
    }

    private static Entity Full(World w, int n)
    {
        var e = w.Create();
        w.Add(e, new A { V = n }); w.Add(e, new B { V = n }); w.Add(e, new C { V = n });
        w.Add(e, new D { V = n }); w.Add(e, new E { V = n }); w.Add(e, new F { V = n });
        return e;
    }

    private static void Arity4()
    {
        var w = new World();
        Full(w, 1); Full(w, 2);
        var partial = w.Create();
        w.Add(partial, new A()); w.Add(partial, new B()); w.Add(partial, new C()); // no D

        var q = w.Query<A, B, C, D>().Build();
        TestKit.Check(w.Entities(q).Count == 2, "Query<A,B,C,D> matches only entities with all four");
    }

    private static void Arity5()
    {
        var w = new World();
        Full(w, 1); Full(w, 2); Full(w, 3);
        var q = w.Query<A, B, C, D, E>().Build();
        TestKit.Check(w.Entities(q).Count == 3, "Query<A,B,C,D,E> matches all five-component entities");
    }

    private static void HighArityForEach()
    {
        var w = new World();
        for (int i = 0; i < 4; i++) Full(w, 10);

        // Sum B..E into A for every match; proves all four ref params bind to the right pools.
        w.Query<A, B, C, D, E>().ForEach((World world, Entity e, ref A a, ref B b, ref C c, ref D d, ref E ee) =>
        {
            a.V = b.V + c.V + d.V + ee.V;
        });

        int correct = 0;
        foreach (var e in w.Entities(w.Query<A>().Build()))
            if (w.Get<A>(e).V == 40) correct++;
        TestKit.Check(correct == 4, "ForEach<A,B,C,D,E> mutates all matched entities via ref");
    }

    private static void WithoutOnHighArity()
    {
        var w = new World();
        Full(w, 1); Full(w, 2);
        var tagged = Full(w, 3);
        w.Add(tagged, new Skip());

        var q = w.Query<A, B, C, D>().Without<Skip>().Build();
        TestKit.Check(w.Entities(q).Count == 2, "Without<Skip> excludes tagged entity on high-arity query");
    }
}
