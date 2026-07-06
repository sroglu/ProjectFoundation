using PFound.Signaling;

namespace PFound.HubApp.MiniGame.Signals
{
	/// <summary>
	/// Fired by <see cref="HubApp.Services.PhotoAlbum.IPhotoAlbumService"/> after a successful
	/// photo capture (file written, save entry appended). Home screen photo strip subscribes to
	/// refresh thumbnails. Carries no payload — listeners query the service for the latest entries.
	/// </summary>
	public class PhotoCapturedSignal : SignalBase { }
}
