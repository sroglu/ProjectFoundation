using System;
using PFound.DependencyContainer;

// Standalone mono/csc runner (pure C#, no Unity) — the parity oracle for the DI container.
internal static class DependencyContainerTests
{
    private static int s_passed, s_failed;
    private static void Check(bool cond, string name)
    {
        if (cond) s_passed++;
        else { s_failed++; Console.WriteLine("  FAIL: " + name); }
    }

    private interface IGreeter { }
    private sealed class Greeter : IGreeter { }
    private sealed class NeedsGreeter { public readonly IGreeter G; public NeedsGreeter(IGreeter g) { G = g; } }
    private sealed class Inited : IInitializableService { public int Count; public void Initialize() => Count++; }
    private sealed class Tracked : IDisposable { public bool Disposed; public void Dispose() => Disposed = true; }
    private sealed class Counter { public static int Made; public Counter() { Made++; } }
    private sealed class CycleA { public CycleA(CycleB b) { } }
    private sealed class CycleB { public CycleB(CycleA a) { } }
    private sealed class Unrelated { }
    private sealed class ScopedDep : IDisposable { public bool Disposed; public void Dispose() => Disposed = true; }

    public static int Main()
    {
        // singleton + As<I> + constructor injection
        var c = new DependencyContainer();
        c.Register<Greeter>().As<IGreeter>();
        c.Register<NeedsGreeter>();
        c.Build();
        Check(ReferenceEquals(c.Get<NeedsGreeter>(), c.Get<NeedsGreeter>()), "singleton: same instance");
        Check(c.Get<NeedsGreeter>().G is Greeter, "ctor injection resolves dependency by interface");
        Check(ReferenceEquals(c.Get<IGreeter>(), c.Get<Greeter>()), "As<I>: interface and impl share the singleton");

        // transient
        Counter.Made = 0;
        var ct = new DependencyContainer();
        ct.Register<Counter>().WithLifetime(ServiceLifetime.Transient);
        ct.Build();
        Check(!ReferenceEquals(ct.Get<Counter>(), ct.Get<Counter>()), "transient: new instance each resolve");

        // scoped
        var cs = new DependencyContainer();
        cs.Register<Counter>().WithLifetime(ServiceLifetime.Scoped);
        cs.Build();
        using (var s1 = cs.CreateScope())
        using (var s2 = cs.CreateScope())
        {
            Check(ReferenceEquals(s1.Get<Counter>(), s1.Get<Counter>()), "scoped: same instance within a scope");
            Check(!ReferenceEquals(s1.Get<Counter>(), s2.Get<Counter>()), "scoped: different instance across scopes");
        }

        // RegisterInstance
        var ci = new DependencyContainer();
        var greeter = new Greeter();
        ci.RegisterInstance<IGreeter>(greeter);
        ci.Build();
        Check(ReferenceEquals(ci.Get<IGreeter>(), greeter), "RegisterInstance: returns the provided instance");

        // factory
        var cf = new DependencyContainer();
        cf.Register<Greeter>().WithFactory(_ => new Greeter());
        cf.Build();
        Check(cf.Get<Greeter>() != null, "WithFactory: factory is used");

        // IInitializableService once
        var cinit = new DependencyContainer();
        cinit.Register<Inited>();
        cinit.Build();
        Check(cinit.Get<Inited>().Count == 1, "IInitializableService.Initialize called exactly once");

        // dispose
        var cd = new DependencyContainer();
        cd.Register<Tracked>();
        cd.Build();
        var tracked = cd.Get<Tracked>();
        cd.Dispose();
        Check(tracked.Disposed, "Dispose disposes container-created IDisposable services");

        // TryGet unregistered
        var cempty = new DependencyContainer();
        cempty.Build();
        Check(!cempty.TryGet<Greeter>(out _), "TryGet returns false for an unregistered service");

        // circular dependency fails fast
        var ccyc = new DependencyContainer();
        ccyc.Register<CycleA>();
        ccyc.Register<CycleB>();
        bool threw = false;
        try { ccyc.Build(); ccyc.Get<CycleA>(); }
        catch (InvalidOperationException) { threw = true; }
        Check(threw, "circular dependency throws InvalidOperationException");

        // GAP 1: non-generic RegisterInstance(Type, object) with assignability check
        var cti = new DependencyContainer();
        var greeterInst = new Greeter();
        cti.RegisterInstance(typeof(IGreeter), greeterInst);
        cti.Build();
        Check(ReferenceEquals(cti.Get<IGreeter>(), greeterInst), "RegisterInstance(Type,obj): resolves under the runtime type");

        var ctiBad = new DependencyContainer();
        bool badAssign = false;
        try { ctiBad.RegisterInstance(typeof(IGreeter), new Unrelated()); }
        catch (InvalidOperationException) { badAssign = true; }
        Check(badAssign, "RegisterInstance(Type,obj): throws when instance not assignable to service type");

        // GAP 2: non-generic TryGet(Type, out object)
        var cnt = new DependencyContainer();
        cnt.Register<Greeter>().As<IGreeter>();
        cnt.Build();
        Check(cnt.TryGet(typeof(IGreeter), out var boxed) && boxed is Greeter, "TryGet(Type,out): resolves a registered service");
        Check(!cnt.TryGet(typeof(Unrelated), out _), "TryGet(Type,out): false for an unregistered service");

        // GAP 3: duplicate-registration detection
        var cdup = new DependencyContainer();
        cdup.Register<Greeter>();
        bool dupThrew = false;
        try { cdup.Register<Greeter>(); }
        catch (InvalidOperationException) { dupThrew = true; }
        Check(dupThrew, "duplicate registration throws instead of silently overwriting");

        var cdupAlias = new DependencyContainer();
        cdupAlias.Register<Greeter>().As<IGreeter>();
        bool dupAliasThrew = false;
        try { cdupAlias.RegisterInstance<IGreeter>(new Greeter()); }
        catch (InvalidOperationException) { dupAliasThrew = true; }
        Check(dupAliasThrew, "duplicate alias/service-type registration throws");

        // GAP 4: widened scope API — non-generic resolve, nested scope, Provider
        var cw = new DependencyContainer();
        cw.Register<ScopedDep>().WithLifetime(ServiceLifetime.Scoped);
        cw.Build();
        using (var scope = cw.CreateScope())
        {
            Check(scope.Get(typeof(ScopedDep)) is ScopedDep, "scope.Get(Type): resolves within the scope");
            Check(ReferenceEquals(scope.Get(typeof(ScopedDep)), scope.Get<ScopedDep>()), "scope.Get(Type) and Get<T> share the scoped instance");
            Check(scope.TryGet(typeof(ScopedDep), out var sd) && sd is ScopedDep, "scope.TryGet(Type,out): resolves a registered service");
            Check(!scope.TryGet(typeof(Unrelated), out _), "scope.TryGet(Type,out): false for an unregistered service");
            Check(scope.Provider != null && scope.Provider.GetService(typeof(ScopedDep)) is ScopedDep, "scope.Provider resolves within the scope");

            using (var nested = scope.CreateScope())
                Check(!ReferenceEquals(nested.Get<ScopedDep>(), scope.Get<ScopedDep>()), "nested scope.CreateScope(): independent scoped instance");
        }

        // GAP 5: As<T> / alias assignability validation
        var calias = new DependencyContainer();
        bool aliasThrew = false;
        try { calias.Register<Greeter>().As<IDisposable>(); }
        catch (InvalidOperationException) { aliasThrew = true; }
        Check(aliasThrew, "As<T>: throws when impl not assignable to the alias type");

        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine($"PFound.DependencyContainer: passed={s_passed} failed={s_failed}");
        return s_failed == 0 ? 0 : 1;
    }
}
