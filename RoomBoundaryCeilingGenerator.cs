using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AR_Finishings
{
    public class RoomBoundaryCeilingGenerator
    {
        private Document _doc;

        public RoomBoundaryCeilingGenerator(Document doc)
        {
            _doc = doc;
        }

    public void CreateCeilings(IEnumerable<ElementId> selectedRoomIds, CeilingType selectedCeilingType)
    {
        StringBuilder message = new StringBuilder("Generated Ceilings for Room IDs:\n");
        using (Transaction trans = new Transaction(_doc, "Generate Ceilings"))
        { 
            trans.Start();
            foreach (ElementId roomId in selectedRoomIds)
            {
                Room room = _doc.GetElement(roomId) as Room;
                if (room != null)
                {


                }
            }
            
    }