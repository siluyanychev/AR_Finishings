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
                List<Wall> createdWalls = new List<Wall>();

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

                            // Получаем элемент, соответствующий сегменту границы помещения
                            Element boundaryElement = _doc.GetElement(segment.ElementId);

                            // Проверяем, не является ли элемент стеной с CurtainGrid
                            Wall boundaryWall = boundaryElement as Wall;
                            if (boundaryWall != null && boundaryWall.CurtainGrid != null)
                            {
                                // Пропускаем создание стены, если это витраж
                                continue;
                            }

                            Curve curve = segment.GetCurve();
                            Wall createdWall = Wall.Create(_doc, curve, selectedWallType.Id, level.Id, (_wallHeight / 304.8 - roomLowerOffset), 0, false, false);
                            createdWall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).Set(roomLowerOffset);
                            message.AppendLine($"Room ID: {roomId.Value}, Wall ID: {createdWall.Id.Value}");
                            createdWalls.Add( createdWall );



                            // Join walls 
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


                            createdWalls.Add(createdWall);
                        }
                    }
                }
                trans.Commit();
                // Вызовите TrimExtendWalls после того как транзакция будет закрыта
                TrimExtendWalls(_doc, createdWalls );
            }


            
        }
        private void TrimExtendWalls(Document doc, List<Wall> walls)
        {
            using (Transaction trans = new Transaction(doc, "Trim/Extend Walls"))
            {
                trans.Start();

                for (int i = 0; i < walls.Count - 1; i++)
                {
                    for (int j = i + 1; j < walls.Count; j++)
                    {
                        Wall wall1 = walls[i];
                        Wall wall2 = walls[j];

                        LocationCurve locCurve1 = wall1.Location as LocationCurve;
                        LocationCurve locCurve2 = wall2.Location as LocationCurve;

                        if (locCurve1 != null && locCurve2 != null)
                        {
                            Curve curve1 = locCurve1.Curve;
                            Curve curve2 = locCurve2.Curve;

                            IntersectionResultArray results;
                            SetComparisonResult comparisonResult = curve1.Intersect(curve2, out results);

                            if (comparisonResult == SetComparisonResult.Overlap)
                            {
                                // There might be more than one intersection result
                                foreach (IntersectionResult ir in results)
                                {
                                    XYZ intersectionPoint = ir.XYZPoint;

                                    // Extend wall1 to intersection
                                    if (!curve1.GetEndPoint(0).IsAlmostEqualTo(intersectionPoint))
                                    {
                                        locCurve1.Curve = Line.CreateBound(curve1.GetEndPoint(0), intersectionPoint);
                                    }

                                    // Extend wall2 to intersection
                                    if (!curve2.GetEndPoint(0).IsAlmostEqualTo(intersectionPoint))
                                    {
                                        locCurve2.Curve = Line.CreateBound(curve2.GetEndPoint(0), intersectionPoint);
                                    }
                                }
                            }
                        }
                    }
                }

                trans.Commit();
            }
        }



    }
}