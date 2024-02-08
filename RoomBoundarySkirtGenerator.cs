using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xaml;

namespace AR_Finishings
{
    public class RoomBoundarySkirtGenerator
    {
        private Document _doc;
        private double _skirtHeight;
        private List<Wall> createdWalls = new List<Wall>();

        public RoomBoundarySkirtGenerator(Document doc, double skirtHeight)
        {
            _doc = doc;
            _skirtHeight = skirtHeight;
        }
        public void CreateWalls(IEnumerable<ElementId> selectedRoomIds, WallType selectedWallType)
        {
            StringBuilder message = new StringBuilder("Generated Skirts for Room IDs:\n");
            using (Transaction trans = new Transaction(_doc, "Generate Skirt"))
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
                            Element boundaryElement = _doc.GetElement(segment.ElementId);

                            // Проверяем, является ли элемент стеной с CurtainGrid или имя типа начинается с "АР_О"
                            Wall boundaryWall = boundaryElement as Wall;

                            if (boundaryWall != null)
                            {
                                WallType wallType = _doc.GetElement(boundaryWall.GetTypeId()) as WallType;
                                if (boundaryWall.CurtainGrid != null || (wallType != null && !wallType.Name.StartsWith("АР_О")))
                                {
                                    // Пропускаем создание стены, если это витраж или тип стены начинается с "АР_О"
                                    continue;
                                }
                            }

                            // Проверяем, является ли элемент разделителем помещений
                            if (boundaryElement.Category != null &&
                                boundaryElement.Category.Id.Value == (int)BuiltInCategory.OST_RoomSeparationLines)
                            {
                                continue;
                            }
                            Curve curve = segment.GetCurve();
                            // Для кривых, которые представляют внешние края стен
                            Curve outerCurve = curve.CreateOffset(selectedWallType.Width / -2.0, XYZ.BasisZ);
                            // Для кривых, которые представляют внутренние края стен
                            Curve innerCurve = curve.CreateOffset(selectedWallType.Width / 2.0, XYZ.BasisZ);

                            Wall createdWall = Wall.Create(_doc, outerCurve, selectedWallType.Id, level.Id, (_skirtHeight / 304.8 - roomLowerOffset), 0, false, false);
                            createdWall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).Set(roomLowerOffset);
                            message.AppendLine($"Room ID: {roomId.Value}, Wall ID: {createdWall.Id.Value}");
                            createdWalls.Add(createdWall);


                            // Установка привязки стены к внутренней стороне
                            Parameter wallKeyRefParam = createdWall.get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM);
                            if (wallKeyRefParam != null && wallKeyRefParam.StorageType == StorageType.Integer)
                            {
                                wallKeyRefParam.Set(3); // 3 соответствует внутренней стороне стены
                            }

                            // Join walls 
                            if (boundaryElement != null &&
                                boundaryElement.Category.Id.Value == (int)BuiltInCategory.OST_Walls &&
                                createdWall != null)
                            {
                                JoinGeometryUtils.JoinGeometry(_doc, createdWall, boundaryElement);
                            }
                        }
                    }
                }
                trans.Commit();
            }
        }
        // Добавляем новый метод в класс RoomBoundarySkirtGenerator
        public void CutSkirtsAtDoors(IEnumerable<ElementId> roomIds)
        {
            using (Transaction trans = new Transaction(_doc, "Cut Skirts at Doors"))
            {
                trans.Start();

                // Получаем последнюю фазу проекта
                Phase phase = _doc.Phases.Cast<Phase>().FirstOrDefault(p => p.Name == "Новая конструкция");

                if (phase == null)
                {
                    TaskDialog.Show("Error", "Фаза не найдена!");
                    trans.RollBack();
                    return;
                }

                foreach (ElementId roomId in roomIds)
                {
                    Room room = _doc.GetElement(roomId) as Room;

                    // Создаем коллектор для поиска всех дверей в комнате в контексте последней фазы
                    var doorsInRoom = new FilteredElementCollector(_doc)
                        .OfCategory(BuiltInCategory.OST_Doors)
                        .WhereElementIsNotElementType()
                        .Cast<FamilyInstance>()
                        .Where(door => door.get_FromRoom(phase)?.Id == roomId || door.get_ToRoom(phase)?.Id == roomId).ToList();

                    foreach (FamilyInstance door in doorsInRoom)
                    {
                        LocationPoint doorLocation = door.Location as LocationPoint;
                        if (doorLocation == null) continue;

                        XYZ doorPosition = doorLocation.Point;
                        XYZ doorDirection = door.HandOrientation; // Используем HandOrientation как направление двери
                        double doorWidth = (door.get_BoundingBox(null).Max.X - door.get_BoundingBox(null).Min.X) * _doc.ActiveView.Scale;

                        foreach (Wall roomWall in createdWalls)
                        {
                            LocationCurve wallLocationCurve = roomWall.Location as LocationCurve;
                            if (wallLocationCurve == null) continue;

                            Curve wallCurve = wallLocationCurve.Curve;
                            Line extendedLine1 = ExtendLine(wallCurve, 200 / 304.8); // 200 мм в футах
                            Line extendedLine2 = ExtendLine(wallCurve.CreateReversed(), 200 / 304.8);

                            // Проверяем пересечение расширенной линии с кривой стены
                            IntersectionResultArray results;
                            if (wallCurve.Intersect(extendedLine1, out results) == SetComparisonResult.Overlap)
                            {
                                ProcessIntersection(results, roomWall, doorPosition, doorDirection, doorWidth);
                            }
                            if (wallCurve.Intersect(extendedLine2, out results) == SetComparisonResult.Overlap)
                            {
                                ProcessIntersection(results, roomWall, doorPosition, doorDirection, doorWidth);
                            }
                        }
                    }
                }

                trans.Commit();
            }
        }

        private Line ExtendLine(Curve curve, double length)
        {
            XYZ start = curve.GetEndPoint(0);
            XYZ end = curve.GetEndPoint(1);

            XYZ direction = (end - start).Normalize();
            XYZ extendedStart = start - direction * length;
            XYZ extendedEnd = end + direction * length;

            return Line.CreateBound(extendedStart, extendedEnd);
        }

        private void ProcessIntersection(IntersectionResultArray results, Wall roomWall, XYZ doorPosition, XYZ doorDirection, double doorWidth)
        {
            // Check if we have intersection points
            if (results == null || results.Size == 0)
                return;

            // This boolean tracks if we created new walls to decide on deleting the original wall
            bool createdNewWall = false;

            // Assume the first intersection point is the start of the door and the second is the end
            XYZ intersectionStart = results.get_Item(0).XYZPoint;
            XYZ intersectionEnd = results.Size > 1 ? results.get_Item(1).XYZPoint : null;

            // Check if the first intersection point is actually within the door's width
            if (intersectionStart.DistanceTo(doorPosition) <= doorWidth / 2.0)
            {
                // If the first intersection is within the door, then this is not an intersection we cut at
                intersectionStart = null;
            }

            // Check if the second intersection point is actually within the door's width
            if (intersectionEnd != null && intersectionEnd.DistanceTo(doorPosition) <= doorWidth / 2.0)
            {
                // If the second intersection is within the door, then this is not an intersection we cut at
                intersectionEnd = null;
            }

            // If we have valid intersection points, create new walls
            if (intersectionStart != null || intersectionEnd != null)
            {
                // Retrieve the original wall's start and end points
                Curve wallCurve = (roomWall.Location as LocationCurve).Curve;
                XYZ wallStart = wallCurve.GetEndPoint(0);
                XYZ wallEnd = wallCurve.GetEndPoint(1);

                // Create the new wall segments
                if (intersectionStart != null)
                {
                    Curve newWallCurve = Line.CreateBound(wallStart, intersectionStart);
                    if (newWallCurve.Length > 0.1) // Ensure the new wall has a minimum length
                    {
                        Wall.Create(_doc, newWallCurve, roomWall.WallType.Id, roomWall.LevelId, roomWall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble(), 0, false, false);
                        createdNewWall = true;
                    }
                }

                if (intersectionEnd != null)
                {
                    Curve newWallCurve = Line.CreateBound(intersectionEnd, wallEnd);
                    if (newWallCurve.Length > 0.1) // Ensure the new wall has a minimum length
                    {
                        Wall.Create(_doc, newWallCurve, roomWall.WallType.Id, roomWall.LevelId, roomWall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble(), 0, false, false);
                        createdNewWall = true;
                    }
                }

                // If new walls were created, delete the original wall
                if (createdNewWall)
                {
                    _doc.Delete(roomWall.Id);
                }
            }
        }
    }
}