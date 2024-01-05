using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AR_Finishings
{
    public class ElementTypeSelector
    {
        private string pref0 = "АР_";
        private string pref1 = "О";
        public IEnumerable<FloorType> GetFloorTypes(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>()
                .Where(f=> f.Name.StartsWith(pref0));
        }
        public IEnumerable <CeilingType> GetCeilingTypes(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(CeilingType))
                .Cast<CeilingType>()
                .Where(c=> c.Name.StartsWith(pref0));
        }
        public IEnumerable <WallType> GetWallTypes(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .Where(w=> w.Name.StartsWith(pref0 + pref1));
        }

    }

}
