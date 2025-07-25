﻿using System;
using Berry.Maui.Core.Views;
using Berry.Maui.Views;
using Tizen.Multimedia;
using Tizen.NUI.BaseComponents;
using AppFW = Tizen.Applications;

namespace Berry.Maui.Core.Views;

public partial class MediaManager : IDisposable
{
	/// <summary>
	/// The platform native counterpart of <see cref="MediaElement"/>.
	/// </summary>
	protected VideoView? VideoView { get; set; }

	/// <summary>
	/// Indicates whether <see cref="VideoView"/> is initialized.
	/// </summary>
	protected bool IsPlayerInitialized { get; set; }

	/// <summary>
	/// Indicates whether the loaded <see cref="MediaElement.Source"/> is streaming from a URI.
	/// </summary>
	protected bool IsUriStreaming { get; set; }

	/// <summary>
	/// Indicates whether the device's screen is locked.
	/// </summary>
	protected bool IsScreenLocked { get; set; }
	
	/// <summary>
	/// Releases the managed and unmanaged resources used by the <see cref="MediaManager"/>.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Creates the corresponding platform view of <see cref="MediaElement"/> on Tizen.
	/// </summary>
	/// <returns>The platform native counterpart of <see cref="MediaElement"/>.</returns>
	public VideoView CreatePlatformView()
	{
		VideoView = new VideoView()
		{
			WidthSpecification = LayoutParamPolicies.MatchParent,
			HeightSpecification = LayoutParamPolicies.MatchParent
		};

		VideoView.AddedToWindow += AddedToWindow;

		return VideoView;
	}

	protected virtual partial void PlatformPlay()
	{
		if (Player is null)
		{
			return;
		}

		if (Player.State is PlayerState.Ready || Player.State is PlayerState.Paused)
		{
			Player.Start();
			UpdateCurrentState();
		}
	}

	protected virtual partial void PlatformPause()
	{
		if (Player is null)
		{
			return;
		}

		if (Player.State is PlayerState.Playing)
		{
			Player.Pause();
			UpdateCurrentState();
		}
	}

	protected virtual async partial Task PlatformSeek(TimeSpan position, CancellationToken token)
	{
		if (Player is null)
		{
			throw new InvalidOperationException($"{nameof(Berry.Maui.Core.Views.TizenPlayer)} is not yet initialized");
		}

		if (Player.State is not (PlayerState.Ready or PlayerState.Playing or PlayerState.Paused))
		{
			throw new InvalidOperationException($"{nameof(Berry.Maui.Core.Views.TizenPlayer)}.{nameof(Berry.Maui.Core.Views.TizenPlayer.State)} must first be set to {PlayerState.Ready}, {PlayerState.Playing} or {PlayerState.Paused}");
		}

		await Player.SetPlayPositionAsync((int)position.TotalMilliseconds, false).WaitAsync(token);

		MediaElement.SeekCompleted();
	}

	protected virtual partial void PlatformStop()
	{
		if (Player is null)
		{
			return;
		}

		if (Player.State is PlayerState.Playing || Player.State is PlayerState.Paused)
		{
			Player.Stop();
			MediaElement.Position = TimeSpan.Zero;
			MediaElement.CurrentStateChanged(MediaElementState.Stopped);
		}

		MediaElement.Position = TimeSpan.Zero;
	}

	protected virtual partial void PlatformUpdateAspect()
	{
		if (Player is null)
		{
			return;
		}

		Player.DisplaySettings.Mode = MediaElement.Aspect switch
		{
			Aspect.AspectFill => PlayerDisplayMode.CroppedFull,
			Aspect.AspectFit => PlayerDisplayMode.LetterBox,
			Aspect.Fill => PlayerDisplayMode.FullScreen,
			_ => PlayerDisplayMode.LetterBox,
		};
	}

	protected virtual partial ValueTask PlatformUpdateSource()
	{
		if (Player is null)
		{
			return ValueTask.CompletedTask;
		}

		if (Player.State is not PlayerState.Idle)
		{
			Player.Unprepare();
		}

		if (MediaElement.Source is null)
		{
			Player.SetSource(null);

			MediaElement.Duration = TimeSpan.Zero;
			MediaElement.MediaWidth = MediaElement.MediaHeight = 0;

			MediaElement.CurrentStateChanged(MediaElementState.None);
			
			return ValueTask.CompletedTask;
		}

		MediaElement.CurrentStateChanged(MediaElementState.Opening);

		if (MediaElement.Source is UriMediaSource uriMediaSource)
		{
			var uri = uriMediaSource.Uri;
			if (!string.IsNullOrWhiteSpace(uri?.AbsoluteUri))
			{
				Player.SetSource(new MediaUriSource(uri.AbsoluteUri));
				IsUriStreaming = true;
			}
		}
		else if (MediaElement.Source is FileMediaSource fileMediaSource)
		{
			var path = fileMediaSource.Path;
			if (!string.IsNullOrWhiteSpace(path))
			{
				Player.SetSource(new MediaUriSource(path));
				IsUriStreaming = false;
			}
		}
		else if (MediaElement.Source is ResourceMediaSource resourceMediaSource)
		{
			var path = resourceMediaSource.Path;
			if (!string.IsNullOrWhiteSpace(path))
			{
				Player.SetSource(new MediaUriSource(GetResourcePath(path)));
				IsUriStreaming = false;
			}
		}

		if (Player.IsSourceSet)
		{
			PreparePlayer();
			MediaElement.MediaOpened();
		}

		return ValueTask.CompletedTask;
	}

	protected virtual partial void PlatformUpdateSpeed()
	{
		if (Player is null)
		{
			return;
		}

		if (!IsUriStreaming && MediaElement.Speed <= 5.0f && MediaElement.Speed >= -5.0f && MediaElement.Speed != 0)
		{
			if (Player.State is PlayerState.Ready || Player.State is PlayerState.Playing || Player.State is PlayerState.Paused)
			{
				Player.SetPlaybackRate((float)MediaElement.Speed);
			}
		}
	}

	protected virtual partial void PlatformUpdateShouldShowPlaybackControls()
	{
		if (VideoView is null)
		{
			return;
		}
	}

	protected virtual partial void PlatformUpdatePosition()
	{
		if (Player is null)
		{
			return;
		}

		if (Player.State is PlayerState.Ready || Player.State is PlayerState.Playing || Player.State is PlayerState.Paused)
		{
			MediaElement.Duration = TimeSpan.FromMilliseconds(Player.StreamInfo.GetDuration());
			MediaElement.Position = TimeSpan.FromMilliseconds(Player.GetPlayPosition());
		}
		else
		{
			MediaElement.Duration = MediaElement.Position = TimeSpan.Zero;
		}
	}

	protected virtual partial void PlatformUpdateVolume()
	{
		if (Player is null)
		{
			return;
		}

		if (MediaElement.Volume >= 0.0 && MediaElement.Volume <= 1.0)
		{
			Player.Volume = (float)MediaElement.Volume;
		}
	}

	protected virtual partial void PlatformUpdateShouldKeepScreenOn()
	{
		if (VideoView is null)
		{
			return;
		}

		if (MediaElement.ShouldKeepScreenOn && !IsScreenLocked)
		{
			Tizen.System.Power.RequestLock(Tizen.System.PowerLock.DisplayNormal, 0);
			Tizen.System.Power.RequestLock(Tizen.System.PowerLock.Cpu, 0);
			IsScreenLocked = true;
		}
		else if (!MediaElement.ShouldKeepScreenOn && IsScreenLocked)
		{
			Tizen.System.Power.ReleaseLock(Tizen.System.PowerLock.DisplayNormal);
			Tizen.System.Power.ReleaseLock(Tizen.System.PowerLock.Cpu);
			IsScreenLocked = false;
		}
	}

	protected virtual partial void PlatformUpdateShouldMute()
	{
		if (Player is null)
		{
			return;
		}

		Player.Muted = MediaElement.ShouldMute;
	}

	protected virtual partial void PlatformUpdateShouldLoopPlayback()
	{
		if (Player is null || VideoView is null)
		{
			return;
		}

		Player.IsLooping = MediaElement.ShouldLoopPlayback;

	}

	/// <summary>
	/// Releases the unmanaged resources used by the <see cref="MediaManager"/> and optionally releases the managed resources.
	/// </summary>
	/// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			if (Player is not null)
			{
				Player.PlaybackCompleted -= OnPlaybackCompleted;
				Player.ErrorOccurred -= OnErrorOccurred;
				Player.BufferingProgressChanged -= OnBufferingProgressChanged;
				Player.Dispose();
			}
			if (VideoView is not null)
			{
				VideoView.Dispose();
			}
		}
	}

	void AddedToWindow(object? sender, EventArgs e)
	{
		if (!IsPlayerInitialized && VideoView is not null)
		{
			InitializePlayer(VideoView);
			IsPlayerInitialized = true;
		}

		if (VideoView is not null)
		{
			VideoView.AddedToWindow -= AddedToWindow;
		}
	}

	void OnPlaybackCompleted(object? sender, EventArgs e)
	{
		MediaElement.MediaEnded();
		MediaElement.CurrentStateChanged(MediaElementState.Stopped);
	}

	void OnErrorOccurred(object? sender, PlayerErrorOccurredEventArgs e)
	{
		MediaElement.MediaFailed(new MediaFailedEventArgs(e.Error.ToString()));
	}

	void OnBufferingProgressChanged(object? sender, BufferingProgressChangedEventArgs e)
	{
		if (e.Percent is 100)
		{
			UpdateCurrentState();
		}
		else
		{
			MediaElement.CurrentStateChanged(MediaElementState.Buffering);
		}
	}

	void InitializePlayer(VideoView VideoView)
	{
		var handle = VideoView.NativeHandle;
		if (handle.IsInvalid)
		{
			throw new InvalidOperationException("The NativeHandler is invalid");
		}

		if (Player is null)
		{
			Player = new TizenPlayer(handle.DangerousGetHandle());
			Player.InitializePlayer();
			Player.PlaybackCompleted += OnPlaybackCompleted;
			Player.ErrorOccurred += OnErrorOccurred;
			Player.BufferingProgressChanged += OnBufferingProgressChanged;
			PlatformUpdateSource();
		}
	}

	void UpdateCurrentState()
	{
		if (Player is null)
		{
			throw new InvalidOperationException("TizenPlayer must not be null.");
		}

		var newsState = Player.State switch
		{
			PlayerState.Idle => MediaElementState.None,
			PlayerState.Ready => MediaElementState.Opening,
			PlayerState.Playing => MediaElementState.Playing,
			PlayerState.Paused => MediaElementState.Paused,
			_ => MediaElementState.None
		};

		MediaElement.CurrentStateChanged(newsState);
	}

	string GetResourcePath(string res)
	{
		if (System.IO.Path.IsPathRooted(res))
		{
			return res;
		}

		foreach (AppFW.ResourceManager.Category category in Enum.GetValues<AppFW.ResourceManager.Category>())
		{
			var path = AppFW.ResourceManager.TryGetPath(category, res);

			if (path != null)
			{
				return path;
			}
		}

		AppFW.Application app = AppFW.Application.Current;
		if (app != null)
		{
			string resPath = app.DirectoryInfo.Resource + res;
			if (File.Exists(resPath))
			{
				return resPath;
			}
		}

		return res;
	}

	async void PreparePlayer()
	{
		if (Player is not null)
		{
			await Player.PrepareAsync();

			var videoSize = Player.StreamInfo.GetVideoProperties().Size;
			MediaElement.MediaWidth = (int)videoSize.Width;
			MediaElement.MediaHeight = (int)videoSize.Height;

			PlatformUpdatePosition();
			UpdateCurrentState();
		}
	}
}
