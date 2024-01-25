using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AR_Finishings
{
    public class RoomBoundaryFloorGenerator
    {
        private Document _doc;

        public RoomBoundaryFloorGenerator(Document doc)
        {
            _doc = doc;
        }

        public void CreateFloors(IEnumerable<ElementId> selectedRoomIds, FloorType selectedFloorType)
        {
            StringBuilder message = new StringBuilder("Generated Floors for Room IDs:\n");
            using (Transaction trans = new Transaction(_doc, "Generate Floors"))
            {
                trans.Start();
                foreach (ElementId roomId in selectedRoomIds)
                {
                    Room room = _doc.GetElement(roomId) as Room;
                    if (room != null)
                    {
                        Level level = _doc.GetElement(room.LevelId) as Level;
                        if (level != null)
                        {
                            double roomLowerOffset = room.get_Parameter(BuiltInParameter.ROOM_LOWER_OFFSET).AsDouble();
                            SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions();
                            IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(options);

                            if (boundaries.Count > 0)
                            {
                                foreach (IList<BoundarySegment> boundary in boundaries)
                                {
                                    CurveLoop curveLoop = CurveLoop.Create(boundary.Select(seg => seg.GetCurve()).ToList());
                                    IList<CurveLoop> loops = new List<CurveLoop> { curveLoop };

                                    if (loops.Count > 0)
                                    {
                                        Floor floor = Floor.Create(_doc, loops, selectedFloorType.Id, level.Id);
                                        floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).Set(roomLowerOffset);
                                        message.AppendLine($"Room ID: {roomId.Value}, Floor ID: {floor.Id.Value}");
                                    }
                                }
                            }
                        }
                    }
                }
                trans.Commit();
            }
            if (selectedFloorType != null)
            {
                TaskDialog.Show("Room Selection", message.ToString());
            }
                
        }

    }
}
