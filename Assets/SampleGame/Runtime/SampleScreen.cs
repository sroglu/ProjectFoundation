using PFound.ScreenRouter;

namespace PFound.SampleGame
{
    /// <summary>
    /// The sample game's landing screen. It carries no bespoke behaviour — the point is to show the
    /// smallest thing <c>ScreenRouter.SwitchScreen</c> can route to. To see it on screen, author a
    /// prefab with an <c>IContentRenderer</c> (e.g. <c>CanvasGroupContentRenderer</c>), add a
    /// <c>ScreenDefinition</c> for it to the ScreenRouterConfig, and drop a <c>ScreenRouterHost</c>
    /// into the scene. Without a host the bootstrap simply skips routing.
    /// </summary>
    public sealed class SampleScreen : Screen { }
}
