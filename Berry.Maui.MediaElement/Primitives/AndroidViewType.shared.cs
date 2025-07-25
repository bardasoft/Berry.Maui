using Berry.Maui.Views;

namespace Berry.Maui.Core;

/// <summary>
/// Enum for <see cref="MediaElement"/> view type on Android
/// </summary>
public enum AndroidViewType
{
	/// <summary>
	/// Create MediaElement on Android using SurfaceView
	/// </summary>
	SurfaceView,

	/// <summary>
	/// Create MediaElement on Android using TextureView
	/// </summary>
	TextureView
}