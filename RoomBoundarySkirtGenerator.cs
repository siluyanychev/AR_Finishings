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
        private double _skirtHeight;
        private List<Wall> createdWalls;
        private Dictionary<ElementId, List<ElementId>> wallDoorIntersections = new Dictionary<ElementId, List<ElementId>>();


        public RoomBoundarySkirtGenerator(Document doc, double skirtHeight)
        {
            _doc = doc;
            _skirtHeight = skirtHeight;
            createdWalls = new List<Wall>();
            wallDoorIntersections = new Dictionary<ElementId, List<ElementId>>(); // Инициализация словаря

        }
        public void CreateWalls(IEnumerable<ElementId> selectedRoomIds, WallType selectedWallType)
        {
            StringBuilder message = new StringBuilder("Generated Skirts for Room IDs:\n");

            using (Transaction trans = new Transaction(_doc, "Generate Skirt"))
            {
                trans.Start();

                foreach (ElementId roomId in selectedRoomIds)
                {
                    Room room = _doc.GetElement(roomId) as Room;
                    string roomNameValue = room.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();
                    string roomNumberValue = room.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString();
                    Level level = _doc.GetElement(room.LevelId) as Level;
                    string levelRoomStringValue = room.get_Parameter(BuiltInParameter.LEVEL_NAME).AsString().Split('_')[1];
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
                            Wall createdWall = Wall.Create(_doc, outerCurve, selectedWallType.Id, level.Id, _skirtHeight / 304.8, 0, false, false);
                            createdWalls.Add(createdWall); // Добавляем стену в список
                            // Join walls 
                            if (boundaryWall != null &&
                                boundaryWall.Category.Id.Value == (int)BuiltInCategory.OST_Walls &&
                                createdWall != null)
                            {
                                JoinGeometryUtils.JoinGeometry(_doc, createdWall, boundaryWall);
                            }
                            // Настройка параметров стены
                            SetupWallParameters(createdWall, roomLowerOffset, roomNameValue, roomNumberValue, levelRoomStringValue);

                        }
                    }
                }

                trans.Commit();
            }

        }
        public void CheckWallsAndDoorsIntersection()
        {
            using (Transaction trans = new Transaction(_doc, "Check Walls and Doors Intersection"))
            {
                trans.Start();
                wallDoorIntersections = new Dictionary<ElementId, List<ElementId>>(); // Инициализация здесь
                foreach (Wall wall in createdWalls)
                {
                    // Получаем BoundingBox стены
                    BoundingBoxXYZ wallBox = wall.get_BoundingBox(null);
                    // Проверяем, что BoundingBox существует и он не пустой
                    if (wallBox == null || wallBox.Min.IsAlmostEqualTo(wallBox.Max))
                    {
                        // Пропускаем стену, если BoundingBox пустой
                        continue;
                    }

                    Outline wallOutline = new Outline(wallBox.Min - new XYZ(500 / 304.8, 500 / 304.8, 0),
                                                      wallBox.Max + new XYZ(500 / 304.8, 500 / 304.8, 0));
                    BoundingBoxIntersectsFilter filter = new BoundingBoxIntersectsFilter(wallOutline);
                    FilteredElementCollector collector = new FilteredElementCollector(_doc).OfCategory(BuiltInCategory.OST_Doors).WherePasses(filter);

                    List<ElementId> intersectingDoors = new List<ElementId>();
                    foreach (Element door in collector)
                    {
                        intersectingDoors.Add(door.Id);
                    }

                    // If there are intersecting doors, add them to the dictionary
                    if (intersectingDoors.Count > 0)
                    {
                        wallDoorIntersections.Add(wall.Id, intersectingDoors);
                    }
                }

                // Display the intersections
                StringBuilder sb = new StringBuilder();
                foreach (var kvp in wallDoorIntersections)
                {
                    foreach (var doorId in kvp.Value)
                    {
                        sb.AppendLine($"Wall ID: {kvp.Key.Value} : Door ID: {doorId.Value}");
                    }
                }

                if (sb.Length > 0)
                {
                    TaskDialog.Show("Intersections", sb.ToString());
                }
                else
                {
                    TaskDialog.Show("Intersections", "No intersections found.");
                }

                trans.Commit();
            }
        }

        public void DivideWallsAtDoors()
        {
            using (Transaction trans = new Transaction(_doc, "Divide Walls At Doors"))
            {
                if (trans.Start() == TransactionStatus.Started)
                {
                    foreach (var kvp in wallDoorIntersections)
                    {
                        Wall wall = _doc.GetElement(kvp.Key) as Wall;
                        if (wall == null) continue;

                        LocationCurve wallLocCurve = wall.Location as LocationCurve;
                        if (wallLocCurve == null) continue;

                        Curve wallCurve = wallLocCurve.Curve;
                        if (wallCurve == null) continue;

                        foreach (ElementId doorId in kvp.Value)
                        {
                            FamilyInstance door = _doc.GetElement(doorId) as FamilyInstance;
                            if (door != null)
                            {
                                LocationPoint doorLocation = door.Location as LocationPoint;
                                XYZ doorPosition = doorLocation.Point;
                                double doorWidth = door.Symbol.get_Parameter(BuiltInParameter.DOOR_WIDTH).AsDouble();

                                // Находим направление стены и двери
                                XYZ wallDirection = wallCurve.GetEndPoint(1) - wallCurve.GetEndPoint(0);
                                XYZ doorDirection = door.HandOrientation;

                                // Находим вектор, параллельный направлению стены и указывающий в сторону от границы помещения
                                XYZ offsetVector = new XYZ(-wallDirection.Y, wallDirection.X, 0);

                                // Нормализуем вектор, чтобы он имел длину 1
                                offsetVector = offsetVector.Normalize();

                                // Находим точки на стенах, параллельные направлению стены
                                XYZ doorLeftPoint = doorPosition - doorDirection * doorWidth / 2.0 - offsetVector;
                                XYZ doorRightPoint = doorPosition + doorDirection * doorWidth / 2.0 + offsetVector;

                                // Найдем ближайшие точки на линии стены
                                double paramLeft = wallCurve.Project(doorLeftPoint).Parameter;
                                double paramRight = wallCurve.Project(doorRightPoint).Parameter;
                                XYZ pointLeft = wallCurve.Evaluate(paramLeft, false);
                                XYZ pointRight = wallCurve.Evaluate(paramRight, false);

                                // Создаем две стены с обеих сторон двери
                                if (pointLeft.DistanceTo(pointRight) > _doc.Application.ShortCurveTolerance)
                                {
                                    // Создаем стену слева от двери
                                    Curve leftSegmentCurve = Line.CreateBound(wallCurve.GetEndPoint(0), pointLeft);
                                    Wall leftWall = Wall.Create(_doc, leftSegmentCurve, wall.WallType.Id, wall.LevelId, wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble(), 0, false, false);

                                    // Создаем стену справа от двери
                                    Curve rightSegmentCurve = Line.CreateBound(pointRight, wallCurve.GetEndPoint(1));
                                    Wall rightWall = Wall.Create(_doc, rightSegmentCurve, wall.WallType.Id, wall.LevelId, wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble(), 0, false, false);
                                }
                            }
                        }

                        // Удаляем исходную стену
                        _doc.Delete(wall.Id);
                    }

                    if (trans.Commit() != TransactionStatus.Committed)
                    {
                        TaskDialog.Show("Ошибка", "Не удалось выполнить транзакцию.");
                    }
                }
                else
                {
                    TaskDialog.Show("Ошибка", "Не удалось начать транзакцию.");
                }
            }
        }















        private void SetupWallParameters(Wall wall, double roomLowerOffset, string roomNameValue, string roomNumberValue, string levelRoomStringValue)
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
            Parameter skirtIdentifier = wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (skirtIdentifier != null && skirtIdentifier.StorageType == StorageType.String)
            {
                skirtIdentifier.Set("Плинтус"); // Идентификатор плинтуса
            }

            // Пример установки значения общего параметра (предполагая, что параметр уже добавлен в проект)
            Guid roomNameGuid = new Guid("4a5cec5d-f883-42c3-a05c-89ec822d637b"); // GUID общего параметра
            Parameter roomNameParam = wall.get_Parameter(roomNameGuid);
            if (roomNameParam != null && roomNameParam.StorageType == StorageType.String)
            {
                roomNameParam.Set(roomNameValue); // Установка значения параметра
            }
            // Пример установки значения общего параметра (предполагая, что параметр уже добавлен в проект)
            Guid roomNumberGuid = new Guid("317bbea6-a1a8-4923-a722-635c998c184d"); // GUID общего параметра
            Parameter roomNumberParam = wall.get_Parameter(roomNumberGuid);
            if (roomNumberParam != null && roomNumberParam.StorageType == StorageType.String)
            {
                roomNumberParam.Set(roomNumberValue); // Установка значения параметра
            }
            // Пример установки значения общего параметра (предполагая, что параметр уже добавлен в проект)
            Guid levelGuid = new Guid("9eabf56c-a6cd-4b5c-a9d0-e9223e19ea3f"); // GUID общего параметра
            Parameter wallLevelParam = wall.get_Parameter(levelGuid);
            if (wallLevelParam != null && wallLevelParam.StorageType == StorageType.String)
            {
                wallLevelParam.Set(levelRoomStringValue); // Установка значения параметра
            }

        }
    }
}