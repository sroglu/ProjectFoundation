using System;
using System.Collections.Generic;
using PFound.MVC.core;
using NUnit.Framework;

namespace PFound.MVC.Tests
{
    // ---- Test helpers ----

    internal class StubModel : IModel
    {
        public uint InstanceId(int subModelIndex = 0) => 0;
        public void Dispose() { }
    }

    internal class StubController : ControllerBase
    {
        public readonly List<(IController source, string action, EventArgs data)> Received = new();

        public StubController(MvcContext context, ControllerType type) : base(context, type) { }

        public override IModel GetModel() => new StubModel();
        public override ViewBase GetView() => null;

        protected override void OnActionRedirected(IController source, string action, EventArgs data)
        {
            Received.Add((source, action, data));
        }

#pragma warning disable CS0618
        public void LegacyBroadcast(string action, EventArgs data = null) => Redirect(action, data);
        public void LegacyTargetedRedirect(string action, string controllerName, EventArgs data = null)
            => Redirect(action, controllerName, data);
#pragma warning restore CS0618
    }

    internal class AnotherStubController : ControllerBase
    {
        public readonly List<(IController source, string action, EventArgs data)> Received = new();

        public AnotherStubController(MvcContext context, ControllerType type) : base(context, type) { }

        public override IModel GetModel() => new StubModel();
        public override ViewBase GetView() => null;

        protected override void OnActionRedirected(IController source, string action, EventArgs data)
        {
            Received.Add((source, action, data));
        }
    }

    internal struct CounterChangedEvent
    {
        public int NewValue;
    }

    internal class EventHandlerController : ControllerBase, IEventHandler<CounterChangedEvent>
    {
        public readonly List<CounterChangedEvent> ReceivedEvents = new();

        public EventHandlerController(MvcContext context) : base(context, ControllerType.Instance) { }

        public override IModel GetModel() => new StubModel();
        public override ViewBase GetView() => null;

        public void Handle(CounterChangedEvent evt) => ReceivedEvents.Add(evt);
    }

    // -----------------------------------------------------------------------

    [TestFixture]
    public class ControllerTypeTests
    {
        [Test]
        public void EnumValues_AreInPascalCase()
        {
            Assert.IsTrue(Enum.IsDefined(typeof(ControllerType), "Page"));
            Assert.IsTrue(Enum.IsDefined(typeof(ControllerType), "Instance"));
            Assert.AreEqual(2, Enum.GetValues(typeof(ControllerType)).Length);
        }
    }

    // -----------------------------------------------------------------------

    [TestFixture]
    public class MvcContextTests
    {
        private MvcContext _context;

        [SetUp]
        public void Setup() => _context = new MvcContext();

        [TearDown]
        public void Cleanup() => _context.Dispose();

        [Test]
        public void TypeSafeRedirect_InvokesTargetController()
        {
            var c1 = new StubController(_context, ControllerType.Page);
            var c2 = new AnotherStubController(_context, ControllerType.Instance);

            _context.Redirect<AnotherStubController>(c => c.Received.Add((null, "TypeSafe", null)));

            Assert.AreEqual(0, c1.Received.Count, "StubController should not receive AnotherStubController redirect");
            Assert.AreEqual(1, c2.Received.Count);
            Assert.AreEqual("TypeSafe", c2.Received[0].action);
        }

        [Test]
        public void TypeSafeBroadcast_DeliversToAllHandlers()
        {
            var h1 = new EventHandlerController(_context);
            var h2 = new EventHandlerController(_context);
            var nonHandler = new StubController(_context, ControllerType.Page);

            _context.Broadcast(new CounterChangedEvent { NewValue = 42 });

            Assert.AreEqual(1, h1.ReceivedEvents.Count);
            Assert.AreEqual(42, h1.ReceivedEvents[0].NewValue);
            Assert.AreEqual(1, h2.ReceivedEvents.Count);
            Assert.AreEqual(42, h2.ReceivedEvents[0].NewValue);
            Assert.AreEqual(0, nonHandler.Received.Count, "Non-handler should not receive typed broadcast");
        }

        [Test]
        public void CrossContextIsolation_EventsDontLeak()
        {
            var contextA = new MvcContext();
            var contextB = new MvcContext();
            var cA = new StubController(contextA, ControllerType.Page);
            var cB = new StubController(contextB, ControllerType.Page);

            cA.LegacyBroadcast("OnlyForA");

            Assert.AreEqual(1, cA.Received.Count);
            Assert.AreEqual(0, cB.Received.Count, "Controller in contextB should not receive events from contextA");

            contextA.Dispose();
            contextB.Dispose();
        }

        [Test]
        public void Dispose_CleansUpAllReferences()
        {
            var c1 = new StubController(_context, ControllerType.Page);
            var c2 = new StubController(_context, ControllerType.Instance);

            _context.Dispose();

            Assert.Throws<ObjectDisposedException>(() =>
                _context.Broadcast(new CounterChangedEvent()));
        }

        [Test]
        public void Dispose_PreventsNewRegistrations()
        {
            _context.Dispose();

            Assert.Throws<ObjectDisposedException>(() =>
                new StubController(_context, ControllerType.Page));
        }

        [Test]
        public void ControllerDispose_UnregistersFromContext()
        {
            var c1 = new StubController(_context, ControllerType.Page);
            var c2 = new StubController(_context, ControllerType.Instance);

            c1.Dispose();

            // c2 broadcasts — c1 should not receive (it unregistered)
            c2.LegacyBroadcast("AfterDispose");

            Assert.AreEqual(0, c1.Received.Count, "Disposed controller should not receive events");
            Assert.AreEqual(1, c2.Received.Count);
        }
    }

    // -----------------------------------------------------------------------

    [TestFixture]
    public class LegacyRedirectTests
    {
        private MvcContext _context;

        [SetUp]
        public void Setup() => _context = new MvcContext();

        [TearDown]
        public void Cleanup() => _context.Dispose();

        [Test]
        public void LegacyBroadcast_DeliversToAllControllersInContext()
        {
            var c1 = new StubController(_context, ControllerType.Page);
            var c2 = new StubController(_context, ControllerType.Instance);
            var c3 = new StubController(_context, ControllerType.Instance);

            c1.LegacyBroadcast("Hello");

            Assert.AreEqual(1, c1.Received.Count);
            Assert.AreEqual(1, c2.Received.Count);
            Assert.AreEqual(1, c3.Received.Count);
            Assert.AreEqual("Hello", c2.Received[0].action);
            Assert.AreSame(c1, c2.Received[0].source);
        }

        [Test]
        public void LegacyTargetedRedirect_FiltersByTypeName()
        {
            var c1 = new StubController(_context, ControllerType.Page);
            var c2 = new AnotherStubController(_context, ControllerType.Instance);

            c1.LegacyTargetedRedirect("Ping", c2.GetType().ToString());

            Assert.AreEqual(0, c1.Received.Count, "StubController type name doesn't match AnotherStubController");
            Assert.AreEqual(1, c2.Received.Count);
        }

        [Test]
        public void LegacyBroadcast_PassesEventArgs()
        {
            var c1 = new StubController(_context, ControllerType.Page);
            var c2 = new StubController(_context, ControllerType.Instance);
            var args = new EventArgs();

            c1.LegacyBroadcast("WithData", args);

            Assert.AreSame(args, c1.Received[0].data);
            Assert.AreSame(args, c2.Received[0].data);
        }
    }

    // -----------------------------------------------------------------------

    [TestFixture]
    public class ScopedModelRegistryTests
    {
        private class IntData : ICloneable
        {
            public int X;
            public object Clone() => new IntData { X = X };
        }

        private class IntModel : Model<IntData>
        {
            public IntModel(MvcContext context, IntData data) : base(context, data) { }
        }

        private MvcContext _context;

        [SetUp]
        public void Setup() => _context = new MvcContext();

        [TearDown]
        public void Cleanup() => _context.Dispose();

        [Test]
        public void NewModel_GetsIncrementalInstanceId()
        {
            var m1 = new IntModel(_context, new IntData { X = 1 });
            var m2 = new IntModel(_context, new IntData { X = 2 });

            Assert.AreEqual(1u, m1.instanceId);
            Assert.AreEqual(2u, m2.instanceId);
            Assert.IsTrue(m1.Initiated);
        }

        [Test]
        public void GetModel_ReturnsRegisteredModel()
        {
            var m = new IntModel(_context, new IntData { X = 42 });

            var fetched = _context.GetModel<IntModel>();

            Assert.AreSame(m, fetched);
        }

        [Test]
        public void GetModelById_ReturnsRegisteredModel()
        {
            var m = new IntModel(_context, new IntData { X = 42 });

            var fetched = _context.GetModelById(m.instanceId);

            Assert.AreSame(m, fetched);
        }

        [Test]
        public void Dispose_UnregistersModel()
        {
            var m = new IntModel(_context, new IntData { X = 9 });
            var id = m.instanceId;

            m.Dispose();

            Assert.IsNull(_context.GetModelById(id));
        }

        [Test]
        public void Update_ReplacesCurrentData()
        {
            var m = new IntModel(_context, new IntData { X = 1 });

            m.Update(new IntData { X = 7 });

            Assert.AreEqual(7, m.CurrentData.X);
        }

        [Test]
        public void TwoContexts_HaveIndependentModelRegistries()
        {
            var contextA = new MvcContext();
            var contextB = new MvcContext();

            var mA = new IntModel(contextA, new IntData { X = 1 });
            var mB = new IntModel(contextB, new IntData { X = 2 });

            Assert.AreSame(mA, contextA.GetModel<IntModel>());
            Assert.AreSame(mB, contextB.GetModel<IntModel>());
            Assert.AreNotSame(mA, mB);

            contextA.Dispose();
            contextB.Dispose();
        }
    }
}
