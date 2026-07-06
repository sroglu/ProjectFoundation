using UnityEngine;
using PFound.ECS;
using PFound.ECS.Samples.TransformModule;
using PFound.ECS.Samples.CameraModule;
using PFound.ECS.Samples.RenderModule;
using PFound.ECS.Samples.ParticleModule;

namespace PFound.ECS.Samples
{
    /// <summary>
    /// Integration demo wiring all four sample modules onto one <see cref="World"/>: a row of
    /// spinning, color-pulsing cubes that a camera follows, plus a particle emitter — all driven by
    /// ECS systems bootstrapped from the generated registry. Put this on one GameObject and press Play.
    /// </summary>
    public sealed class SampleBootstrap : MonoBehaviour
    {
        [SerializeField] private int _cubeCount = 5;
        [SerializeField] private float _spacing = 2f;

        private World _world;

        private void Start()
        {
            _world = new World();
            Generated.SystemRegistry.Register(_world); // reflection-free, generated system list
            BuildDemo();
        }

        private void Update()
        {
            if (_world == null) return;
            _world.DeltaTime = Time.deltaTime;
            _world.UnscaledDeltaTime = Time.unscaledDeltaTime;
            _world.Update();
        }

        private void OnDestroy() => _world?.Dispose();

        private void BuildDemo()
        {
            Entity firstCube = Entity.Invalid;

            for (int i = 0; i < _cubeCount; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "EcsCube_" + i;
                go.transform.SetParent(transform, false);

                var position = new Vector3((i - (_cubeCount - 1) * 0.5f) * _spacing, 0f, 0f);

                var e = _world.Create();
                _world.Add(e, new LocalTransform { Position = position, Rotation = Quaternion.identity, Scale = Vector3.one });
                _world.Add(e, new TransformBinding { Target = go.transform });
                _world.Add(e, new Spin { DegreesPerSecond = new Vector3(0f, 45f + i * 15f, 0f) });
                _world.Add(e, new ColorPulse { ColorA = Color.cyan, ColorB = Color.magenta, Speed = 1.5f + i * 0.3f, Phase = i });
                _world.Add(e, new RendererBinding { Renderer = go.GetComponent<Renderer>() });

                if (i == 0) firstCube = e;
            }

            var cam = Camera.main;
            if (cam != null && firstCube.IsValid)
            {
                var camEntity = _world.Create();
                _world.Add(camEntity, new CameraFollow { Target = firstCube, Offset = new Vector3(0f, 4f, -10f), Smooth = 3f });
                _world.Add(camEntity, new CameraBinding { Camera = cam.transform });
            }

            var psGo = new GameObject("EcsParticles");
            psGo.transform.SetParent(transform, false);
            var ps = psGo.AddComponent<ParticleSystem>();
            var emitter = _world.Create();
            _world.Add(emitter, new EmitterControl { RateOverTime = 20f, Enabled = true });
            _world.Add(emitter, new ParticleBinding { System = ps });
        }
    }
}
