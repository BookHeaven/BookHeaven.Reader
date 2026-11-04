namespace BookHeaven.Reader.Features.Lifecycle.Services;

public sealed class LifeCycleService
{
	public Action? Resumed;
	public Action? Paused;
	public Action? Stopped;
	public Action? Destroyed;
	
	public Action? ScreenOn;
	public Action? ScreenOff;
}