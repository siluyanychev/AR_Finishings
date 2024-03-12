using Autodesk.Revit.DB;
using System.Collections.Generic;

public class XYZComparer : IComparer<XYZ>
{
    private Curve _curve;

    public XYZComparer(Curve curve)
    {
        _curve = curve;
    }

    public int Compare(XYZ p1, XYZ p2)
    {
        double param1 = _curve.Project(p1).Parameter;
        double param2 = _curve.Project(p2).Parameter;

        if (param1 < param2)
        {
            return -1;
        }
        else if (param1 > param2)
        {
            return 1;
        }
        else
        {
            return 0;
        }
    }
}
