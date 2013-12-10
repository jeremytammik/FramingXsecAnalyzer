#region Namespaces
using System;
using System.Collections.Generic;
//using System.Diagnostics;
using System.Drawing;
//using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using System.Diagnostics;
#endregion

namespace FramingXsecAnalyzer
{
  class GeoSnoop
  {
    static double MinsteAvListe( List<double> list )
    {
      double minste = list[0];
      foreach( double d in list )
      {
        if( d < minste )
          minste = d;
      }
      return minste;
    }

    static double StorsteAvListe( List<double> list )
    {
      double storste = list[0];
      foreach( double d in list )
      {
        if( d > storste )
          storste = d;
      }
      return storste;
    }

    static List<PointF[]> Make2DCurvesFromXYZ3DDataJustDroppingZ( List<List<XYZ>> xyzarrlist )
    {
      List<PointF[]> curves = new List<PointF[]>();

      foreach( List<XYZ> xyzarr in xyzarrlist )
      {
        PointF[] pfarr = new PointF[xyzarr.Count];
        for( int i = 0; i < xyzarr.Count; i++ )
        {
          XYZ xyz = xyzarr[i];

          pfarr[i] = new PointF( (float) xyz.X, (float) xyz.Y );
        }
        curves.Add( pfarr );
      }
      return curves;
    }

    static PointF GetPointF( XYZ p,
      AnalyticalDirection dropCoordinate )
    {
      if( AnalyticalDirection.X == dropCoordinate )
      {
        return new PointF( (float) p.Y, (float) p.Z );
      }
      else if( AnalyticalDirection.Y == dropCoordinate )
      {
        return new PointF( (float) p.X, (float) p.Z );
      }
      else
      {
        Debug.Assert(
          AnalyticalDirection.Z == dropCoordinate,
          "expected X, Y or Z" );

        return new PointF( (float) p.X, (float) p.Y );
      }
    }

    static List<PointF[]> GetPointLoops(
      EdgeArrayArray eaa,
      AnalyticalDirection dropCoordinate )
    {
      int n = eaa.Size;

      List<PointF[]> loops = new List<PointF[]>( n );

      foreach( EdgeArray ea in eaa )
      {
        PointF[] loop = new PointF[ea.Size + 1];

        int i = 0;
        XYZ p0 = null;

        foreach( Edge e in ea )
        {
          XYZ p = e.AsCurve().get_EndPoint( 0 );
          loop[i] = GetPointF( p, dropCoordinate );
          if( null == p0 ) { p0 = p; }
          ++i;
        }
        loop[i] = GetPointF( p0, dropCoordinate );
        loops.Add( loop );
      }
      return loops;
    }

    static List<List<XYZ>> PointArrayListToXYZArrayList( List<PointF[]> pointarraylist )
    {
      List<List<XYZ>> xyzarraylist = new List<List<XYZ>>();
      foreach( PointF[] pfarr in pointarraylist )
      {
        if( pfarr.Length > 0 )
        {
          List<XYZ> xyzarr = new List<XYZ>();
          for( int i = 0; i < pfarr.Length; i++ )
          {
            xyzarr.Add( new XYZ( pfarr[i].X, pfarr[i].Y, 0 ) );
          }
          xyzarraylist.Add( xyzarr );
        }
      }
      return xyzarraylist;
    }

    static PointF[] GiveCornersOfCurves( List<PointF[]> pflist )
    {
      List<XYZ> xyzarr = GiveCornersOfCurves( PointArrayListToXYZArrayList( pflist ) );
      XYZ min = xyzarr[0];
      XYZ max = xyzarr[1];
      PointF[] ret = new PointF[] { new PointF( (float) min.X, (float) min.Y ), new PointF( (float) max.X, (float) max.Y ) };
      return ret;
    }

    static List<XYZ> GiveCornersOfCurves( List<List<XYZ>> xyzarraylist )
    {
      List<XYZ> ret = new List<XYZ>();
      if( xyzarraylist != null && xyzarraylist.Count > 0 )
      {

        double minx = 0;
        double miny = 0;
        double minz = 0;
        double maxx = 0;
        double maxy = 0;
        double maxz = 0;

        List<double> xer = new List<double>();
        List<double> yer = new List<double>();
        List<double> zer = new List<double>();

        foreach( List<XYZ> pfarr in xyzarraylist )
        {
          for( int i = 0; i < pfarr.Count; i++ )
          {
            XYZ pf = pfarr[i];
            xer.Add( pf.X );
            yer.Add( pf.Y );
            zer.Add( pf.Z );
          }
        }
        minx = MinsteAvListe( xer );
        miny = MinsteAvListe( yer );
        minz = MinsteAvListe( zer );

        maxx = StorsteAvListe( xer );
        maxy = StorsteAvListe( yer );
        maxz = StorsteAvListe( zer );

        ret.Add( new XYZ( minx, miny, minz ) );
        ret.Add( new XYZ( maxx, maxy, maxz ) );
      }
      return ret;
    }

    static XYZ FinnOrigo( List<PointF[]> pflist )
    {
      List<List<XYZ>> xyarraylist = PointArrayListToXYZArrayList( pflist );
      return FinnOrigo( xyarraylist );
    }

    static XYZ FinnOrigo( List<List<XYZ>> pflist )
    {
      XYZ ret = XYZ.Zero;
      if( pflist != null && pflist.Count > 0 )
      {
        List<XYZ> Corners = GiveCornersOfCurves( pflist );
        XYZ min = Corners[0];
        XYZ max = Corners[1];
        double x = ( max.X + min.X ) / 2;
        double y = ( max.Y + min.Y ) / 2;
        double z = ( max.Z + min.Z ) / 2;

        ret = new XYZ( x, y, z );
      }
      return ret;
    }

    static void FinnMinMaxSkaleringOrigo( List<PointF[]> curve3Ds, SizeF WantedSize,
       ref float origox, ref float origoy,
       ref float scalex, ref float scaley,
       ref float translatex, ref float translatey, out float width, out float height
       , bool flipscaley )
    {
      PointF[] corners = GiveCornersOfCurves( curve3Ds );
      PointF min = corners[0];
      PointF max = corners[1];
      width = max.X - min.X;
      height = max.Y - min.Y;
      if( height == 0 && width != 0 )
        height = width;
      if( width == 0 && height != 0 )
        width = height;

      if( width > 0 && height > 0 )
      {
        scalex = WantedSize.Width / width;
        scaley = WantedSize.Height / height;

        float scalemargin = 0.7F;
        scalex = scalex * scalemargin;
        scaley = scaley * scalemargin;
        float scaleuniform = Math.Min( scalex, scaley );
        scalex = scaleuniform;
        scaley = scaleuniform;


        if( flipscaley )
          scaley = -scaley;

        //origox = (max.X + min.X) / 2;
        //origoy = (max.Y + min.Y) / 2;
        XYZ origo = FinnOrigo( curve3Ds );
        origox = (float) origo.X;
        origoy = (float) origo.Y;

        translatex = ( WantedSize.Width / 2 - ( origox * scalex ) );
        translatey = ( WantedSize.Height / 2 - ( origoy * scaley ) );
      }

      else
      {
        scalex = 1;
        scaley = 1;
        if( flipscaley )
          scaley = -scaley;
        translatex = ( WantedSize.Width / 2 - ( origox * scalex ) );
        translatey = ( WantedSize.Height / 2 - ( origoy * scaley ) );
      }
    }

    /// <summary>
    /// Draw curves on graphics with transform and given pen
    /// </summary>
    static void DrawCurves(
      Graphics graphics,
      List<PointF[]> curves,
      System.Drawing.Drawing2D.Matrix transform,
      Pen pen )
    {
      foreach( PointF[] curve in curves )
      {
        System.Drawing.Drawing2D.GraphicsPath gPath = new System.Drawing.Drawing2D.GraphicsPath();
        if( curve.Length == 0 )
        {
          break;
        }
        if( curve.Length == 1 )
        {
          gPath.AddArc( new RectangleF( curve[0], new SizeF( 0.5f, 0.5f ) ), 0.0f, (float) Math.PI );
        }
        else
        {
          gPath.AddLines( curve );
        }
        if( transform != null )
          gPath.Transform( transform );

        graphics.DrawPath( pen, gPath );
      }
    }

    static void DrawCurvesOnGraphics(
      Graphics graphics,
      Size WantedSize,
      List<PointF[]> curve3Ds,
      bool bSkalerOgTranslater,
      float translatex,
      float translatey,
      float scalex,
      float scaley,
      System.Drawing.Color farge,
      float pensize )
    {
      graphics.Clear( System.Drawing.Color.White );
      graphics.Transform = new System.Drawing.Drawing2D.Matrix();
      System.Drawing.Drawing2D.Matrix trans = graphics.Transform.Clone();

      if( bSkalerOgTranslater )
      {
        graphics.TranslateTransform( translatex, translatey );
        graphics.ScaleTransform( scalex, scaley );
        trans = graphics.Transform.Clone();
        graphics.Transform = new System.Drawing.Drawing2D.Matrix();
      }
      DrawCurves( graphics, curve3Ds, trans, new Pen( farge, pensize ) );
    }

    static public void ShowCurve( 
      string caption,
      EdgeArrayArray eaa,
      AnalyticalDirection dropCoordinate )
    {
      float width = 400;
      float height = 400;
      Bitmap bmp = new Bitmap( (int) width, (int) height );
      //List<PointF[]> pointcurve = Make2DCurvesFromXYZ3DDataJustDroppingZ( curves );
      List<PointF[]> pointLoops = GetPointLoops( eaa, dropCoordinate );
      float origox = 0;
      float origoy = 0;
      float scalex = 1;
      float scaley = 1;
      float translatex = 0;
      float translatey = 0;
      SizeF fwantedsize = new SizeF( width, height );
      FinnMinMaxSkaleringOrigo( pointLoops, fwantedsize, ref origox, ref origoy, ref scalex, ref scaley, ref translatex, ref translatey, out width, out height, false );
      Graphics gr = Graphics.FromImage( bmp );
      DrawCurvesOnGraphics( gr, bmp.Size, pointLoops, true, translatex, translatey, scalex, scaley, System.Drawing.Color.Black, 1 );

      PictureBox pb = new PictureBox();
      pb.Image = bmp;
      pb.Size = bmp.Size;
      System.Windows.Forms.Form form = new System.Windows.Forms.Form();
      form.Size = new Size( bmp.Width + 10, bmp.Height + 10 );
      form.Text = caption;
      pb.Parent = form;
      pb.Location = new System.Drawing.Point( 0, 0 );
      form.Show();
    }
  }
}
