using System;

namespace PFound.Render.ShaderWarmup
{
    /// <summary>
    /// Convenience DI helper. Constructs a <see cref="ShaderWarmupController"/> and registers it
    /// as an <see cref="IShaderWarmupController"/> singleton instance on the supplied
    /// <see cref="PFound.DependencyContainer.DependencyContainer"/>.
    /// </summary>
    public static class RenderShaderWarmupRegistration
    {
        /// <summary>
        /// Registers the controller. Returns the same instance for chained configuration
        /// (e.g., immediately setting <c>DiagnosticMode = true</c>).
        /// </summary>
        /// <exception cref="ArgumentNullException">When <paramref name="registry"/> is null.</exception>
        public static IShaderWarmupController Register(PFound.DependencyContainer.DependencyContainer registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var controller = new ShaderWarmupController();
            registry.RegisterInstance<IShaderWarmupController>(controller);
            return controller;
        }
    }
}
