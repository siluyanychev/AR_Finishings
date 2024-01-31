using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;

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
                    double roomLowerOffset = room.get_Parameter(BuiltInParameter.ROOM_LOWER_OFFSET).AsDouble();

                    var boundaries = room.GetBoundarySegments(new SpatialElementBoundaryOptions());
                    foreach (var boundary in boundaries)
                    {
                        foreach (var segment in boundary)
                        {
                            Curve curve = segment.GetCurve();
                            Wall createdWall = Wall.Create(_doc, curve, selectedWallType.Id, level.Id, (_wallHeight / 304.8 - roomLowerOffset), 0, false, false);
                            createdWall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).Set(roomLowerOffset);
                            message.AppendLine($"Room ID: {roomId.Value}, Wall ID: {createdWall.Id.Value}");

                            // Join walls 
                            Element boundaryElement = _doc.GetElement(segment.ElementId);
                            if (boundaryElement != null && createdWall != null)
                            {
                                JoinGeometryUtils.JoinGeometry(_doc, createdWall, boundaryElement);
                            }

                            // Установка привязки стены к внутренней стороне
                            Parameter wallKeyRefParam = createdWall.get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM);
                            if (wallKeyRefParam != null && wallKeyRefParam.StorageType == StorageType.Integer)
                            {
                                wallKeyRefParam.Set(3); // 3 соответствует внутренней стороне стены
                            }

                            // Assuming 'createdWall' is a Wall object that has just been created
                            LocationCurve wallLocationCurve = createdWall.Location as LocationCurve;
                            Curve wallCurve = wallLocationCurve.Curve;

                            // Determine the wall's orientation and apply offset accordingly
                            XYZ point0 = wallCurve.GetEndPoint(0);
                            XYZ point1 = wallCurve.GetEndPoint(1);
                            XYZ wallDirection = (point1 - point0).Normalize();
                            XYZ normal = XYZ.BasisZ.CrossProduct(wallDirection); // Assuming walls are vertical, so Z cross product gives the normal

                            double wallWidth = selectedWallType.Width;
                            XYZ offset = normal * (+wallWidth / 2); // Offset towards the interior side of the wall

                            // Create a new curve for the wall at the offset location
                            Curve offsetCurve = wallCurve.CreateTransformed(Autodesk.Revit.DB.Transform.CreateTranslation(offset));

                            // Move the wall to the new offset location
                            ElementTransformUtils.MoveElement(_doc, createdWall.Id, offset);

                        }
                    }
                }
                trans.Commit();
            }
        }
    }
}