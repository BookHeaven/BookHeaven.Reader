using Android.Graphics;
using Android.Graphics.Drawables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class Helpers
{
	public static string ConvertDrawableToBase64(Drawable drawable)
		{
			Bitmap bitmap = ConvertDrawableToBitmap(drawable);
			using (MemoryStream ms = new MemoryStream())
			{
				bitmap.Compress(Bitmap.CompressFormat.Png!, 100, ms);
				return Convert.ToBase64String(ms.ToArray());
			}
		}

	public static Bitmap ConvertDrawableToBitmap(Drawable drawable)
	{
		Bitmap bitmap = Bitmap.CreateBitmap(drawable.IntrinsicWidth, drawable.IntrinsicHeight, Bitmap.Config.Argb8888!);
		using (Canvas canvas = new Canvas(bitmap))
		{
			drawable.SetBounds(0, 0, canvas.Width, canvas.Height);
			drawable.Draw(canvas);
		}
		return bitmap;

	}
}
