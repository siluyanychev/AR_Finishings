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
using static Autodesk.Revit.DB.SpecTypeId;


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
                //StringBuilder sb = new StringBuilder();
                //foreach (var kvp in wallDoorIntersections)
                //{
                //    foreach (var doorId in kvp.Value)
                //    {
                //        sb.AppendLine($"Wall ID: {kvp.Key.Value} : Door ID: {doorId.Value}");
                //    }
                //}

                //if (sb.Length > 0)
                //{
                //    TaskDialog.Show("Intersections", sb.ToString());
                //}
                //else
                //{
                //    TaskDialog.Show("Intersections", "No intersections found.");
                //}

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

                        // Сортируем двери по параметру на кривой, чтобы разрезать стену в правильном порядке
                        List<ElementId> sortedDoors = kvp.Value.OrderBy(
                            doorId =>
                            {
                                FamilyInstance door = _doc.GetElement(doorId) as FamilyInstance;
                                LocationPoint doorLocation = door.Location as LocationPoint;
                                return wallCurve.Project(doorLocation.Point).Parameter;
                            }
                        ).ToList();

                        double lastEndParameter = 0;

                        foreach (ElementId doorId in sortedDoors)
                        {
                            FamilyInstance door = _doc.GetElement(doorId) as FamilyInstance;
                            if (door == null) continue;

                            LocationPoint doorLocation = door.Location as LocationPoint;
                            XYZ doorPosition = doorLocation.Point;
                            double doorWidth = door.Symbol.get_Parameter(BuiltInParameter.DOOR_WIDTH).AsDouble();

                            // Находим точки разреза
                            XYZ point1 = doorPosition - door.HandOrientation * (doorWidth / 2.0);
                            XYZ point2 = doorPosition + door.HandOrientation * (doorWidth / 2.0);

                            // Получаем параметры на кривой для точек разреза
                            IntersectionResult result1 = wallCurve.Project(point1);
                            IntersectionResult result2 = wallCurve.Project(point2);

                            // Проверяем, нужно ли менять порядок точек разреза
                            if (result1.Parameter > lastEndParameter && result2.Parameter < result1.Parameter)
                            {
                                // Если параметр начала первой точки больше предыдущего конца и параметр второй точки меньше параметра первой точки,
                                // значит, произошел разворот двери. Меняем точки местами.
                                var temp = result1;
                                result1 = result2;
                                result2 = temp;
                            }

                            // Создаем сегменты только справа и слева от двери
                            if (result1.Parameter > lastEndParameter)
                            {
                                Curve leftSegment = wallCurve.Clone();
                                leftSegment.MakeBound(lastEndParameter, result1.Parameter);
                                Wall newLeftWall = Wall.Create(_doc, leftSegment, wall.WallType.Id, wall.LevelId, wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble(), 0, false, false);
                                SetupWallParameters(newLeftWall,
                                wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).AsDouble(),
                                wall.get_Parameter(new Guid("4a5cec5d-f883-42c3-a05c-89ec822d637b")).AsString(),
                                wall.get_Parameter(new Guid("317bbea6-a1a8-4923-a722-635c998c184d")).AsString(),
                                wall.get_Parameter(new Guid("9eabf56c-a6cd-4b5c-a9d0-e9223e19ea3f")).AsString());

                            }

                            lastEndParameter = result2.Parameter;
                        }

                        // Создаем последний сегмент после последней двери
                        if (lastEndParameter < wallCurve.GetEndParameter(1))
                        {
                            Curve rightSegment = wallCurve.Clone();
                            rightSegment.MakeBound(lastEndParameter, wallCurve.GetEndParameter(1));
                            Wall newRightwall = Wall.Create(_doc, rightSegment, wall.WallType.Id, wall.LevelId, wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble(), 0, false, false);
                            SetupWallParameters(newRightwall,
                                wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).AsDouble(),
                                wall.get_Parameter(new Guid("4a5cec5d-f883-42c3-a05c-89ec822d637b")).AsString(),
                                wall.get_Parameter(new Guid("317bbea6-a1a8-4923-a722-635c998c184d")).AsString(),
                                wall.get_Parameter(new Guid("9eabf56c-a6cd-4b5c-a9d0-e9223e19ea3f")).AsString());

                        }

                        // Удаляем исходную стену
                        _doc.Delete(wall.Id);
                    }


                    trans.Commit();
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