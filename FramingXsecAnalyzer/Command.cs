#define USING_REX

#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using RvtOperationCanceledException = Autodesk.Revit.Exceptions.OperationCanceledException;
using Application = Autodesk.Revit.ApplicationServices.Application;

#if USING_REX
using REX.ContentGenerator.Converters;
using REX.ContentGenerator.Families;
using REX.ContentGenerator.Geometry;
#endif // USING_REX
#endregion

namespace FramingXsecAnalyzer
{
  [Transaction( TransactionMode.Manual )]
  public class Command : IExternalCommand
  {
    #region Obsolete attempts
#if OBSOLETE_ATTEMPTS
    static int [] _framing_category_ints = new int [] {
      (int) BuiltInCategory.OST_StructuralColumns,
      (int) BuiltInCategory.OST_StructuralFraming };

    bool GetFramingPoints( Element e, out XYZ p, out XYZ q )
    {
      p = q = null;

      bool rc = false;

      if( e is FamilyInstance
        && null != e.Category )
      {
        Category cat = e.Category;

        //int ibic = (int) cat.Id.IntegerValue;
        //if( _framing_category_ints.Contains<int>( 
        //  ibic ) ) { }

        int icat = cat.Id.IntegerValue;

        if( icat.Equals( 
          (int) BuiltInCategory.OST_StructuralFraming ) )
        {
          LocationCurve lc = e.Location as LocationCurve;

          if( null != lc )
          {
            Line line = lc.Curve as Line;

            if( null != line )
            {
              p = line.get_EndPoint( 0 );
              q = line.get_EndPoint( 1 );
              rc = true;
            }
          }
        }
        else if( icat.Equals(
          (int) BuiltInCategory.OST_StructuralColumns ) )
        {
          AnalyticalModel am = e.GetAnalyticalModel();

          if( null != am )
          {
            Line line = am.GetCurve() as Line;

            if( null != line )
            {
              p = line.get_EndPoint( 0 );
              q = line.get_EndPoint( 1 );
              rc = true;
            }
          }
        }
      }
      return rc;
    }

    ViewSection GetFrontView( Element e )
    {
      // Retrieve element bounding box

      BoundingBoxXYZ bb = e.get_BoundingBox( null );

      // Determine box size

      XYZ size = bb.Max - bb.Min;

      // Set up view from front with small gap 
      // between section and element

      //XYZ pMax = new XYZ( -0.5 * size.X, 0.5 * size.Z,  0.5 * size.Y );
      //XYZ pMin = new XYZ(  0.5 * size.X, -0.5 * size.Z, -0.5 * size.Y - 0.2 );

      // Set up view from front in element midpoint

      XYZ pMax = new XYZ( -0.5 * size.X, 0.5 * size.Z,  0.5 * size.Y );
      XYZ pMin = new XYZ( 0.5 * size.X, -0.5 * size.Z, -0.5 * size.Y - 0.2 );

      BoundingBoxXYZ bbView = new BoundingBoxXYZ();
      bbView.Enabled = true;
      bbView.Max = pMax;
      bbView.Min = pMin;

      // Set the transform

      Transform tx = Transform.Identity;

      // Determine element midpoint

      XYZ pMid = 0.5 * ( bb.Max + bb.Min );

      // Set it as origin

      tx.Origin = pMid;

      // Set view direction

      tx.BasisX = -XYZ.BasisX;
      tx.BasisY = XYZ.BasisZ;
      tx.BasisZ = XYZ.BasisY;

      bbView.Transform = tx;

      // Create and return section view

      Document doc = e.Document;

      Transaction t = new Transaction( doc );
      t.Start( "Create Section View" );
      
      ViewSection viewSection = doc.Create.NewViewSection( bbView );
      
      t.Commit();

      return viewSection;
    }
#endif // OBSOLETE_ATTEMPTS
    #endregion // Obsolete attempts

    #region Using REX
#if USING_REX
    /// <summary>
    /// Use REX to analyse element cross section.
    /// This requires a reference to 
    /// REX.ContentGeneratorLT.dll and prior
    /// initialisation of the REX framework.
    /// The converter initialisation must reside in
    /// a different method than the subscription to
    /// the assembly resolver OnAssemblyResolve.
    /// </summary>
    void RexXsecAnalyis(
      ExternalCommandData commandData,
      Element e )
    {
      // Initialise converter

      RVTFamilyConverter rvt = new RVTFamilyConverter(
        commandData, true );

      // Retrieve family type

      REXFamilyType fam = rvt.GetFamily( e,
        ECategoryType.SECTION_PARAM );

      // Retrieve section data

      REXFamilyType_ParamSection paramSection = fam
        as REXFamilyType_ParamSection;

      REXSectionParamDescription parameters
        = paramSection.Parameters;

      // Extract dimensions, section type, tapered
      // predicate, etc.
      // If different start and end sections are 
      // required, use DimensionsEnd as well.

      REXSectionParamDimensions dimensions = parameters
        .Dimensions;

      ESectionType sectionType = parameters
        .SectionType;

      bool tapered = parameters.Tapered;

      bool start = true;

      Contour_Section contour = parameters.GetContour(
        start );

      List<ContourCont> shape = contour.Shape;

      Debug.Print(
        "The selected structural framing element "
        + "cross section REX section type is "
        + "{0}.", sectionType );
    }

    static System.Reflection.Assembly OnAssemblyResolve(
      object sender,
      ResolveEventArgs args )
    {
      Assembly a = Assembly.GetExecutingAssembly();

      return Autodesk.REX.Framework.REXAssemblies
        .Resolve( sender, args, "2014", a );
    }
#endif // Using REX
    #endregion // Using REX

    public Result Execute(
      ExternalCommandData commandData,
      ref string message,
      ElementSet elements )
    {

#if USING_REX
      AppDomain.CurrentDomain.AssemblyResolve
        += new ResolveEventHandler( OnAssemblyResolve );
#endif // Using REX

      UIApplication uiapp = commandData.Application;
      UIDocument uidoc = uiapp.ActiveUIDocument;
      Application app = uiapp.Application;

      Document doc = Util.GetActiveDocument(
        uidoc, false );

      if( null == doc )
      {
        return Result.Failed;
      }

      // Retrieve pre-selected or 
      // interactively select element 

      Selection sel = uidoc.Selection;

      int n = sel.Elements.Size;

      if( 1 < n )
      {
        Util.InfoMessage( string.Format(
          "{0} element{1} selected. Please select one "
          + "single framing element for cross section "
          + "analysis.",
          n, Util.PluralSuffix( n ) ) );

        return Result.Failed;
      }

      Element e = null;

      if( 0 < n )
      {
        Debug.Assert( 1 == n,
          "we already checked for 1 < n above" );

        foreach( Element e2 in sel.Elements )
        {
          e = e2;
        }
      }
      else
      {
        try
        {
          Reference r = sel.PickObject(
            ObjectType.Element,
            "Please pick a framing element "
            + "for cross section analysis." );

          e = doc.GetElement( r.ElementId );
        }
        catch( RvtOperationCanceledException )
        {
          return Result.Cancelled;
        }
      }

      #region Obsolete attempts
#if OBSOLETE_ATTEMPTS
      // Retrieve framing start and end points

      XYZ p, q;

      if( !GetFramingPoints( e, out p, out q ) )
      {
        InfoMessage( "Sorry, I am unable to retrieve "
          + "the structural framing start and end "
          + "points from the selected element." );

        return Result.Failed;
      }
#endif // OBSOLETE_ATTEMPTS
      #endregion // Obsolete attempts

      // Set up section view

      //ViewSection viewSection = GetFrontView( e );

      // Retrieve element solid

      Options opt = app.Create.NewGeometryOptions();

      //opt.IncludeNonVisibleObjects = true;

      // Cannot set both detail level and view:
      // Autodesk.Revit.Exceptions.InvalidOperationException
      // DetailLevel is already set. When DetailLevel is set 
      // view-specific geometry can't be extracted.

      //opt.DetailLevel = DetailLevels.MAX;

      View view = doc.ActiveView;
      opt.View = view;

      //opt.View = viewSection;

      GeometryElement geo = e.get_Geometry( opt );
      GeometryInstance inst = null;

      foreach( GeometryObject obj in geo )
      {
        inst = obj as GeometryInstance;
        if( null != inst )
        {
          break;
        }
      }
      if( null == inst )
      {
        Util.InfoMessage( "Sorry, I am unable to retrieve "
          + "the structural framing geometry instance "
          + "from the selected element." );

        return Result.Failed;
      }
      Solid solid = null;

      //geo = inst.SymbolGeometry;
      geo = inst.GetInstanceGeometry();

      foreach( GeometryObject obj in geo )
      {
        solid = obj as Solid;

        if( null != solid
          && 0 != solid.Faces.Size )
        {
          break;
        }
      }

      #region Obsolete attempts
#if OBSOLETE_ATTEMPTS
      // Set up extrusion analyser

      XYZ direction = q - p;

      Plane plane = app.Create.NewPlane(
        direction, p + 0.5 * direction );

      ExtrusionAnalyzer ea = ExtrusionAnalyzer.Create(
        solid, plane, direction );

      Face face = ea.GetExtrusionBase();

      EdgeArrayArray eaa = face.EdgeLoops;

      n = eaa.Size;

      Debug.Print(
        "The selected structural framing element "
        + "cross section generates {0} "
        + "ExtrusionAnalyzer loop{1}.",
        n, Util.PluralSuffix( n ) );

      GeoSnoop.ShowCurve( "Extrusion Analyzer", eaa );

      // Extract faces in section view plane

      Plane viewPlane = app.Create.NewPlane(
        view.RightDirection, 
        view.UpDirection, 
        view.Origin );

      if( null != view.SketchPlane )
      {
        Debug.Assert( viewDir.IsAlmostEqualTo( 
          view.SketchPlane.Plane.Normal ), 
          "expected same view plane from sketch plane" );

        Debug.Assert( view.Origin.IsAlmostEqualTo( 
          view.SketchPlane.Plane.Origin ),
          "expected same view plane from sketch plane" );
      }
#endif // OBSOLETE_ATTEMPTS
      #endregion // Obsolete attempts

      // Select the first planar face which is 
      // perpendicular to view direction, i.e. has
      // same normal vector as the view plane.
      // Assuming the cross section is constant,
      // any one will do. We expect two of them.

      XYZ viewDir = view.ViewDirection;

      PlanarFace crossSection = null;

      foreach( Face f in solid.Faces )
      {
        if( f is PlanarFace
          && Util.IsParallel( viewDir,
            ( f as PlanarFace ).Normal ) )
        {
          crossSection = f as PlanarFace;

          break;
        }
      }

      if( null == crossSection )
      {
        Util.InfoMessage( "Sorry, I am unable to retrieve "
          + "the structural framing cross section." );

        return Result.Failed;
      }

      EdgeArrayArray eaa = crossSection.EdgeLoops;

      n = eaa.Size;

      Debug.Print(
        "The selected structural framing element "
        + "cross section section view cut plane "
        + "face has {0} loop{1} and is thus '{2}'.",
        n, Util.PluralSuffix( n ),
        ( 1 == n ? "open" : "closed" ) );

      GeoSnoop.ShowCurve( "Solid face directly",
        eaa, AnalyticalDirection.Y );

#if USING_REX

      RexXsecAnalyis( commandData, e );

#endif // Using REX

      return Result.Succeeded;
    }
  }
}

//<HintPath>a:\references\Autodesk.Common.AResourcesControl.dll</HintPath>
//<HintPath>a:\references\Autodesk.REX.Framework.dll</HintPath>
//<HintPath>C:\Program Files\Common Files\Autodesk Shared\Extensions 2012\Framework\Interop\Interop.ROHTMLLib.dll</HintPath>
//<HintPath>..\..\..\..\..\..\..\..\Releasex64\RevitAPI.dll</HintPath>
//<HintPath>..\..\..\..\..\..\..\..\Releasex64\RevitAPIUI.dll</HintPath>
//<HintPath>C:\Program Files\Common Files\Autodesk Shared\Extensions 2012\Framework\Foundation\REX.API.dll</HintPath>
//<HintPath>C:\Program Files\Common Files\Autodesk Shared\Extensions 2012\Framework\Components\AREXContentGenerator\REX.ContentGeneratorLT.dll</HintPath>
//<HintPath>C:\Program Files\Common Files\Autodesk Shared\Extensions 2012\Framework\Engine\REX.Controls.dll</HintPath>
//<HintPath>C:\Program Files\Common Files\Autodesk Shared\Extensions 2012\Framework\Engine\REX.Controls.WPF.dll</HintPath>
//<HintPath>C:\Program Files\Common Files\Autodesk Shared\Extensions 2012\Framework\Foundation\REX.Foundation.dll</HintPath>
//<HintPath>C:\Program Files\Common Files\Autodesk Shared\Extensions 2012\Framework\Foundation\REX.Foundation.WPF.dll</HintPath>
//<HintPath>C:\Program Files\Common Files\Autodesk Shared\Extensions 2012\Framework\Foundation\REX.Geometry.dll</HintPath>

// "\Program Files\Common Files\Autodesk Shared\Extensions 2012\Framework\Engine\AssemblyCache\Autodesk.REX.Framework.dll"
// "\Program Files\Common Files\Autodesk Shared\Extensions 2013\Framework\Engine\AssemblyCache\Autodesk.REX.Framework.dll"
