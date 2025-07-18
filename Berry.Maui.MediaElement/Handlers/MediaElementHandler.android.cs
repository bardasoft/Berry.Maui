﻿using Berry.Maui.Core.Views;
using Berry.Maui.Views;
using Microsoft.Maui.Handlers;

namespace Berry.Maui.Core.Handlers;

public partial class MediaElementHandler : ViewHandler<MediaElement, MauiMediaElement>, IDisposable
{
	/// <summary>
	/// Maps the <see cref="IMediaElement.ShouldLoopPlayback"/> property between the abstract
	/// <see cref="MediaElement"/> and platform counterpart.
	/// </summary>
	/// <param name="handler">The associated handler.</param>
	/// <param name="mediaElement">The associated <see cref="MediaElement"/> instance.</param>
	public static void ShouldLoopPlayback(MediaElementHandler handler, MediaElement mediaElement)
	{
		handler.MediaManager?.UpdateShouldLoopPlayback();
	}


	protected override MauiMediaElement CreatePlatformView()
	{
		MediaManager ??= new(MauiContext ?? throw new InvalidOperationException($"{nameof(MauiContext)} cannot be null"),
								VirtualView,
								Dispatcher.GetForCurrentThread() ?? throw new InvalidOperationException($"{nameof(IDispatcher)} cannot be null"));

		var (_, playerView) = MediaManager.CreatePlatformView(VirtualView.AndroidViewType);
		return new(Context, playerView);
	}

	protected override void DisconnectHandler(MauiMediaElement platformView)
	{
		platformView.Dispose();
		Dispose();
		base.DisconnectHandler(platformView);
	}
}