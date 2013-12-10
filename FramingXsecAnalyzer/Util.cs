#region Namespaces
using System;
using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
#endregion

namespace FramingXsecAnalyzer
{
  class Util
  {
    const double _eps = 1.0e-9;

    static public double Eps
    {
      get
      {
        return _eps;
      }
    }

    static public bool IsZero( double a, double tolerance )
    {
      return tolerance > Math.Abs( a );
    }

    static public bool IsZero( double a )
    {
      return IsZero( a, _eps );
    }

    static public bool IsEqual( double a, double b )
    {
      return IsZero( b - a );
    }

    static public bool IsParallel( XYZ p, XYZ q )
    {
      return p.CrossProduct( q ).IsZeroLength();
    }

    public const string Caption = "FramingXsecAnalyzer";

    /// <summary>
    /// Return an English plural suffix for the given 
    /// number of items, i.e. 's' for zero or more 
    /// than one, and nothing for exactly one.
    /// </summary>
    static public string PluralSuffix( int n )
    {
      return 1 == n ? "" : "s";
    }

    /// <summary>
    /// Return a full stop dot for zero items,
    /// and a colon for more than zero.
    /// </summary>
    static public string DotOrColon( int n )
    {
      return 0 == n ? "." : ":";
    }

    /// <summary>
    /// Display an informational message 
    /// in a Revit task dialogue.
    /// </summary>
    static public void InfoMessage( string instruction )
    {
      Debug.Print( instruction );

      TaskDialog a = new TaskDialog( Caption );

      a.MainInstruction = instruction;
      a.Show();
    }

    /// <summary>
    /// Display an informational message 
    /// in a Revit task dialogue.
    /// </summary>
    static public void InfoMessage(
      string instruction,
      string content )
    {
      Debug.Print( instruction );
      Debug.Print( content );

      TaskDialog a = new TaskDialog( Caption );

      a.MainInstruction = instruction;
      a.MainContent = content;
      a.Show();
    }

    /// <summary>
    /// Return active document or 
    /// warn user if there is none.
    /// Optionally, require the document to be 
    /// modifiable.
    /// </summary>
    static public Document GetActiveDocument(
      UIDocument uidoc,
      bool requireModifiable )
    {
      Document doc = uidoc.Document;

      if( null == doc )
      {
        InfoMessage( "Please run this command "
          + "in an activate Revit document." );
      }
      else if( requireModifiable && doc.IsReadOnly )
      {
        InfoMessage( "The active document is "
          + "read-only. This command requires a "
          + "modifiable Revit document." );

        doc = null;
      }
      return doc;
    }

    /// <summary>
    /// Return a string describing the given element:
    /// .NET type name,
    /// category name,
    /// family and symbol name for a family instance,
    /// element id and element name.
    /// </summary>
    static public string ElementDescription( Element e )
    {
      if( null == e )
      {
        return "<null>";
      }

      // For a wall, the element name equals the
      // wall type name, which is equivalent to the
      // family name ...

      FamilyInstance fi = e as FamilyInstance;

      string typeName = e.GetType().Name;

      string categoryName = ( null == e.Category )
        ? string.Empty
        : e.Category.Name + " ";

      string familyName = ( null == fi )
        ? string.Empty
        : fi.Symbol.Family.Name + " ";

      string symbolName = ( null == fi
        || e.Name.Equals( fi.Symbol.Name ) )
          ? string.Empty
          : fi.Symbol.Name + " ";

      return string.Format( "{0} {1}{2}{3}<{4} {5}>",
        typeName, categoryName, familyName, symbolName,
        e.Id.IntegerValue, e.Name );
    }
  }

  static public class JtPlaneExtensionMethods
  {
    /// <summary>
    /// Return signed distance from plane to a given point.
    /// </summary>
    static public double SignedDistanceTo(
      this Plane plane,
      XYZ p )
    {
      Debug.Assert(
        Util.IsEqual( plane.Normal.GetLength(), 1 ),
        "expected normalised plane normal" );

      XYZ v = p - plane.Origin;

      return plane.Normal.DotProduct( v );
    }
  }
}
