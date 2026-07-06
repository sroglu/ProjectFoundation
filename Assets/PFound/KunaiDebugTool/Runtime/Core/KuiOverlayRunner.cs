using System.Collections;
using UnityEngine;

namespace Kunai
{
    // WaitForEndOfFrame yields after the camera AND any UI Toolkit / uGUI
    // panels have composited to the back buffer. Issuing the overlay's
    // CommandBuffer here guarantees Kunai sits ON TOP of every UIDocument,
    // which CameraEvent.AfterEverything (Built-in) and
    // RenderPipelineManager.endCameraRendering (SRP) cannot do — both fire
    // before screen-overlay panels are composed.
    internal class KuiOverlayRunner : MonoBehaviour
    {
        IEnumerator Start()
        {
            var wfeof = new WaitForEndOfFrame();
            while (true)
            {
                yield return wfeof;
                var ctx = KuiContext.Instance;
                if (ctx == null || !ctx.IsVisible) continue;
                ctx.Canvas?.ExecuteOnBackBuffer();
            }
        }
    }
}
