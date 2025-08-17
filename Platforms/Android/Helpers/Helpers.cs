using Android.Graphics;
using Android.Graphics.Drawables;
using Bitmap = Android.Graphics.Bitmap;

public static class Helpers
{
	public static string ConvertDrawableToBase64(Drawable drawable)
		{
			var bitmap = ConvertDrawableToBitmap(drawable);
			using (var ms = new MemoryStream())
			{
				bitmap.Compress(Bitmap.CompressFormat.Png!, 100, ms);
				return Convert.ToBase64String(ms.ToArray());
			}
		}

	public static Bitmap ConvertDrawableToBitmap(Drawable drawable)
	{
		var bitmap = Bitmap.CreateBitmap(drawable.IntrinsicWidth, drawable.IntrinsicHeight, Bitmap.Config.Argb8888!);
		using (var canvas = new Canvas(bitmap))
		{
			drawable.SetBounds(0, 0, canvas.Width, canvas.Height);
			drawable.Draw(canvas);
		}
		return bitmap;

	}
}
