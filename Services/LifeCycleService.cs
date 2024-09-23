namespace BookHeaven.Reader.Services;

public class LifeCycleService
{
	public event EventHandler? Resumed;
	public event EventHandler? Paused;
	public event EventHandler? Stopped;
	public event EventHandler? Destroyed;

	public void OnResume()
	{
		Resumed?.Invoke(this, EventArgs.Empty);
	}

	public void OnPause()
	{
		Paused?.Invoke(this, EventArgs.Empty);
	}

	public void OnStop()
	{
		Stopped?.Invoke(this, EventArgs.Empty);
	}

	public void OnDestroy()
	{
		Destroyed?.Invoke(this, EventArgs.Empty);
	}
}