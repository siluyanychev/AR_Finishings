using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xaml;

namespace AR_Finishings
{
    public class RoomBoundarySkirtGenerator
    {
        private Document _doc;
       //Shared Parameters

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
            List<Wall> createdWalls = new List<Wall>(); // Список для хранения созданных стен

            using (Transaction trans = new Transaction(_doc, "Generate Skirt"))
            {
                trans.Start();

                foreach (ElementId roomId in selectedRoomIds)
                {
                    Room room = _doc.GetElement(roomId) as Room;
                    string roomNameValue = room.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();
                    Level level = _doc.GetElement(room.LevelId) as Level;
                    double roomLowerOffset = room.get_Parameter(BuiltInParameter.ROOM_LOWER_OFFSET).AsDouble();

                    var boundaries = room.GetBoundarySegments(new SpatialElementBoundaryOptions());
                    foreach (var boundary in boundaries)
                    {
                        foreach (var segment in boundary)
                        {
                            ElementId boundaryElementId = segment.ElementId;
                            Wall boundaryWall = _doc.GetElement(boundaryElementId) as Wall;

                            if (boundaryWall != null && (boundaryWall.CurtainGrid != null || !boundaryWall.WallType.Name.StartsWith("АР_О")))
                            {
                                continue; // Пропускаем, если не соответствует условиям
                            }

                            Curve curve = segment.GetCurve();
                            Curve outerCurve = curve.CreateOffset(selectedWallType.Width / -2.0, XYZ.BasisZ);
                            Wall createdWall = Wall.Create(_doc, outerCurve, selectedWallType.Id, level.Id, _skirtHeight / 304.8 - roomLowerOffset, 0, false, false);
                            createdWalls.Add(createdWall); // Добавляем стену в список

                            // Настройка параметров стены
                            SetupWallParameters(createdWall, roomLowerOffset, roomNameValue);

                            message.AppendLine($"Room ID: {roomId.Value}, Wall ID: {createdWall.Id.Value}");
                        }
                    }
                }

                trans.Commit();
            }

            // Выводим сообщение с результатами
            TaskDialog.Show("Walls Creation Report", message.ToString());
        }

        private void SetupWallParameters(Wall wall, double roomLowerOffset, string roomNameValue)
        {
            wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).Set(roomLowerOffset);

            Parameter wallKeyRefParam = wall.get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM);
            if (wallKeyRefParam != null && wallKeyRefParam.StorageType == StorageType.Integer)
            {
                wallKeyRefParam.Set(3); // Установка внутренней стороны стены
            }

            Parameter wallBoundaries = wall.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING);
            if (wallBoundaries != null && wallBoundaries.StorageType == StorageType.Integer)
            {
                wallBoundaries.Set(0); // Отмена границы помещения
            }

            // Пример установки значения общего параметра (предполагая, что параметр уже добавлен в проект)
            Guid roomNameGuid = new Guid("4a5cec5d-f883-42c3-a05c-89ec822d637b"); // GUID общего параметра
            Parameter roomNameParam = wall.get_Parameter(roomNameGuid);
            if (roomNameParam != null && roomNameParam.StorageType == StorageType.String)
            {
                roomNameParam.Set(roomNameValue); // Установка значения параметра
            }
        }

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