using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Xaml;

namespace AR_Finishings
{
    public class RoomBoundarySkirtGenerator
    {
        private Document _doc;
        private double _skirtHeight;

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

                    // Вызываем метод FindDoors для поиска дверей внутри комнаты
                    ICollection<ElementId> doorsInRoomIds = FindDoors(room);

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

                            // Используем doorsInRoomIds для обработки дверей внутри комнаты
                            CutSkirtsAtDoors(_doc, createdWall, doorsInRoomIds);

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
        private ICollection<ElementId> FindDoors(Room room)
        {
            // Создайте пустую коллекцию для хранения идентификаторов дверей
            List<ElementId> doorIdsInRoom = new List<ElementId>();

            // Получите объекты всех дверей в проекте
            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            collector.OfCategory(BuiltInCategory.OST_Doors);

            foreach (Element doorElement in collector)
            {
                FamilyInstance door = doorElement as FamilyInstance;
                if (door != null)
                {
                    // Получите Location для двери
                    Location doorLocation = door.Location;

                    if (doorLocation != null && doorLocation is LocationPoint locationPoint)
                    {
                        XYZ doorPoint = locationPoint.Point;

                        // Проверьте, принадлежит ли точка двери комнате
                        if (room.IsPointInRoom(doorPoint))
                        {
                            doorIdsInRoom.Add(door.Id);
                        }
                    }
                }
            }

            return doorIdsInRoom;
        }


        private void CutSkirtsAtDoors(Document doc, Wall skirtWall, ICollection<ElementId> doorIds)
        {
            LocationCurve skirtLocationCurve = skirtWall.Location as LocationCurve;
            if (skirtLocationCurve == null) return;

            Curve skirtCurve = skirtLocationCurve.Curve;
            Line skirtLine = skirtCurve as Line;
            if (skirtLine == null) return;

            foreach (ElementId doorId in doorIds)
            {
                Element door = doc.GetElement(doorId);
                LocationPoint doorLocation = door.Location as LocationPoint;
                if (doorLocation != null)
                {
                    XYZ doorPoint = doorLocation.Point;
                    BoundingBoxXYZ doorBox = door.get_BoundingBox(null);

                    // Найти точки пересечения и создать новые сегменты стены, если они в пределах плинтуса
                    XYZ intersect1, intersect2;
                    if (LineIntersectsBox(skirtLine, doorBox, out intersect1, out intersect2))
                    {
                        using (Transaction trans = new Transaction(doc, "Cut Skirts"))
                        {
                            trans.Start();

                            // Создаем новые сегменты плинтуса
                            if (intersect1.DistanceTo(intersect2) > 0) // Убедитесь, что точки пересечения действительны
                            {
                                Curve segmentBefore = Line.CreateBound(skirtLine.GetEndPoint(0), intersect1);
                                Curve segmentAfter = Line.CreateBound(intersect2, skirtLine.GetEndPoint(1));

                                // Создаем новые стены плинтуса
                                Wall.Create(doc, segmentBefore, skirtWall.WallType.Id, skirtWall.LevelId, _skirtHeight, 0, false, false);
                                Wall.Create(doc, segmentAfter, skirtWall.WallType.Id, skirtWall.LevelId, _skirtHeight, 0, false, false);
                            }

                            // Удаляем исходный плинтус, который пересекается с дверью
                            doc.Delete(skirtWall.Id);

                            trans.Commit();
                        }
                    }
                }
            }
        }


        // Метод для проверки пересечения линии и bounding box'а двери
        private bool LineIntersectsBox(Line line, BoundingBoxXYZ box, out XYZ point1, out XYZ point2)
        {
            // Реализуйте логику для определения пересечения линии с bounding box'ом
            // Установите point1 и point2 в точки пересечения
            // Верните true, если линия пересекает bounding box, иначе false

            // Это просто псевдокод для демонстрации
            point1 = null;
            point2 = null;
            return false;
        }


    }
}