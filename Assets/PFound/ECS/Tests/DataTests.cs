using System;
using PFound.ECS;

// Data: bulk single-array component access via GetAllComponents<T>() -> Span<T>.
internal static class DataTests
{
    private struct Health { public int Value; }
    private struct Absent { public int X; }

    public static void Run()
    {
        BulkReadOfEveryComponent();
        SpanWritesMutateInPlace();
        EmptyPoolYieldsEmptySpan();
    }

    private static void BulkReadOfEveryComponent()
    {
        var w = new World();
        var a = w.Create(); var b = w.Create(); var c = w.Create();
        w.Add(a, new Health { Value = 1 });
        w.Add(b, new Health { Value = 2 });
        w.Add(c, new Health { Value = 3 });

        var all = w.GetAllComponents<Health>();
        int sum = 0;
        for (int i = 0; i < all.Length; i++) sum += all[i].Value;
        TestKit.Check(all.Length == 3 && sum == 6, "GetAllComponents returns the packed dense array of every component");
    }

    private static void SpanWritesMutateInPlace()
    {
        var w = new World();
        var a = w.Create();
        w.Add(a, new Health { Value = 10 });

        var span = w.GetAllComponents<Health>();
        span[0].Value = 99; // zero-copy over live storage
        TestKit.Check(w.Get<Health>(a).Value == 99, "writes through the span mutate components in place");
    }

    private static void EmptyPoolYieldsEmptySpan()
    {
        var w = new World();
        TestKit.Check(w.GetAllComponents<Absent>().Length == 0, "a never-added component type yields an empty span");
    }
}
