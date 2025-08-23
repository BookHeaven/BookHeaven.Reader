namespace BookHeaven.Reader.Services;

public class LifeCycleService
{
	public Action? Resumed;
	public Action? Paused;
	public Action? Stopped;
	public Action? Destroyed;
	
	public Action? ScreenOn;
	public Action? ScreenOff;
}