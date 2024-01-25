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
        private double _ceilingHeight;

        public RoomBoundaryCeilingGenerator(Document doc, double ceilingHeight)
        {
            _doc = doc;
            _ceilingHeight = ceilingHeight; // Сохраняем значение высоты потолка из MainWindow
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
                        Level level = _doc.GetElement(room.LevelId) as Level;
                        if (level != null)
                        {
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
                                        Ceiling ceiling = Ceiling.Create(_doc, loops, selectedCeilingType.Id, level.Id);
                                        ceiling.get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM).Set(UnitUtils.ConvertToInternalUnits(_ceilingHeight, UnitTypeId.Millimeters));
                                        message.AppendLine($"Room ID: {roomId.Value}, Ceiling ID: {ceiling.Id.Value}");
                                    }
                                }
                            }
                        }
                    }
                }
                trans.Commit();
            }
            if (selectedCeilingType != null)
            {
                TaskDialog.Show("Room Selection", message.ToString());
            }
        }
    }
}
