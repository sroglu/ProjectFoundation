using System.Threading;
using Cysharp.Threading.Tasks;

namespace PFound.HubApp.MiniGame
{
	/// <summary>
	/// Optional companion to <see cref="IMiniGameModule"/> for modules that load content
	/// asynchronously inside <see cref="IMiniGameModule.Initialize"/> (e.g. an AssetSystem
	/// environment prefab). The host fires its <c>onReady</c> callback — which dismisses the
	/// loading cover — only AFTER <see cref="WaitUntilReadyAsync"/> completes, so the cover stays
	/// up (and hides the heavy load + first-render shader compilation behind it) until the module
	/// is actually ready to be shown.
	/// </summary>
	/// <remarks>
	/// Modules that do NOT implement this are considered ready the instant <c>Initialize</c> returns
	/// (unchanged behaviour). If <see cref="WaitUntilReadyAsync"/> throws or never completes, the
	/// loading cover's own safety timeout still force-hides it, so a stuck load can't block forever.
	/// </remarks>
	public interface IMiniGameModuleAsyncReady
	{
		/// <summary>Completes when the module's async content load is done and the scene is safe to reveal.</summary>
		UniTask WaitUntilReadyAsync(CancellationToken ct);
	}
}
