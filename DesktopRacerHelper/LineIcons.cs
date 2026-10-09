using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DesktopRacerHelper;

internal static class LineIcons
{
	private const float ViewBox = 24f;

	private const float StrokeWidth = 1.7f;

	public static Bitmap Create(string name, int size, Color color)
	{
		Bitmap bitmap = new Bitmap(size, size);
		using Graphics graphics = Graphics.FromImage(bitmap);
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		graphics.ScaleTransform((float)size / 24f, (float)size / 24f);
		using Pen pen = new Pen(color, 1.7f)
		{
			StartCap = LineCap.Round,
			EndCap = LineCap.Round,
			LineJoin = LineJoin.Round
		};
		switch (name)
		{
		case "dashboard":
			DrawDashboard(graphics, pen);
			break;
		case "warehouse":
			DrawWarehouse(graphics, pen);
			break;
		case "chest":
			DrawChest(graphics, pen);
			break;
		case "chart":
			DrawChart(graphics, pen);
			break;
		case "sliders":
			DrawSliders(graphics, pen);
			break;
		case "log":
			DrawLog(graphics, pen);
			break;
		case "gear":
			DrawGear(graphics, pen);
			break;
		case "shield":
			DrawShield(graphics, pen);
			break;
		case "github":
			DrawGithub(graphics, pen.Color);
			break;
		case "qq":
			DrawChatGroup(graphics, pen);
			break;
		case "heart":
			DrawHeart(graphics, pen.Color);
			break;
		}
		return bitmap;
	}

	private static void DrawDashboard(Graphics g, Pen pen)
	{
		g.DrawArc(pen, 3.6f, 5f, 16.8f, 16.8f, 158f, 224f);
		g.DrawLine(pen, 12f, 13.4f, 16.4f, 8.9f);
		using SolidBrush brush = new SolidBrush(pen.Color);
		g.FillEllipse(brush, 10.7f, 12.1f, 2.6f, 2.6f);
	}

	private static void DrawWarehouse(Graphics g, Pen pen)
	{
		g.DrawRectangle(pen, 3.6f, 6.6f, 16.8f, 12.8f);
		g.DrawLine(pen, 3.6f, 10.8f, 20.4f, 10.8f);
		g.DrawLine(pen, 12f, 10.8f, 12f, 19.4f);
		g.DrawLine(pen, 8.6f, 6.6f, 8.6f, 4.4f);
		g.DrawLine(pen, 15.4f, 6.6f, 15.4f, 4.4f);
		g.DrawLine(pen, 8.6f, 4.4f, 15.4f, 4.4f);
	}

	private static void DrawChest(Graphics g, Pen pen)
	{
		g.DrawRectangle(pen, 3.6f, 10.4f, 16.8f, 9f);
		g.DrawArc(pen, 3.6f, 4.4f, 16.8f, 12f, 180f, 180f);
		g.DrawLine(pen, 12f, 9.9f, 12f, 10.4f);
		g.DrawRectangle(pen, 10.4f, 12.6f, 3.2f, 3.4f);
		g.DrawLine(pen, 3.6f, 10.4f, 20.4f, 10.4f);
	}

	private static void DrawChart(Graphics g, Pen pen)
	{
		g.DrawLine(pen, 4.2f, 19.6f, 19.8f, 19.6f);
		g.DrawLine(pen, 4.4f, 19.4f, 4.4f, 5.2f);
		g.DrawRectangle(pen, 7.2f, 12.4f, 3.1f, 7.2f);
		g.DrawRectangle(pen, 12.2f, 8.8f, 3.1f, 10.8f);
		g.DrawRectangle(pen, 17.2f, 4.8f, 3.1f, 14.8f);
	}

	private static void DrawSliders(Graphics g, Pen pen)
	{
		g.DrawLine(pen, 4.2f, 8.6f, 19.8f, 8.6f);
		g.DrawEllipse(pen, 8.2f, 6.4f, 4.4f, 4.4f);
		g.DrawLine(pen, 4.2f, 15.4f, 19.8f, 15.4f);
		g.DrawEllipse(pen, 14.4f, 13.2f, 4.4f, 4.4f);
		using SolidBrush brush = new SolidBrush(Color.White);
		g.FillEllipse(brush, 8.2f, 6.4f, 4.4f, 4.4f);
		g.FillEllipse(brush, 14.4f, 13.2f, 4.4f, 4.4f);
		g.DrawEllipse(pen, 8.2f, 6.4f, 4.4f, 4.4f);
		g.DrawEllipse(pen, 14.4f, 13.2f, 4.4f, 4.4f);
	}

	private static void DrawLog(Graphics g, Pen pen)
	{
		g.DrawRectangle(pen, 4.6f, 3.6f, 14.8f, 16.8f);
		g.DrawLine(pen, 7.8f, 8.2f, 16.2f, 8.2f);
		g.DrawLine(pen, 7.8f, 12f, 16.2f, 12f);
		g.DrawLine(pen, 7.8f, 15.8f, 12.8f, 15.8f);
	}

	private static void DrawGear(Graphics g, Pen pen)
	{
		PointF pointF = new PointF(12f, 12f);
		float num = 8.4f;
		float num2 = 6.2f;
		List<PointF> list = new List<PointF>();
		for (int i = 0; i < 16; i++)
		{
			float num3 = (float)(Math.PI * 2.0 * (double)i / 16.0 - Math.PI / 2.0);
			float num4 = ((i % 2 == 0) ? num : num2);
			list.Add(new PointF(pointF.X + num4 * (float)Math.Cos(num3), pointF.Y + num4 * (float)Math.Sin(num3)));
		}
		g.DrawPolygon(pen, list.ToArray());
		g.DrawEllipse(pen, pointF.X - 2.6f, pointF.Y - 2.6f, 5.2f, 5.2f);
	}

	private static void DrawShield(Graphics g, Pen pen)
	{
		g.DrawLines(pen, new PointF[7]
		{
			new PointF(12f, 3.4f),
			new PointF(19.4f, 6.4f),
			new PointF(19.4f, 12.6f),
			new PointF(12f, 20.6f),
			new PointF(4.6f, 12.6f),
			new PointF(4.6f, 6.4f),
			new PointF(12f, 3.4f)
		});
		g.DrawLines(pen, new PointF[3]
		{
			new PointF(8.8f, 12.2f),
			new PointF(11.2f, 14.6f),
			new PointF(15.4f, 9.6f)
		});
	}

	private static void DrawGithub(Graphics g, Color color)
	{
		using SolidBrush brush = new SolidBrush(color);
		using SolidBrush brush2 = new SolidBrush(Color.White);
		g.FillEllipse(brush, 4f, 6f, 16f, 15f);
		g.FillPolygon(brush, new PointF[3]
		{
			new PointF(4.7f, 9f),
			new PointF(3.3f, 2.8f),
			new PointF(9f, 5.8f)
		});
		g.FillPolygon(brush, new PointF[3]
		{
			new PointF(19.3f, 9f),
			new PointF(20.7f, 2.8f),
			new PointF(15f, 5.8f)
		});
		g.FillEllipse(brush2, 7.6f, 10.2f, 2f, 2.4f);
		g.FillEllipse(brush2, 14.4f, 10.2f, 2f, 2.4f);
		using Pen pen = new Pen(color, 2f)
		{
			StartCap = LineCap.Round,
			EndCap = LineCap.Round
		};
		g.DrawBezier(pen, 7f, 17f, 6f, 20f, 6f, 22f, 4f, 22f);
		g.DrawBezier(pen, 17f, 17f, 18f, 20f, 18f, 22f, 20f, 22f);
	}

	private static void DrawChatGroup(Graphics g, Pen pen)
	{
		using SolidBrush brush = new SolidBrush(Color.White);
		RectangleF rectangle = new RectangleF(2.8f, 3.4f, 13.5f, 10.6f);
		RectangleF rectangle2 = new RectangleF(8f, 9f, 13.5f, 10.6f);
		using GraphicsPath path = RoundedRectangle(rectangle, 2f);
		using GraphicsPath path2 = RoundedRectangle(rectangle2, 2f);
		g.FillPath(brush, path);
		g.DrawPath(pen, path);
		g.FillPath(brush, path2);
		g.DrawPath(pen, path2);
		g.DrawLines(pen, new PointF[3]
		{
			new PointF(10f, 19.5f),
			new PointF(8f, 22f),
			new PointF(13.5f, 19.5f)
		});
		using SolidBrush brush2 = new SolidBrush(pen.Color);
		g.FillEllipse(brush2, 11.2f, 13.6f, 1.3f, 1.3f);
		g.FillEllipse(brush2, 14.1f, 13.6f, 1.3f, 1.3f);
		g.FillEllipse(brush2, 17f, 13.6f, 1.3f, 1.3f);
	}

	private static GraphicsPath RoundedRectangle(RectangleF rectangle, float radius)
	{
		GraphicsPath graphicsPath = new GraphicsPath();
		float num = radius * 2f;
		graphicsPath.AddArc(rectangle.Left, rectangle.Top, num, num, 180f, 90f);
		graphicsPath.AddArc(rectangle.Right - num, rectangle.Top, num, num, 270f, 90f);
		graphicsPath.AddArc(rectangle.Right - num, rectangle.Bottom - num, num, num, 0f, 90f);
		graphicsPath.AddArc(rectangle.Left, rectangle.Bottom - num, num, num, 90f, 90f);
		graphicsPath.CloseFigure();
		return graphicsPath;
	}

	private static void DrawHeart(Graphics g, Color color)
	{
		using GraphicsPath graphicsPath = new GraphicsPath();
		graphicsPath.StartFigure();
		graphicsPath.AddBezier(12f, 21f, 10f, 18f, 2.5f, 13.8f, 2.5f, 8.5f);
		graphicsPath.AddBezier(2.5f, 8.5f, 2.5f, 3.2f, 8.3f, 2.1f, 12f, 7.2f);
		graphicsPath.AddBezier(12f, 7.2f, 15.7f, 2.1f, 21.5f, 3.2f, 21.5f, 8.5f);
		graphicsPath.AddBezier(21.5f, 8.5f, 21.5f, 13.8f, 14f, 18f, 12f, 21f);
		graphicsPath.CloseFigure();
		using SolidBrush brush = new SolidBrush(color);
		g.FillPath(brush, graphicsPath);
	}
}
