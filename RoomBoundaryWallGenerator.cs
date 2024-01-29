using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AR_Finishings
{
    public class RoomBoundaryWallGenerator
    {
        private Document _doc;
        private double _wallHeight;

        public RoomBoundaryWallGenerator(Document doc, double wallHeight)
        {
            _doc = doc;
            _wallHeight = wallHeight;
        }

        public void CreateWalls(IEnumerable<ElementId> selectedRoomIds, WallType selectedWallType)
        {
            StringBuilder message = new StringBuilder("Generated Walls for Room IDs:\n");
            using (Transaction trans = new Transaction(_doc, "Generate Walls"))
            {
                trans.Start();
                foreach (ElementId roomId in selectedRoomIds)
                {
                    Room room = _doc.GetElement(roomId) as Room;
                    Level level = _doc.GetElement(room.LevelId) as Level;

                    var boundaries = room.GetBoundarySegments(new SpatialElementBoundaryOptions());
                    foreach (var boundary in boundaries)
                    {
                        foreach (var segment in boundary)
                        {
                            Curve curve = segment.GetCurve();
                            Wall createdWall = Wall.Create(_doc, curve, selectedWallType.Id, level.Id, _wallHeight, 0, false, false);

                            // Установка привязки стены к внутренней стороне
                            Parameter wallKeyRefParam = createdWall.get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM);
                            if (wallKeyRefParam != null && wallKeyRefParam.StorageType == StorageType.Integer)
                            {
                                wallKeyRefParam.Set(3); // 3 соответствует внутренней стороне стены
                            }

                            // Смещение стены внутрь помещения
                            double wallWidth = selectedWallType.Width;
                            XYZ offset = new XYZ(-wallWidth / 2, +wallWidth / 2, 0);
                            Line wallLine = (createdWall.Location as LocationCurve).Curve as Line;
                            XYZ newLineStart = wallLine.GetEndPoint(0).Add(offset);
                            XYZ newLineEnd = wallLine.GetEndPoint(1).Add(offset);
                            Line offsetLine = Line.CreateBound(newLineStart, newLineEnd);
                            ElementTransformUtils.MoveElement(_doc, createdWall.Id, offsetLine.GetEndPoint(0) - wallLine.GetEndPoint(0));
                        }
                    }
                }
                trans.Commit();
            }
            TaskDialog.Show("Room Selection", message.ToString());
        }
    }
}
